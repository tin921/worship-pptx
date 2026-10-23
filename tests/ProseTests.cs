using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace WorshipPptx.Tests;

static class ProseTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Only the first slide of each lyric block has a plain tag, including consecutive repeats",()=> {
            foreach(var automatic in new[]{false,true}) {
                ServiceItem Song()=>new() { Title="詩歌",Lyrics="[c]\n"+string.Join('\n',Enumerable.Range(1,9).Select(i=>"恩典 "+i))+"\n[v1]\n平安",Order="c c v1 c",Formatting=new() { FontSize=48,AutoFontSize=automatic } };
                var plan=new ServicePlan { Items=new() {
                    new ServiceItem {Title="宣召",Lyrics="主是牧者\n平安"},
                    Song(),Song(),
                    new ServiceItem {Title="讀經",Lyrics="主的話語"},
                    new ServiceItem {Title="公告",Lyrics="[1]\n第一項\n[2]\n第二項"}
                } };
                var songTags=automatic ? new[]{"","c","c","v1","c"} : new[]{"","c","","","c","","","v1","c","",""};
                var expected=new[]{""}.Concat(songTags).Concat(songTags).Concat(new[]{"","",""}).ToArray();
                var slides=SlideLayout.Build(plan,new UserSettings());
                check(slides.Count==expected.Length,"The test blocks did not paginate as expected");
                for(var i=0;i<slides.Count;i++) {
                    var tag=SlideVisual.Create(slides[i]).Children.OfType<Border>().SingleOrDefault(b=>b.Name=="SlideTag");
                    check((tag?.Child as TextBlock)?.Text== (expected[i]=="" ? null : expected[i]),"Preview tag differs on slide "+(i+1));
                    check(SlideVisual.ThumbnailLabel(slides[i])=="["+slides[i].Section+"]","Navigation lost its section metadata");
                }
                var path=Path.Combine(root,"block-tags-"+automatic+".pptx"); PptxWriter.Write(path,slides,"Block tag rules");
                using(var doc=PresentationDocument.Open(path,false)) check(!new OpenXmlValidator().Validate(doc).Any(),"Block-tag deck is invalid");
                using var zip=ZipFile.OpenRead(path);
                XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",a="http://schemas.openxmlformats.org/drawingml/2006/main";
                for(var i=0;i<slides.Count;i++) {
                    using var input=zip.GetEntry($"ppt/slides/slide{i+1}.xml").Open();
                    var tags=XDocument.Load(input).Descendants(p+"sp").Where(s=>(string)s.Element(p+"nvSpPr").Element(p+"cNvPr").Attribute("name")=="Section tag").ToArray();
                    check(tags.Length==(expected[i]=="" ? 0 : 1),"Wrong exported tag count on slide "+(i+1));
                    if(tags.Length==1) check(tags[0].Descendants(a+"t").Single().Value==expected[i],"Exported tag has brackets or wrong case");
                }
            }
        });
        test("Untagged scripture keeps blank lines, spaces and tabs on one full slide",()=> {
            var text="   宣召: 詩篇 100:1–2\n\n普天下當向耶和華歡呼！\n\t你們當樂意事奉耶和華，  \n當來向他歌唱！";
            var item=new ServiceItem { Kind="song",Title="宣召經文",Lyrics=text,Order="v1 c v2 c c",SongId="old-song-number",Formatting=new() { FullSlide=true,FontSize=40,Alignment="Left" } };
            var slides=SlideLayout.BuildItem(item,new UserSettings { ChurchLicense="12345" });
            check(slides.Count==1 && !slides[0].IsTitle && slides[0].Footer=="","Untagged text got a song title, CCLI, or forced blank-line page break");
            check(slides[0].Lines.SequenceEqual(text.Replace("\t","    ").Split('\n')),"Whitespace was discarded");
            var path=Path.Combine(root,"spaced-scripture.pptx"); PptxWriter.Write(path,slides,"宣召");
            using(var doc=PresentationDocument.Open(path,false)) check(!new OpenXmlValidator().Validate(doc).Any(),"Spaced scripture deck is invalid");
            XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";
            using var zip=ZipFile.OpenRead(path); using var input=zip.GetEntry("ppt/slides/slide1.xml").Open();
            check(XDocument.Load(input).Descendants(a+"t").Select(t=>t.Value).SequenceEqual(slides[0].Lines),"PowerPoint lost indentation or empty lines, or added a Scripture tag");
            check(SlideVisual.Create(slides[0]).Children.OfType<TextBlock>().Select(t=>t.Text).SequenceEqual(slides[0].Lines),"Preview lost whitespace");
        });
        test("Prose pagination follows font metrics and half/full area, preserving all content",()=> {
            var text=string.Join('\n',Enumerable.Range(1,40).Select(i=>i%6==0 ? "" : $"  {i} 祢的恩典與慈愛  "));
            foreach(var font in FontCatalog.All) foreach(var full in new[]{false,true}) foreach(var size in new[]{18.0,40,72}) {
                var item=new ServiceItem { Title="公告",Lyrics=text,Order="stale",Copyright="資料來源",Formatting=new() { FontName=font.Name,FontSize=size,FullSlide=full } };
                var slides=SlideLayout.BuildItem(item,new UserSettings());
                check(slides.All(s=>!s.IsTitle) && string.Join('\n',slides.SelectMany(s=>s.Lines))==text,"Pagination changed content: "+font.Name);
                foreach(var slide in slides) {
                    var used=slide.Lines.Count*SlideLayout.LineSpacing(slide.FontSize,slide.FontName);
                    check(SlideLayout.TextTop(slide)+used<=SlideLayout.FooterTop(slide)-18+.01,"Prose overflows selected area: "+font.Name);
                }
            }
            var shortItem=new ServiceItem { Lyrics=string.Join('\n',Enumerable.Repeat("內容",15)),Formatting=new() { FontSize=18,FullSlide=true } };
            check(SlideLayout.BuildItem(shortItem,new UserSettings()).First().Lines.Count>8,"Prose retains a fixed song line limit");
            var wrapped=SlideLayout.Wrap(string.Concat(Enumerable.Repeat("  Grace and peace  恩典與平安\t",12)),FontCatalog.DefaultName,60);
            check(string.Concat(wrapped)==string.Concat(Enumerable.Repeat("  Grace and peace  恩典與平安    ",12)),"Word wrapping removed spaces");
            check(wrapped.All(l=>SlideLayout.Measure(l,80)<=SlideLayout.TextWidth),"Whitespace wrapping overflow");
        });
        test("Top/middle/bottom positioning agrees between preview and PowerPoint",()=> {
            var output=new List<SlideData>();
            foreach(var full in new[]{false,true}) foreach(var tagged in new[]{false,true}) {
                var tops=new List<double>();
                foreach(var position in new[]{"Top","Middle","Bottom"}) {
                    var item=new ServiceItem { Title="平安",Lyrics=tagged ? "[v1]\n平安" : "平安",Order="v1",Copyright="來源",Formatting=new() { FullSlide=full,TextPosition=position } };
                    var slides=SlideLayout.BuildItem(item,new UserSettings());
                    tops.Add(SlideLayout.TextTop(slides[0])); output.AddRange(slides);
                    foreach(var slide in slides) check(SlideLayout.TextTop(slide)+slide.Lines.Count*SlideLayout.LineSpacing(slide.FontSize,slide.FontName)<=SlideLayout.FooterTop(slide),"Position overlaps credits or leaves area");
                }
                check(tops[0]<tops[1] && tops[1]<tops[2] && Math.Abs(tops[1]-(tops[0]+tops[2])/2)<.01,"Position does not move text within its available area");
            }
            var path=Path.Combine(root,"vertical-position.pptx"); PptxWriter.Write(path,output,"位置");
            XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main",p="http://schemas.openxmlformats.org/presentationml/2006/main";
            using var zip=ZipFile.OpenRead(path);
            for(var i=0;i<output.Count;i++) {
                using var input=zip.GetEntry($"ppt/slides/slide{i+1}.xml").Open();
                var preview=SlideVisual.Create(output[i]).Children.OfType<TextBlock>().ToArray();
                var shapes=XDocument.Load(input).Descendants(p+"sp").ToArray();
                for(var j=0;j<preview.Length;j++) check((long)shapes[j].Element(p+"spPr").Element(a+"xfrm").Element(a+"off").Attribute("y")==Math.Round(Canvas.GetTop(preview[j])*9525),"Exported text position differs from preview");
            }
        });
        test("Numbered announcements each fit one slide at the largest size up to the selected maximum",()=> {
            var first="  歡迎參加主日崇拜  ";
            var second="\t教會消息\n\n"+string.Concat(Enumerable.Repeat("請弟兄姊妹於聚會後到接待處報名參加下週活動。  ",24));
            var output=new List<SlideData>();
            foreach(var font in FontCatalog.Bundled) foreach(var full in new[]{false,true}) {
                var item=new ServiceItem { Title="教會消息",Lyrics="[1]\n"+first+"\n[2]\n"+second,Order="v1 c",SongId="ignored",Formatting=new() { FontName=font.Name,FontSize=60,FullSlide=full,Bold=true,TextPosition="Middle" } };
                var slides=SlideLayout.BuildItem(item,new UserSettings { ChurchLicense="123" });
                check(slides.Count==2 && slides[0].Section=="公告 1" && slides[1].Section=="公告 2" && slides.All(s=>!s.IsTitle && s.Footer==""),"Announcement tags did not produce one slide per item");
                check(slides[0].FontSize==60 && slides[1].FontSize<60 && item.Formatting.FontSize==60,"Maximum size changed or long announcement did not shrink");
                check(string.Concat(slides[0].Lines)==first && string.Concat(slides[1].Lines)==second.Replace("\t","    ").Replace("\n",""),"Announcement fitting dropped content or whitespace");
                foreach(var slide in slides) {
                    check(SlideLayout.TextTop(slide)+slide.Lines.Count*SlideLayout.LineSpacing(slide.FontSize,slide.FontName,slide.Bold)<=SlideLayout.FooterTop(slide)+.01,"Announcement does not fit on its slide");
                    check(slide.Lines.All(l=>SlideLayout.Measure(l,SlideLayout.FontPixels(slide.FontSize),slide.FontName,slide.Bold)<=SlideLayout.TextWidth),"Announcement overflows horizontally");
                }
                var nextSize=slides[1].FontSize+1;
                var nextLines=second.Split('\n').SelectMany(l=>l.Length==0 ? new List<string>{""} : SlideLayout.Wrap(l,font.Name,nextSize,true)).Count();
                check(nextLines*SlideLayout.LineSpacing(nextSize,font.Name,true)>(full ? 652 : 344)-SlideLayout.TextY,"Font was shrunk more than necessary");
                output.AddRange(slides);
            }
            var path=Path.Combine(root,"numbered-announcements.pptx"); PptxWriter.Write(path,output,"教會消息");
            using(var doc=PresentationDocument.Open(path,false)) check(!new OpenXmlValidator().Validate(doc).Any() && doc.PresentationPart.SlideParts.Count()==output.Count,"Announcement export invalid or has extra slides");
            XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";
            using var zip=ZipFile.OpenRead(path);
            for(var i=0;i<output.Count;i++) {
                using var input=zip.GetEntry($"ppt/slides/slide{i+1}.xml").Open(); var xml=XDocument.Load(input);
                check(xml.Descendants(a+"rPr").All(r=>(int)r.Attribute("sz")==output[i].FontSize*100),"Export did not use the fitted font size");
            }
            foreach(var invalid in new[]{"[1]\n甲\n[1]\n乙","[1]\n\n[2]\n乙","前言\n[1]\n甲","[v1]\n甲\n[2]\n乙"}) {
                var rejected=false; try { SlideLayout.BuildItem(new ServiceItem { Lyrics=invalid },new UserSettings()); } catch(FormatException) { rejected=true; }
                check(rejected,"Ambiguous announcement structure was accepted");
            }
        });
        test("Control-Tab inserts indentation and removing tags switches to prose without losing source text",()=> {
            var store=new LocalStore(Path.Combine(root,"prose-edit-"+Guid.NewGuid().ToString("N")));
            store.Save(Demo.Create()); var window=new MainWindow(store); window.Show(); Dispatcher.UIThread.RunJobs();
            const string source="   宣召\n\n    經文  ";
            try {
                var lyrics=window.FindControl<TextBox>("Lyrics"); lyrics.Text=source; Dispatcher.UIThread.RunJobs();
                check(!window.FindControl<StackPanel>("OrderPanel").IsVisible && !window.FindControl<TextBox>("SongId").IsEnabled,"Untagged text remains in song mode");
                lyrics.Focus(); lyrics.CaretIndex=0; window.KeyPress(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,null); window.KeyRelease(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,null);
                check(lyrics.IsFocused && lyrics.Text=="\t"+source,"Tab moved focus or lost text");
                lyrics.Text="[v1]\n"+lyrics.Text; Dispatcher.UIThread.RunJobs();
                check(window.FindControl<StackPanel>("OrderPanel").IsVisible && window.FindControl<TextBox>("SongId").IsEnabled,"Adding a section tag did not restore song fields");
            } finally { window.Close(); }
            check(store.LoadHistory().Single().Items[1].Lyrics=="[v1]\n\t"+source,"Saving trimmed raw whitespace");
        });
    }
}
