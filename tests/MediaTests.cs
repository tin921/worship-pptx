using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using SkiaSharp;

namespace WorshipPptx.Tests;

static class MediaTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root,LocalStore store,ImageStore images)
    {
        GalleryTests.Run(test,check,images);
        test("All 80 themes display Chinese and add English only to the Google link",()=> {
            var scenes=JsonSerializer.Deserialize<List<MediaScene>>(File.ReadAllText(Path.Combine(MediaLibrary.Root,"scenes.json")),MediaLibrary.JsonOptions);
            try {
                foreach(var language in new[]{ChineseText.Traditional,ChineseText.Simplified}) {
                    ChineseText.SetLanguage(language);
                    foreach(var scene in scenes) {
                        var display=ImageKeywords.ForDisplay(scene);
                        check(display.Split('、').Length==4 && !System.Text.RegularExpressions.Regex.IsMatch(display,"[A-Za-z]"),"English leaked into the app: "+scene.Id);
                        check(display.Split('、').All(term=>ImageKeywords.EnglishFor(term).Any()),"Missing English equivalent: "+scene.Id);
                        var query=Uri.UnescapeDataString(ImageSearchLink.Create(display).Query);
                        check(query.Contains(ImageKeywords.EnglishFor(display.Split('、')[0]).First()),"Google subject missing English: "+scene.Id);
                    }
                }
                var edited=Uri.UnescapeDataString(ImageSearchLink.Create("彩虹、雨後天空").Query);
                check(edited.Contains("rainbow") && !edited.Contains("kneeling prayer"),"Edited subject retained a stale English translation");
                check(!ImageKeywords.EnglishFor("自訂陌生詞").Any(),"Unknown custom text was assigned an unrelated English term");
                check(!MediaSearch.IsModelLoaded,"Keyword display or link creation loaded the model");
            } finally { ChineseText.SetLanguage(ChineseText.Traditional); }
        });
        test("Editor, keyword typing and metadata filters never load the embedding model",()=> {
            check(!MediaSearch.IsModelLoaded,"Model loaded before search");
            var picker=new BackgroundDialog(images,""); picker.Show();
            picker.FindControl<TextBox>("SearchText").Text="彩虹";
            picker.FindControl<ComboBox>("CollectionFilter").SelectedIndex=3;
            Dispatcher.UIThread.RunJobs();
            picker.FindControl<ComboBox>("CollectionFilter").SelectedIndex=0;
            foreach(var style in new[]{4,5}) {
                picker.FindControl<ComboBox>("StyleFilter").SelectedIndex=style; Dispatcher.UIThread.RunJobs();
                check(picker.FindControl<WrapPanel>("Gallery").Children.Count==1,"New style filter did not find its sample");
            }
            check(!MediaSearch.IsModelLoaded,"Typing/filtering initialized model"); picker.Close();
        });
        test("Local tokenizer and quantized embeddings agree with Hugging Face fixtures",()=> {
            using var encoder=new LocalEncoder();
            using var fixtures=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"embedding-fixtures.json")));
            var minimumCosine=1.0;
            foreach(var fixture in fixtures.RootElement.EnumerateArray()) {
                var text=fixture.GetProperty("text").GetString(); var actual=encoder.Tokenize(text);
                var expected=fixture.GetProperty("ids").EnumerateArray().Select(v=>v.GetInt64()).ToArray();
                check(actual.SequenceEqual(expected),$"Tokenizer mismatch: {text}: {string.Join(',',actual)} vs {string.Join(',',expected)}");
                var vector=encoder.Encode(actual); var reference=fixture.GetProperty("vector").EnumerateArray().Select(v=>v.GetSingle()).ToArray();
                var cosine=vector.Zip(reference,(a,b)=>(double)a*b).Sum();
                Console.WriteLine($"  Embedding cosine {cosine:F6}: {text}");
                minimumCosine=Math.Min(minimumCosine,cosine);
            }
            // Quantized kernels differ slightly across runtime versions/CPU architectures.
            // Require near-identical direction; token IDs above remain an exact comparison.
            check(minimumCosine>.995,$"Model mismatch: minimum reference cosine {minimumCosine:F6}");
            check(encoder.Normalize("祢的恩典與慈愛") == encoder.Normalize("你的恩典与慈爱"),"Chinese normalization differs");
        });
        test("Opening Edit Background matches the song and creates an editable Google Images link",()=> {
            var picker=new BackgroundDialog(images,"","恩典之路","[v1]\n前路穿過高山低谷\n每一步都被引領");
            check(!MediaSearch.IsModelLoaded,"Constructing an unopened picker initialized the model"); picker.Show();
            check(picker.FindControl<Border>("MatchingStatus").IsVisible && picker.FindControl<ProgressBar>("MatchProgressBar").IsIndeterminate && picker.FindControl<TextBlock>("Message").Text.Contains("擷取主題及關鍵字"),"First match has no visible loading explanation");
            var button=picker.FindControl<Button>("SearchButton");
            var timer=Stopwatch.StartNew();
            while(!button.IsEnabled && timer.Elapsed.TotalSeconds<40) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); }
            check(button.IsEnabled && MediaSearch.IsModelLoaded,"Search did not finish");
            check(!picker.FindControl<Border>("MatchingStatus").IsVisible && picker.FindControl<ProgressBar>("MatchProgressBar").Value==100,"Completed progress overlay remained visible");
            check(picker.FindControl<TextBlock>("Message").Text.StartsWith("已按歌名及歌詞"),picker.FindControl<TextBlock>("Message").Text);
            check(picker.FindControl<WrapPanel>("Gallery").Children.Count>0,"No media returned");
            var keywords=picker.FindControl<TextBox>("SearchText"); var link=picker.FindControl<HyperlinkButton>("GoogleImageLink");
            check(!string.IsNullOrWhiteSpace(keywords.Text) && link.IsEnabled && link.NavigateUri.Host=="www.google.com","Keywords/link not populated");
            check(!System.Text.RegularExpressions.Regex.IsMatch(keywords.Text,"[A-Za-z]"),"English keywords are visible in the app");
            check(!Uri.UnescapeDataString(link.NavigateUri.Query).Contains("每一步"),"Lyrics were included in the Google link");
            keywords.Text="雨後 彩虹 & 日光"; Dispatcher.UIThread.RunJobs();
            check(link.NavigateUri.AbsoluteUri.Contains(Uri.EscapeDataString(keywords.Text)) && link.NavigateUri.Query.Contains("tbm=isch"),"Edited keywords not escaped or not an image search");
            keywords.Text=""; Dispatcher.UIThread.RunJobs(); check(!link.IsEnabled,"Empty search link stays enabled");
            keywords.Text="、 / OR /"; Dispatcher.UIThread.RunJobs(); check(!link.IsEnabled,"Punctuation-only search stays enabled"); picker.Close();
            var traditional=MediaSearch.SearchAsync("","雨後彩虹帶來盼望").Result;
            var simplified=MediaSearch.SearchAsync("","雨后彩虹带来盼望").Result;
            check(traditional.Select(a=>a.Id).SequenceEqual(simplified.Select(a=>a.Id)),"Traditional/Simplified results differ");
            check(traditional.First().Theme=="rainbow","Concrete visual cue not ranked first");
            if(OperatingSystem.IsWindows() && File.Exists(Path.Combine(AppContext.BaseDirectory,"VC-RUNTIME-MANIFEST.json"))) {
                foreach(var name in new[]{"msvcp140.dll","msvcp140_1.dll","vcruntime140.dll","vcruntime140_1.dll"}) {
                    var loaded=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().FirstOrDefault(m=>m.ModuleName.Equals(name,StringComparison.OrdinalIgnoreCase));
                    check(loaded!=null && Path.GetDirectoryName(loaded.FileName).Equals(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase),"Runtime not loaded from app folder: "+name);
                }
            }
        });
        test("Matching reports completed stages and respects cancellation without false completion",()=> {
            var updates=new List<MediaSearchProgress>();
            var progress=new InlineProgress(updates.Add);
            var lyrics=string.Join('\n',Enumerable.Range(0,30).Select(i=>$"第{i}天在高山低谷都經歷祢的恩典與帶領"));
            var match=MediaSearch.MatchAsync("恩典之路",lyrics,progress:progress).GetAwaiter().GetResult();
            var numbers=updates.Where(p=>p.Percent.HasValue).Select(p=>p.Percent.Value).ToArray();
            check(updates[0].Percent==null && numbers.First()==0 && numbers.Last()==100 && numbers.Zip(numbers.Skip(1),(a,b)=>a<=b).All(b=>b),"Progress is missing stages or goes backwards");
            check(numbers.Length>4 && numbers.All(n=>n>=0 && n<=100) && match.Assets.Count>0,"Long lyrics have no measurable intermediate progress");
            using var cancellation=new CancellationTokenSource(); updates.Clear();
            var cancelProgress=new InlineProgress(value=> { updates.Add(value); if(value.Percent>0) cancellation.Cancel(); });
            var cancelled=false;
            try { MediaSearch.MatchAsync("恩典之路",lyrics,cancellation.Token,cancelProgress).GetAwaiter().GetResult(); }
            catch(OperationCanceledException) { cancelled=true; }
            check(cancelled && !updates.Any(p=>p.Percent==100),"Cancelled matching claimed completion");
        });
        test("Old animated plans keep their background and load as static",()=> {
            var file=Path.Combine(root,"legacy-item.json"); File.WriteAllText(file,"{\"Title\":\"舊詩歌\",\"Background\":\"media:mountain_path-photo\",\"AnimateBackground\":true}");
            var item=LocalStore.Read<ServiceItem>(file); check(item.Background=="media:mountain_path-photo","Old background lost");
            LocalStore.Write(file,item); check(!File.ReadAllText(file).Contains("AnimateBackground"),"Animation option is still saved");
        });
        test("Plus is the last tab and adds a real song before itself",()=> {
            var window=new MainWindow(store); window.Show(); Dispatcher.UIThread.RunJobs();
            try {
                var tabs=window.FindControl<TabControl>("SongTabs"); var count=tabs.Items.Count; tabs.SelectedIndex=count-1; Dispatcher.UIThread.RunJobs();
                check(tabs.Items.Count==count+1,"Plus did not add song");
                check(tabs.Items.Cast<TabItem>().Last().Classes.Contains("add-tab"),"Plus moved from last position");
                check(((TabItem)tabs.SelectedItem).Tag is ServiceItem,"Plus stayed selected");
            } finally { window.Close(); }
        });
        test("Library has 272 static 720p images including two new style samples",()=> {
            check(MediaLibrary.All.Count==272,$"Expected 272, found {MediaLibrary.All.Count}");
            var themed=MediaLibrary.All.Where(a=>a.Style is "photo" or "illustration" or "abstract").ToList();
            check(themed.Count==240 && themed.Count(a=>a.Core)==180,"Wrong core/optional distribution");
            check(themed.GroupBy(a=>a.Theme).All(g=>g.Count()==3 && g.Select(a=>a.Style).Distinct().Count()==3),"Missing style variations");
            check(MediaLibrary.All.Single(a=>a.Style=="watercolour").Theme=="kneeling_prayer" && MediaLibrary.All.Single(a=>a.Style=="sculpted").Theme=="crown","Missing new style samples");
            foreach(var asset in MediaLibrary.All) {
                using var codec=SKCodec.Create(MediaLibrary.FilePath(asset.Still));
                check(codec.Info.Width==1280 && codec.Info.Height==720,"Wrong still size: "+asset.Id);
            }
            check(!Directory.EnumerateFiles(MediaLibrary.Root,"*.mp4",SearchOption.AllDirectories).Any() && !Directory.EnumerateFiles(MediaLibrary.Root,"*.webp",SearchOption.AllDirectories).Any(),"Animation files remain in the app library");
        });
        test("Static preview has no shaded text panel",()=> {
            var visual=SlideVisual.Create(new SlideData { Background=MediaLibrary.All.First().Key,Lines=new(){"平安"},Footer="版權" });
            check(visual.Children.First() is Image && visual.Children.Skip(1).All(c=>c is TextBlock),"A shading or animation layer remains");
        });
        test("PowerPoint exports only still images with editable coloured text",()=> {
            var asset=MediaLibrary.All.First(a=>a.Style=="generic" && a.Lighting=="daylight");
            var slides=new List<SlideData> { new() { Background=asset.Key,Lines=new(){"平安與盼望"},TextColor="#B83D3D",Footer="版權 · CCLI Song #123" },new() { Background=asset.Key,Lines=new(){"同心讚美"},TextColor="#B83D3D" } };
            var path=Path.Combine(root,"static-backgrounds.pptx"); PptxWriter.Write(path,slides,"靜態背景測試");
            System.Threading.Tasks.Task.Run(()=>PptxWriter.Write(Path.Combine(root,"static-backgrounds-worker.pptx"),slides,"背景匯出")).GetAwaiter().GetResult();
            using(var document=PresentationDocument.Open(path,false)) {
                var errors=new OpenXmlValidator(FileFormatVersions.Office2019).Validate(document).ToList();
                check(errors.Count==0,string.Join('\n',errors.Take(10).Select(e=>e.Description+" "+e.Path?.XPath)));
            }
            using var zip=ZipFile.OpenRead(path);
            check(!zip.Entries.Any(e=>e.FullName.EndsWith(".mp4")),"Video still embedded");
            check(zip.Entries.Count(e=>e.FullName.StartsWith("ppt/media/"))==1,"Still image not deduplicated");
            XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",a="http://schemas.openxmlformats.org/drawingml/2006/main";
            for(var i=1;i<=slides.Count;i++) {
                using var input=zip.GetEntry($"ppt/slides/slide{i}.xml").Open(); var xml=XDocument.Load(input);
                check(!xml.Descendants(p+"video").Any() && !xml.Descendants(p+"timing").Any(),"Animation timeline remains");
                check(xml.Descendants(p+"sp").All(s=>s.Element(p+"txBody")!=null),"Shaded overlay still exported");
                check(xml.Descendants(a+"rPr").All(r=>(string)r.Element(a+"solidFill")?.Element(a+"srgbClr")?.Attribute("val")=="B83D3D"),"Text colour not exported");
            }
        });
    }
    private sealed class InlineProgress(Action<MediaSearchProgress> report) : IProgress<MediaSearchProgress>
    {
        public void Report(MediaSearchProgress value) => report(value);
    }
}
