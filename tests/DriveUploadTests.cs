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
    private sealed class Transport : HttpMessageHandler
    {
        public Func<HttpRequestMessage,Task<HttpResponseMessage>> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Reply(request);
    }
    private static HttpResponseMessage Json(object value)=>new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(value),Encoding.UTF8,"application/json")};
    private static DriveUploadSettings Settings()=>new() { ServiceUrl="https://script.google.com/macros/s/test-deployment/exec",UploadKey=new string('k',40),FolderUrl="https://drive.google.com/drive/folders/folder123" };
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Upload logs use one compact local timestamp and retain multiline errors",()=> {
            var zone=TimeZoneInfo.CreateCustomTimeZone("test-eastern",TimeSpan.FromHours(-4),"test-eastern","test-eastern");
            var received=DateTimeOffset.Parse("2026-10-24T00:19:44.094Z");
            var text=UploadLogText.Format("[2026-10-24T00:19:42.814Z] Destination folder is writable.\nIOException: failure\n   at Upload()",received,zone);
            check(text=="[10/23 20:19:42] Destination folder is writable.\n[10/23 20:19:44] IOException: failure\n[10/23 20:19:44]    at Upload()","Duplicate timestamp, wrong local time, or lost exception details");
            check(UploadLogText.Format("[redacted] detail",received,zone)=="[10/23 20:19:44] [redacted] detail","Removed a non-timestamp bracket prefix");
        });
        test("Local upload copies replace atomically or keep both without losing the original",()=> {
            var folder=Path.Combine(root,"local-copies"); Directory.CreateDirectory(folder);
            var source=Path.Combine(folder,"source.pptx"); File.WriteAllText(source,"first");
            var one=LocalPresentationFiles.SaveCopy(source,Path.Combine(folder,"destination"),"PC-1025c-praise.pptx",true);
            File.WriteAllText(source,"second");
            var two=LocalPresentationFiles.SaveCopy(source,Path.GetDirectoryName(one),"PC-1025c-praise.pptx",false);
            check(one!=two && File.ReadAllText(one)=="first" && File.ReadAllText(two)=="second","Keep both overwrote the original");
            check(LocalPresentationFiles.SaveCopy(source,Path.GetDirectoryName(one),"PC-1025c-praise.pptx",true)==one && File.ReadAllText(one)=="second","Replace did not update the local copy");
            var failed=false; try {LocalPresentationFiles.SaveCopy(source,source,"PC-1025c-praise.pptx",true);}catch(IOException){failed=true;}
            check(failed && File.ReadAllText(source)=="second" && !Directory.GetFiles(Path.GetDirectoryName(one),"*.tmp").Any(),"Invalid folder damaged files or left staging files");
        });
        test("Upload folder edits survive closing and preserve language and church settings",()=> {
            var store=new LocalStore(Path.Combine(root,"folder-preferences"));
            store.SaveSettings(new UserSettings { ChurchLicense="123",Language=ChineseText.Simplified,PptxSaveFolder=Path.Combine(root,"before") });
            var window=new DriveUploadWindow(new List<SlideData>(),"test","PC-1025c-praise.pptx",store.Root); window.Show(); Dispatcher.UIThread.RunJobs();
            check(window.FindControl<TextBox>("UploadFolder").Text==Path.Combine(root,"before"),"Upload screen ignored the default folder");
            window.FindControl<TextBox>("UploadFolder").Text=Path.Combine(root,"after"); window.Close();
            var saved=store.LoadSettings(); check(saved.PptxSaveFolder==Path.Combine(root,"after") && saved.ChurchLicense=="123" && saved.Language==ChineseText.Simplified,"Folder change overwrote other settings or was not saved");
            File.WriteAllText(Path.Combine(store.Root,"legacy.json"),"{\"Language\":\"zh-Hant\"}");
            check(LocalStore.Read<UserSettings>(Path.Combine(store.Root,"legacy.json")).PptxSaveFolder==LocalPresentationFiles.DefaultFolder,"Legacy settings have no default save folder");
        });
        test("Upload saves identical local bytes before contacting Drive and keeps them after a server failure",()=> {
            foreach(var success in new[]{true,false}) {
                var store=new LocalStore(Path.Combine(root,"local-upload-"+Guid.NewGuid().ToString("N")));
                var folder=Path.Combine(store.Root,"copies"); store.SaveSettings(new UserSettings {PptxSaveFolder=folder});
                File.WriteAllText(Path.Combine(store.Root,"google-drive.json"),JsonSerializer.Serialize(Settings()));
                var name="PC-1025c-praise.pptx"; var path=Path.Combine(folder,name); long length=0;
                using var handler=new Handler {Reply=body=> {
                    check(File.Exists(path),"Contacted Drive before saving a local copy");
                    var action=body.GetProperty("action").GetString();
                    if(action=="cancel") return new {ok=true};
                    if(!success) return new {ok=false,error="Simulated server failure",stack="test"};
                    if(action=="start") {length=new FileInfo(path).Length;return new {ok=true,offset=0,folderUrl=Settings().FolderUrl};}
                    var bytes=Convert.FromBase64String(body.GetProperty("data").GetString());
                    check(File.ReadAllBytes(path).AsSpan((int)body.GetProperty("offset").GetInt64(),bytes.Length).SequenceEqual(bytes),"Drive bytes differ from the saved copy");
                    var offset=body.GetProperty("offset").GetInt64()+bytes.Length;
                    return new {ok=true,offset,complete=offset==length,fileId="file123",folderUrl=Settings().FolderUrl,expiresAt=DateTimeOffset.UtcNow.AddDays(10)};
                }};
                var slide=new SlideData {Title="Test",Lines=new(){"Local copy test"},FontName=FontCatalog.All.FirstOrDefault(f=>!f.IsBundled)?.Name ?? FontCatalog.DefaultName};
                var window=new DriveUploadWindow(new[]{slide},"test",name,store.Root,()=>new HttpClient(handler,false)); window.Show(); Dispatcher.UIThread.RunJobs();
                try {
                    var button=window.FindControl<Button>("StartUpload"); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var timeout=DateTime.UtcNow.AddSeconds(45);
                    while(!button.IsEnabled && DateTime.UtcNow<timeout) {Dispatcher.UIThread.RunJobs();Thread.Sleep(10);}
                    check(button.IsEnabled,"Upload did not finish");
                    check(File.Exists(path) && handler.Calls.Count>0,"Local copy missing after upload attempt");
                    var log=window.FindControl<TextBox>("UploadLog").Text;
                    check(log.Contains(path) && window.FindControl<HyperlinkButton>("FolderLink").IsVisible==success,"Missing local path or incorrect outcome");
                    check(success ? window.FindControl<TextBlock>("Outcome").Text.Contains("10") || window.FindControl<TextBlock>("Outcome").Text.Contains("保留") : log.Contains("Simulated server failure"),"Missing expiry or error details");
                } finally {window.Close();}
            }
        });
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
        test("Drive negotiates larger chunks without changing bytes or accepting unsupported sizes",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"upload-large-chunks.pptx"); var bytes=new byte[DriveUploadClient.PreferredChunkSize+73];
            Array.Fill(bytes,(byte)251); bytes[0]=80; bytes[1]=75; File.WriteAllBytes(path,bytes);
            var received=new MemoryStream();
            using(var handler=new Handler {Reply=body=> {
                if(body.GetProperty("action").GetString()=="start") {
                    check(body.GetProperty("chunkSize").GetInt32()==DriveUploadClient.PreferredChunkSize,"Client did not request the larger chunk size");
                    return new {ok=true,offset=0,folderUrl=Settings().FolderUrl,chunkSize=DriveUploadClient.PreferredChunkSize};
                }
                var chunk=Convert.FromBase64String(body.GetProperty("data").GetString());
                check(chunk.Length==Math.Min(DriveUploadClient.PreferredChunkSize,bytes.Length-received.Length),"Wrong negotiated chunk length");
                received.Write(chunk);
                return new {ok=true,offset=received.Length,complete=received.Length==bytes.Length,fileId="large",folderUrl=Settings().FolderUrl,chunkSize=DriveUploadClient.PreferredChunkSize};
            }})
            using(var http=new HttpClient(handler)) {
                await new DriveUploadClient(http,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None);
                check(received.ToArray().SequenceEqual(bytes) && handler.Calls.Count==3,"Larger chunks corrupted bytes or used extra requests");
            }
            using var bad=new Handler {Reply=_=>new {ok=true,offset=0,chunkSize=int.MaxValue,folderUrl=Settings().FolderUrl}};
            using var badHttp=new HttpClient(bad); var rejected=false;
            try {await new DriveUploadClient(badHttp,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None);}catch(IOException){rejected=true;}
            check(rejected && bad.Calls.All(b=>b.GetProperty("action").GetString()!="chunk"),"Unbounded server chunk size was accepted");
        }).GetAwaiter().GetResult());
        test("Drive destination mismatch is rejected before sending file content",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"wrong-destination.pptx");File.WriteAllBytes(path,new byte[]{80,75,3,4});
            using var handler=new Handler {Reply=_=>new {ok=true,offset=0,folderUrl="https://drive.google.com/drive/folders/otherFolder"}};
            using var http=new HttpClient(handler);var rejected=false;
            try {await new DriveUploadClient(http,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None);}catch(IOException){rejected=true;}
            check(rejected && handler.Calls.All(b=>b.GetProperty("action").GetString()!="chunk"),"Content was sent to the wrong folder");
        }).GetAwaiter().GetResult());
        test("Direct upload bypasses Apps Script for file bytes and resumes a lost final reply before server verification",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"direct-upload.pptx"); var bytes=new byte[DriveUploadClient.PreferredChunkSize+73]; new Random(123).NextBytes(bytes); File.WriteAllBytes(path,bytes);
            foreach(var loseReply in new[]{false,true}) {
                var received=new MemoryStream(); var finished=false; var dataCalls=0; var probes=0; var serviceCalls=0;
                using var handler=new Transport {Reply=async request=> {
                    if(request.RequestUri.Host=="script.google.com") {
                        serviceCalls++; var json=await request.Content.ReadAsStringAsync(); check(json.Length<1000,"File bytes still flow through Apps Script");
                        var body=JsonDocument.Parse(json).RootElement; var action=body.GetProperty("action").GetString();
                        if(action=="start") return Json(new {ok=true,offset=0,folderUrl=Settings().FolderUrl,chunkSize=DriveUploadClient.PreferredChunkSize,uploadUrl="https://www.googleapis.com/upload/drive/v3/files?upload_id=test-secret"});
                        check(action=="finish" && received.Length==bytes.Length,"Server verification was skipped or happened before upload"); finished=true;
                        return Json(new {ok=true,complete=true,offset=bytes.Length,fileId="direct",folderUrl=Settings().FolderUrl,expiresAt=DateTimeOffset.UtcNow.AddDays(10)});
                    }
                    check(request.RequestUri.Host=="www.googleapis.com" && request.Method==HttpMethod.Put && request.Headers.Authorization==null,"Unexpected direct upload destination or owner credentials");
                    var range=request.Content.Headers.ContentRange;
                    if(!range.HasRange) {probes++;check(received.Length==bytes.Length,"Unexpected recovery probe");return Json(new {id="direct"});}
                    check(range.From==received.Length,"Repeated or out-of-order direct bytes");
                    received.Write(await request.Content.ReadAsByteArrayAsync()); dataCalls++;
                    if(received.Length==bytes.Length) {
                        if(loseReply) throw new HttpRequestException("Lost final reply from "+request.RequestUri);
                        return Json(new {id="direct"});
                    }
                    var response=new HttpResponseMessage((HttpStatusCode)308); response.Headers.TryAddWithoutValidation("Range","bytes=0-"+(received.Length-1));return response;
                }};
                using var http=new HttpClient(handler); var progress=new Progress();
                var result=await new DriveUploadClient(http,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,progress,CancellationToken.None);
                check(finished && result.FileId=="direct" && dataCalls==2 && serviceCalls==2 && probes==(loseReply ? 1 : 0),"Unexpected direct transfer / verification request count");
                check(received.ToArray().SequenceEqual(bytes) && progress.Values.Last().Percent==100,"Direct upload bytes or final progress incorrect");
            }
        }).GetAwaiter().GetResult());
        test("Direct upload rejects unsafe destinations, requires server completion, and redacts upload URLs",()=>Task.Run(async ()=> {
            var path=Path.Combine(root,"direct-invalid.pptx"); File.WriteAllBytes(path,new byte[]{80,75,3,4});
            foreach(var url in new[]{"https://example.com/upload/drive/test","https://www.googleapis.com.evil.test/upload/drive/test","http://www.googleapis.com/upload/drive/test","https://www.googleapis.com/drive/v3/files"}) {
                using var handler=new Handler {Reply=_=>new {ok=true,offset=0,folderUrl=Settings().FolderUrl,uploadUrl=url}};using var http=new HttpClient(handler);var failed=false;
                try {await new DriveUploadClient(http,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None);}catch(IOException){failed=true;}
                check(failed && handler.Calls.Count==2,"Unsafe direct upload URL accepted");
            }
            using var pending=new Transport {Reply=request=>Task.FromResult(request.RequestUri.Host=="www.googleapis.com" ? Json(new {id="unverified"}) : Json(new {ok=true,offset=0,folderUrl=Settings().FolderUrl,uploadUrl="https://www.googleapis.com/upload/drive/v3/files?upload_id=secret"}))};
            using var pendingHttp=new HttpClient(pending);var rejected=false;
            try {await new DriveUploadClient(pendingHttp,Settings()).UploadAsync(path,"PC-1025c-praise.pptx",true,null,CancellationToken.None);}catch(IOException){rejected=true;}
            check(rejected,"Client claimed success without server confirmation");
            var redacted=new DriveUploadClient(pendingHttp,Settings()).Redact("Failure https://www.googleapis.com/upload/drive/v3/files?upload_id=secret "+Settings().UploadKey);
            check(!redacted.Contains("secret") && !redacted.Contains(Settings().UploadKey),"Upload session or shared key leaked in errors");
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
