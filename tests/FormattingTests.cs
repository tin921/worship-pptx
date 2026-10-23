using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace WorshipPptx.Tests;

static class FormattingTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Legacy formatting migrates independently to every tab",()=> {
            var path=Path.Combine(root,"legacy-format.json");
            File.WriteAllText(path,"{\"Title\":\"舊崇拜\",\"FontName\":\"Noto Serif CJK HK\",\"FontSize\":60,\"Alignment\":\"Right\",\"TextColor\":\"#2457A7\",\"Items\":[{\"Title\":\"甲\"},{\"Title\":\"乙\"}]}");
            var plan=LocalStore.Read<ServicePlan>(path);
            check(plan.Items.All(i=>i.Formatting.FontSize==60 && i.Formatting.FontName=="Noto Serif CJK HK" && i.Formatting.Alignment=="Right" && i.Formatting.TextColor=="#2457A7" && !i.Formatting.Bold && !i.Formatting.FullSlide),"Old appearance changed");
            plan.Items[0].Formatting.FontSize=28;
            check(plan.Items[1].Formatting.FontSize==60,"Tabs share a mutable formatting instance");
            LocalStore.Write(path,plan); var again=LocalStore.Read<ServicePlan>(path);
            check(again.Items[0].Formatting.FontSize==28 && again.Items[1].Formatting.FontSize==60,"Legacy values overwrite saved tab settings");
        });
        test("History renames live, formatting stays per tab and swatches fit without scrolling",()=> {
            var store=new LocalStore(Path.Combine(root,"tab-format-"+Guid.NewGuid().ToString("N")));
            var original=Demo.Create(); store.Save(original);
            var window=new MainWindow(store); window.Show(); Dispatcher.UIThread.RunJobs();
            try {
                window.FindControl<TextBox>("ServiceTitle").Text="asdf"; Dispatcher.UIThread.RunJobs();
                var history=window.FindControl<ListBox>("HistoryList");
                check(history.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text=="asdf"),"History label waits for a save/reload");
                var tabs=window.FindControl<TabControl>("SongTabs");
                window.FindControl<ComboBox>("FontPicker").SelectedItem=FontCatalog.Get("Noto Serif CJK HK");
                window.FindControl<ComboBox>("FontSizePicker").SelectedItem=60.0;
                var bold=window.FindControl<ToggleButton>("BoldToggle"); bold.IsChecked=true; bold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.FindControl<ToggleButton>("PositionBottom").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.FindControl<ToggleButton>("AlignRight").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var swatches=window.FindControl<UniformGrid>("ColorSwatches");
                swatches.Children.OfType<Button>().Single(b=>((TextColorChoice)b.Tag).Hex=="#B83D3D").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                tabs.SelectedIndex=2; Dispatcher.UIThread.RunJobs();
                check((double)window.FindControl<ComboBox>("FontSizePicker").SelectedItem==36 && bold.IsChecked==false && window.FindControl<ToggleButton>("AlignCenter").IsChecked==true && window.FindControl<ToggleButton>("PositionTop").IsChecked==true,"Formatting leaked into another song");
                tabs.SelectedIndex=1; Dispatcher.UIThread.RunJobs();
                check((double)window.FindControl<ComboBox>("FontSizePicker").SelectedItem==60 && bold.IsChecked==true && window.FindControl<ToggleButton>("AlignRight").IsChecked==true && window.FindControl<ToggleButton>("PositionBottom").IsChecked==true,"Switching tabs lost formatting");
                var color=window.FindControl<Button>("TextColorPicker"); color.Flyout.ShowAt(color); Dispatcher.UIThread.RunJobs();
                check(swatches.Bounds.Width<=300 && swatches.Bounds.Height<=300 && TextColors.All.Select(c=>c.Hex).Distinct().Count()==64,"Palette needs scrolling or repeats colours");
                check(!swatches.GetVisualDescendants().OfType<TextBlock>().Any(),"Colour names appear in palette");
                check(color.GetVisualDescendants().OfType<Border>().Any(b=>b.Name=="PaletteBorder" && b.BorderThickness==new Thickness(1) && b.Bounds.Width==color.Bounds.Width),"Colour button has no complete border");
                color.Flyout.Hide();
                var block=window.FindControl<Grid>("BackgroundToolbar");
                var photo=window.FindControl<Border>("BackgroundBox"); var controls=window.FindControl<StackPanel>("FormattingPanel");
                check(Math.Abs(photo.Bounds.Width-controls.Bounds.Width)<1,"Background does not occupy the left half");
                var heading=window.FindControl<TextBlock>("AppHeading");
                check(Math.Abs(heading.TranslatePoint(default,window).Value.Y-tabs.TranslatePoint(default,window).Value.Y)<1,"Sidebar heading does not align with tab row");
                check(!window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Name=="FontHint" || t.Text=="✦"),"Removed font note or star still visible");
                var order=window.FindControl<TextBox>("SongOrder"); var lyrics=window.FindControl<TextBox>("Lyrics");
                var orderBottom=order.TranslatePoint(default,window).Value.Y+order.Bounds.Height;
                check(Math.Abs(lyrics.TranslatePoint(default,window).Value.Y-orderBottom-12)<1,"Repeat order is not immediately above lyrics");
                var toolbar=window.FindControl<WrapPanel>("TypographyRow");
                foreach(var name in new[]{"PositionTop","PositionMiddle","PositionBottom"}) check(window.FindControl<ToggleButton>(name).Content is PathIcon,"Vertical alignment still uses a text label");
                foreach(var width in new[]{144.0,295.0}) {
                    toolbar.Measure(new Size(width,double.PositiveInfinity)); toolbar.Arrange(new Rect(0,0,width,toolbar.DesiredSize.Height));
                    check(toolbar.Children.All(c=>c.Bounds.X>=0 && c.Bounds.Right<=width+.1),"Formatting controls clip in narrow panel");
                    for(int i=0;i<toolbar.Children.Count;i++) for(int j=i+1;j<toolbar.Children.Count;j++) check(!toolbar.Children[i].Bounds.Intersects(toolbar.Children[j].Bounds),"Formatting borders overlap");
                }
            } finally { window.Close(); }
            var saved=store.LoadHistory().Single();
            check(saved.Title=="asdf" && saved.Items[1].Formatting.Bold && !saved.Items[1].Formatting.FullSlide && saved.Items[1].Formatting.TextPosition=="Bottom" && saved.Items[2].Formatting.TextPosition=="Top" && saved.Items[1].Formatting.TextColor=="#B83D3D" && !saved.Items[2].Formatting.Bold,"Settings did not survive saving");
        });
        test("Full-slide bold text exports while singer guide stays preview-only",()=> {
            var item=new ServiceItem { Title="全畫面測試",Lyrics="[v1]\n第一行\n第二行\n第三行\n第四行\n第五行\n第六行",Order="v1",Copyright="版權",SongId="123" };
            var settings=new UserSettings { ChurchLicense="456" };
            var top=SlideLayout.BuildItem(item,settings);
            item.Formatting.FullSlide=true; item.Formatting.Bold=true;
            var full=SlideLayout.BuildItem(item,settings);
            check(full.Count<top.Count && full[1].Lines.Count==6,"Full-slide mode did not use extra space");
            check(full.Skip(1).All(s=>s.Footer==""),"Credits appeared after title");
            var topPreview=SlideVisual.Create(top[1],true); var guide=topPreview.Children.OfType<Border>().Single(b=>b.Name=="PipFrame");
            check(guide.Width==512 && guide.Height==288 && Math.Abs(Canvas.GetLeft(guide)+guide.Width-1254.4)<.01 && Math.Abs(Canvas.GetTop(guide)+guide.Height-705.6)<.01 && guide.CornerRadius==new CornerRadius(0) && guide.BorderThickness==new Thickness(2),"Singer guide is not 40%, inset 2%, with square white borders");
            check(!SlideVisual.Create(full[1],true).Children.OfType<Border>().Any(b=>b.Name=="PipFrame" && b.IsVisible),"Full-slide preview has a singer guide");
            check(SlideVisual.Create(full[1]).Children.OfType<TextBlock>().All(t=>t.FontWeight==FontWeight.Bold),"Bold preview lost weight");
            check(top.All(s=>SlideLayout.TextTop(s)+s.Lines.Count*SlideLayout.LineSpacing(s.FontSize,s.FontName,s.Bold)<=360),"Top-half text spills below half");
            var path=Path.Combine(root,"full-slide-bold.pptx"); PptxWriter.Write(path,full,"全畫面");
            using(var doc=PresentationDocument.Open(path,false)) check(!new OpenXmlValidator().Validate(doc).Any(),"Full-slide deck is invalid");
            XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main",p="http://schemas.openxmlformats.org/presentationml/2006/main";
            using var zip=ZipFile.OpenRead(path);
            using var input=zip.GetEntry("ppt/slides/slide2.xml").Open(); var xml=XDocument.Load(input);
            check(xml.Descendants(p+"sp").Where(s=>(string)s.Element(p+"nvSpPr").Element(p+"cNvPr").Attribute("name")!="Section tag").SelectMany(s=>s.Descendants(a+"rPr")).All(r=>(string)r.Attribute("b")=="1"),"Bold not exported");
            check(xml.Descendants(p+"pic").Count()==1 && !xml.ToString().Contains("攝影機"),"Guide entered PowerPoint");
            var bottoms=xml.Descendants(a+"xfrm").Select(t=>(long)t.Element(a+"off").Attribute("y")+(long)t.Element(a+"ext").Attribute("cy")).ToList();
            check(bottoms.Skip(1).Any(y=>y>360*9525) && bottoms.Skip(1).All(y=>y<=720*9525),"Full slide text bounds wrong");
            foreach(var font in FontCatalog.Bundled) {
                var text=string.Concat(Enumerable.Repeat("祢的恩典與慈愛永遠長存",8));
                var wrapped=SlideLayout.Wrap(text,font.Name,72,true);
                check(string.Concat(wrapped)==text && wrapped.All(l=>SlideLayout.Measure(l,96,font.Name,true)<=SlideLayout.TextWidth),"Bold Chinese wrapping overflow");
            }
        });
        test("Image search requires Christian scope and subject with bilingual OR alternatives",()=> {
            var uri=ImageSearchLink.Create("屈膝禱告 / kneeling prayer、柔和光線 / soft light、雙手合十 / praying hands、寧靜 / peaceful"); var decoded=Uri.UnescapeDataString(uri.Query);
            check(decoded.Contains("(Christian OR Bible OR 基督教 OR 聖經 OR 圣经) (\"屈膝禱告\" OR \"屈膝祷告\" OR \"kneeling prayer\") ("),"Christian scope or required bilingual subject missing");
            check(decoded.Contains("\"soft light\"") && decoded.Contains("(background OR 背景)") && decoded.Contains("imgsz=2mp") && decoded.Contains("imgar=w") && !decoded.Contains("1280x720") && !decoded.Contains("&lr=") && !decoded.Contains("&hl="),"Details, image filters or language scope incorrect");
            check(Uri.UnescapeDataString(ImageSearchLink.Create("prayer, soft light OR prayer").Query).Contains("(\"prayer\") (\"soft light\")"),"Subject became optional or duplicate terms remain");
            check(Uri.UnescapeDataString(ImageSearchLink.Create("prayer OR worship").Query).Contains("(\"prayer\" OR \"worship\")"),"Explicit subject alternatives lost");
            check(!Uri.UnescapeDataString(ImageSearchLink.Create("crown").Query).Contains("()"),"Single subject produced empty modifier group");
        });
    }
}
