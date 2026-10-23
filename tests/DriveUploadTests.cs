using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SkiaSharp;

namespace WorshipPptx.Tests;

static class DriveUploadTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public Func<JsonElement,object> Reply;
        public readonly List<JsonElement> Calls=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var body=JsonDocument.Parse(await request.Content.ReadAsStringAsync(token)).RootElement.Clone(); Calls.Add(body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(Reply(body)),Encoding.UTF8,"application/json") };
        }
    }
    private sealed class Progress : IProgress<UploadProgress> { public readonly List<UploadProgress> Values=new(); public void Report(UploadProgress value)=>Values.Add(value); }
    private static DriveUploadSettings Settings()=>new() { ServiceUrl="https://script.google.com/macros/s/test-deployment/exec",UploadKey=new string('k',40),FolderUrl="https://drive.google.com/drive/folders/folder123" };
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Local and Drive filenames share Sunday, language and invocation rules; only Drive adds the PC",()=> {
            var previous=ChineseText.Current.Language;
            try {
                ChineseText.SetLanguage(ChineseText.Traditional);
                var plan=new ServicePlan { Items=new() { new ServiceItem { Kind="scripture",Lyrics="願榮耀歸於主" } } };
                check(DriveFileName.Create(plan,"My PC!_01",new DateTime(2026,10,23),ChineseText.Traditional)=="MyPC01-1025c-call+praise.pptx","Traditional invocation filename failed");
                check(PresentationFileName.Create(plan,new DateTime(2026,10,23),ChineseText.Traditional)=="1025c-call+praise.pptx","Local Traditional invocation filename failed");
                ChineseText.SetLanguage(ChineseText.Simplified);
                check(DriveFileName.Create(plan,"PC",new DateTime(2026,12,31),ChineseText.Simplified)=="PC-0103m-call+praise.pptx","Simplified/year boundary failed");
                check(PresentationFileName.Create(plan,new DateTime(2026,12,31),ChineseText.Simplified)=="0103m-call+praise.pptx","Local Simplified/year boundary failed");
                plan.Items[0].Lyrics="[v1]\n荣耀归主";
                check(DriveFileName.Create(plan,"!",new DateTime(2026,10,25),ChineseText.Simplified)=="PC-1025m-praise.pptx","Sunday or tagged song treated as invocation");
                check(PresentationFileName.Create(plan,new DateTime(2026,10,25),ChineseText.Simplified)=="1025m-praise.pptx","Local Sunday or tagged song treated as invocation");
                plan.Items[0].Lyrics="[1]\n通知";
                check(!DriveFileName.Create(plan,"PC",DateTime.Today,ChineseText.Traditional).Contains("call+"),"Announcement treated as scripture");
                check(!PresentationFileName.Create(plan,DateTime.Today,ChineseText.Traditional).Contains("call+"),"Local announcement treated as scripture");
                plan.Items[0].Title="Praise"; plan.Items[0].Lyrics="[v1]\nGrace and peace";
                check(PresentationFileName.Create(plan,new DateTime(2026,10,23),ChineseText.Simplified)=="1025m-praise.pptx" && PresentationFileName.Create(plan,new DateTime(2026,10,23),ChineseText.Traditional)=="1025c-praise.pptx","Local filename ignored language preference for neutral text");
                DriveFileName.Validate("PC-0229c-call+praise.pptx");
                foreach(var name in new[]{"../file.pptx","PC-1330c-praise.pptx","PC-0230c-praise.pptx","PC-1025c-praise.exe","PC-1025C-praise.pptx","PC-1025M-praise.pptx"}) {
                    var rejected=false; try { DriveFileName.Validate(name); } catch(ArgumentException) { rejected=true; } check(rejected,"Unsafe or invalid filename accepted");
                }
            } finally { ChineseText.SetLanguage(previous); }
        });
        test("Unconfigured Drive dialog is explicit and supplies timestamped copyable diagnostics",()=> {
            var path=Path.Combine(root,"unconfigured-drive"); Directory.CreateDirectory(path);
            var window=new DriveUploadWindow(new List<SlideData>(),"test","PC-1025c-praise.pptx",path); window.Show(); Dispatcher.UIThread.RunJobs();
            try {
                check(window.FindControl<CheckBox>("ReplaceExisting").IsChecked==true,"Replacement not checked");
                window.FindControl<Button>("StartUpload").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
                var log=window.FindControl<TextBox>("UploadLog");
                check(log.IsReadOnly && log.Text.Contains("InvalidOperationException") && log.Text.Contains("google-drive.json") && log.Text.StartsWith("["),"Missing setup/error log");
                check(!window.FindControl<HyperlinkButton>("FolderLink").IsVisible,"Unconfigured dialog claims success");
            } finally { window.Close(); }
        });
        test("Drive upload sends ordered chunks, replacement flag and verifies completion",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"upload-protocol.pptx"); var bytes=new byte[DriveUploadClient.ChunkSize+73]; bytes[0]=80;bytes[1]=75; new Random(42).NextBytes(bytes.AsSpan(2)); File.WriteAllBytes(path,bytes);
            foreach(var replace in new[]{true,false}) {
                var received=new MemoryStream(); var progress=new Progress();
                using var handler=new Handler { Reply=body=> {
                    if(body.GetProperty("action").GetString()=="start") return new {ok=true,offset=0,folderUrl=Settings().FolderUrl};
                    var offset=body.GetProperty("offset").GetInt64(); check(offset==received.Length,"Out of order bytes");
                    var chunk=Convert.FromBase64String(body.GetProperty("data").GetString()); received.Write(chunk);
                    return new {ok=true,offset=received.Length,complete=received.Length==bytes.Length,fileId="file123",folderUrl=Settings().FolderUrl};
                } };
                using var http=new HttpClient(handler);
                var result=await new DriveUploadClient(http,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",replace,progress,CancellationToken.None);
                check(received.ToArray().SequenceEqual(bytes) && result.FileId=="file123","Upload bytes or result corrupted");
                check(handler.Calls[0].GetProperty("replace").GetBoolean()==replace && handler.Calls.Count==3,"Wrong replacement mode / chunk count");
                check(progress.Values.Last().Percent==100 && progress.Values.Any(p=>p.Percent>0 && p.Percent<100),"No intermediate/complete progress");
            }
        }).GetAwaiter().GetResult());
        test("Drive failures redact the shared key and release the session; cancellation sends no chunks",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"upload-failure.pptx"); File.WriteAllBytes(path,new byte[]{80,75,3,4});
            var settings=Settings();
            using(var handler=new Handler { Reply=b=> b.GetProperty("action").GetString()=="cancel" ? new {ok=true} : (object)new {ok=false,error="Bad key "+settings.UploadKey,stack="server stack"} })
            using(var http=new HttpClient(handler)) {
                var failed=false; try { await new DriveUploadClient(http,settings).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None); }
                catch(IOException ex) {failed=true;check(ex.ToString().Contains("server stack") && !ex.ToString().Contains(settings.UploadKey),"Secret leaked / stack lost");}
                check(failed && handler.Calls.Last().GetProperty("action").GetString()=="cancel","Failed upload not released");
            }
            using var cancellation=new CancellationTokenSource();
            using var canceledHandler=new Handler {Reply=b=> {if(b.GetProperty("action").GetString()=="start") cancellation.Cancel();return new {ok=true,offset=0,folderUrl=Settings().FolderUrl};}};
            using var canceledHttp=new HttpClient(canceledHandler);
            var canceled=false;try { await new DriveUploadClient(canceledHttp,settings).UploadAsync(path,"PC-1025c-praise.pptx",true,null,cancellation.Token); }catch(OperationCanceledException){canceled=true;}
            check(canceled && canceledHandler.Calls.All(b=>b.GetProperty("action").GetString()!="chunk"),"Canceled upload still sent content");
        }).GetAwaiter().GetResult());
        test("Drive destination mismatch is rejected before sending file content",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"wrong-destination.pptx");File.WriteAllBytes(path,new byte[]{80,75,3,4});
            using var handler=new Handler {Reply=_=>new {ok=true,offset=0,folderUrl="https://drive.google.com/drive/folders/otherFolder"}};
            using var http=new HttpClient(handler);var rejected=false;
            try {await new DriveUploadClient(http,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None);}catch(IOException){rejected=true;}
            check(rejected && handler.Calls.All(b=>b.GetProperty("action").GetString()!="chunk"),"Content was sent to the wrong folder");
        }).GetAwaiter().GetResult());
        test("Zero opacity is white in the preview and exported background",()=> {
            var slide=new SlideData {BackgroundTransform=new() {Opacity=0}};
            var visual=SlideVisual.Create(slide);
            check(((Avalonia.Media.ISolidColorBrush)visual.Background).Color==Avalonia.Media.Colors.White,"Preview fades to black");
            using var bitmap=SKBitmap.Decode(SlideVisual.BackgroundPng("",slide.BackgroundTransform));
            check(bitmap.GetPixel(640,360)==SKColors.White && bitmap.GetPixel(0,0)==SKColors.White,"Export does not fade to white");
            using var full=SKBitmap.Decode(SlideVisual.BackgroundPng(""));
            using var half=SKBitmap.Decode(SlideVisual.BackgroundPng("",new BackgroundTransform {Opacity=.5}));
            var a=full.GetPixel(640,360);var b=half.GetPixel(640,360);check(b.Red>=a.Red && b.Green>=a.Green && b.Blue>=a.Blue,"Half opacity darkens the image");
        });
    }
}
