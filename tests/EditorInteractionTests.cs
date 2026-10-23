using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using SkiaSharp;

namespace WorshipPptx.Tests;

static class EditorInteractionTests
{
    private const string LongChorus="讚美歸於主榮耀聖名，讚美歸於主榮耀聖名\n讚美祂名永不停息，直到永恆我歌唱讚美";
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static ServiceItem Item(MainWindow window) => (ServiceItem)((TabItem)window.FindControl<TabControl>("SongTabs").SelectedItem).Tag;
    private static Point At(Control c,Window w,double x,double y) => c.TranslatePoint(new Point(x,y),w).Value;
    private static void Press(Window window,Key key) { window.KeyPress(key,RawInputModifiers.None,PhysicalKey.None,null); window.KeyRelease(key,RawInputModifiers.None,PhysicalKey.None,null); Pump(); }
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Auto fits both lyric line width and block height without adding line breaks in preview or export",()=> {
            var output=new System.Collections.Generic.List<SlideData>();
            var lines=LongChorus.Split('\n');
            foreach(var font in FontCatalog.All.Where(f=>f.IsBundled || f.Name=="Microsoft YaHei")) foreach(var bold in new[]{false,true}) foreach(var full in new[]{false,true}) {
                var item=new ServiceItem { Title="讚美",Lyrics="[v1]\n平安\n[c]\n"+LongChorus,Order="v1 c c",Formatting=new() { FontName=font.Name,Bold=bold,FullSlide=full,AutoFontSize=true } };
                var slides=SlideLayout.BuildItem(item,new UserSettings());
                var chorus=slides[2];
                check(slides.Count==4 && chorus.Lines.SequenceEqual(lines),"Auto wrapped an authored chorus line: "+font.Name+" / "+chorus.FontSize+" pt");
                check(slides.All(s=>s.FontSize==chorus.FontSize),"The song does not use one common size");
                check(chorus.Lines.All(l=>SlideLayout.Measure(l,SlideLayout.FontPixels(chorus.FontSize),font.Name,bold)<=SlideLayout.TextWidth-24),"An unwrapped line exceeds the safe text width");
                check(SlideLayout.TextTop(chorus)+chorus.Lines.Count*SlideLayout.LineSpacing(chorus.FontSize,font.Name,bold)<=SlideLayout.FooterTop(chorus),"Unwrapped block overflows vertically");
                var larger=chorus.FontSize+1;
                check(larger>72 || lines.Any(l=>SlideLayout.Measure(l,SlideLayout.FontPixels(larger),font.Name,bold)>SlideLayout.TextWidth-24),"Auto shrank the two-line block more than needed");
                check(SlideVisual.Create(chorus).Children.OfType<TextBlock>().Select(t=>t.Text).SequenceEqual(lines),"Preview changed the authored lines");
                output.Add(chorus);
                if(font.Name=="Microsoft YaHei" && !bold && !full) Console.WriteLine("  Screenshot chorus fits at "+chorus.FontSize+" pt with Microsoft YaHei");
            }
            var path=Path.Combine(root,"unwrapped-auto-lyrics.pptx"); PptxWriter.Write(path,output,"Auto line fitting");
            using var zip=ZipFile.OpenRead(path); XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";
            for(var i=0;i<output.Count;i++) {
                using var input=zip.GetEntry($"ppt/slides/slide{i+1}.xml").Open();
                var xml=XDocument.Load(input);
                check(xml.Descendants(a+"t").Select(t=>t.Value).SequenceEqual(lines.Append(SlideVisual.TagLabel(output[i]))),"PowerPoint changed the authored lines");
                check(xml.Descendants(a+"rPr").Take(lines.Length).All(r=>(int)r.Attribute("sz")==output[i].FontSize*100),"PowerPoint font differs from the fitted preview");
            }
        });
        test("Typing and pasting fit the longest block with a shared centered font until a saved manual override",()=> {
            var store=new LocalStore(Path.Combine(root,"auto-size-"+Guid.NewGuid().ToString("N")));
            var plan=Demo.Create(); plan.Items[1].Lyrics="[v1]\n平安\n[c]\n恩典"; plan.Items[1].Order="v1 c v1"; store.Save(plan);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var item=Item(window); var picker=window.FindControl<ComboBox>("FontSizePicker"); var thumbs=window.FindControl<ListBox>("Thumbnails");
                var lyrics=window.FindControl<TextBox>("Lyrics"); thumbs.SelectedIndex=1; Pump();
                check(item.Formatting.AutoFontSize && picker.SelectedItem is double initial && initial==72,"New song does not start in automatic sizing");
                check(picker.Items.Cast<object>().All(value=>value is double) && picker.Items.Cast<double>().Distinct().Count()==picker.ItemCount,"Font-size options are not unique numbers");
                void WaitFor(Func<bool> done) {
                    var frame=new DispatcherFrame();
                    using var timeout=new System.Threading.CancellationTokenSource(4000);
                    using var registration=timeout.Token.Register(()=>Dispatcher.UIThread.Post(()=>frame.Continue=false));
                    var poll=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(25) };
                    poll.Tick+=(_,_)=> { if(done()) frame.Continue=false; };
                    poll.Start(); try { Dispatcher.UIThread.PushFrame(frame); } finally { poll.Stop(); }
                    check(done(),"Live preview did not finish reflowing: "+picker.SelectedItem+" / "+window.FindControl<TextBlock>("ValidationText").Text);
                }
                var longer=string.Join("\n",Enumerable.Repeat("  平安與恩典充滿心間",8));
                lyrics.Text="[v1]\n平安\n[c]\n"+LongChorus;
                WaitFor(()=>picker.SelectedItem is double size && size<72);
                var chorusSlides=SlideLayout.BuildItem(item,store.LoadSettings());
                check(chorusSlides[2].Lines.SequenceEqual(LongChorus.Split('\n')) && picker.SelectedItem is double chorusSize && chorusSize==chorusSlides[2].FontSize,"Live Auto added a line break or left the size dropdown stale");
                lyrics.Text="[v1]\n"+longer+"\n[c]\n恩典";
                WaitFor(()=>picker.SelectedItem is double size && size<chorusSlides[2].FontSize);
                var slides=SlideLayout.BuildItem(item,store.LoadSettings());
                check(slides.Count==4 && slides.All(s=>s.FontSize==slides[1].FontSize && s.Alignment=="Center" && s.TextPosition=="Middle") && slides[1].Text==longer,"Longest block does not set a shared centered font, or whitespace was lost");
                lyrics.Text="[v1]\n平安\n[c]\n恩典";
                WaitFor(()=>picker.SelectedItem is double size && size==72);
                picker.SelectedItem=48.0; Pump();
                check(!item.Formatting.AutoFontSize && item.Formatting.FontSize==48,"Explicit size did not disable automatic sizing");
                lyrics.Text="[v1]\n"+longer+"\n[c]\n恩典";
                WaitFor(()=>thumbs.ItemCount>4);
                slides=SlideLayout.BuildItem(item,store.LoadSettings());
                check(slides.Where(s=>!s.IsTitle).All(s=>s.FontSize==48) && picker.SelectedItem is double n && n==48,"Typing overwrote the manual size");
                var tabs=window.FindControl<TabControl>("SongTabs"); tabs.SelectedIndex=2; Pump();
                check(Item(window).Formatting.AutoFontSize && picker.SelectedItem is double,"Manual override leaked to another tab");
                tabs.SelectedIndex=1; Pump(); check(picker.SelectedItem is double v && v==48,"Manual override lost after switching tabs");
                var automatic=window.FindControl<ToggleButton>("AutoFontSizeButton");
                check(automatic.IsChecked==false,"Manual size still shows Auto active");
                automatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                slides=SlideLayout.BuildItem(item,store.LoadSettings());
                check(item.Formatting.AutoFontSize && automatic.IsChecked==true && slides.Count==4 && slides.All(s=>s.FontSize==slides[1].FontSize) && picker.SelectedItem is double fitted && fitted==slides[1].FontSize,"Auto did not restore one shared fitted size");
                check(store.LoadHistory().Single().Items[1].Formatting.AutoFontSize,"Restored Auto mode was not saved");
                window.FindControl<TextBox>("SongOrder").Text="";
                lyrics.Text+="\n[v2]\n"+string.Join("\n",Enumerable.Repeat("更長的新段落",12));
                var before=slides[1].FontSize;
                WaitFor(()=>picker.SelectedItem is double size && size<before && thumbs.ItemCount==4);
                slides=SlideLayout.BuildItem(item,store.LoadSettings());
                check(slides.All(s=>s.FontSize==slides[1].FontSize),"A new block did not update the shared size");
                lyrics.Text="[v1]\n平安\n[c]\n恩典";
                WaitFor(()=>picker.SelectedItem is double size && size==72);
                automatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
                check(item.Formatting.AutoFontSize && automatic.IsChecked==true,"Clicking active Auto disabled live sizing");
                picker.SelectedItem=40.0; Pump();
                check(!item.Formatting.AutoFontSize && automatic.IsChecked==false,"An explicit size did not leave Auto mode");
            } finally { window.Close(); }
            var saved=store.LoadHistory().Single().Items;
            check(!saved[1].Formatting.AutoFontSize && saved[1].Formatting.FontSize==40 && saved[2].Formatting.AutoFontSize,"Automatic/manual modes did not persist per tab");
        });
        test("Escape leaves fields; Page keys change services, arrows change tabs/slides and Tab follows fields",()=> {
            var store=new LocalStore(Path.Combine(root,"editor-keys-"+Guid.NewGuid().ToString("N")));
            var first=Demo.Create(); store.Save(first); var second=Demo.Create(); second.Date=first.Date.AddDays(7); store.Save(second);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var title=window.FindControl<TextBox>("SongTitle"); var tabs=window.FindControl<TabControl>("SongTabs");
                var thumbs=window.FindControl<ListBox>("Thumbnails"); var history=window.FindControl<ListBox>("HistoryList");
                title.Focus(); var text=title.Text; var index=tabs.SelectedIndex; Press(window,Key.Left);
                check(tabs.SelectedIndex==index,"Arrow stole input focus");
                Press(window,Key.Escape); check(ReferenceEquals(window.FocusManager.GetFocusedElement(),window) && title.Text==text,"Escape changed text or failed to focus main window");
                Press(window,Key.Right); check(tabs.SelectedIndex==(index+1)%5,"Right did not select next real tab");
                Press(window,Key.Left); check(tabs.SelectedIndex==index,"Left did not select previous tab");
                var slide=thumbs.SelectedIndex; Press(window,Key.Down); check(thumbs.SelectedIndex==slide+1,"Down did not change thumbnail");
                Press(window,Key.Up); check(thumbs.SelectedIndex==slide,"Up did not change thumbnail");
                var service=((ServicePlan)history.SelectedItem).Id; Press(window,Key.PageDown);
                check(((ServicePlan)history.SelectedItem).Id!=service,"Page Down did not switch service");
                Press(window,Key.PageUp); check(((ServicePlan)history.SelectedItem).Id==service,"Page Up did not switch back");
                Press(window,Key.Tab); check(title.IsFocused,"First Tab did not start at song name");
                foreach(var name in new[]{"CopyrightText","SongId","SongOrder","Lyrics","ServiceTitle"}) {
                    Press(window,Key.Tab); check((name=="ServiceTitle" ? window.ActiveServiceField<TextBox>(name) : window.FindControl<TextBox>(name)).IsFocused,"Unexpected tab order at "+name);
                }
                var language=window.FindControl<StackPanel>("LanguagePanel"); var toggle=window.FindControl<ToggleButton>("TraditionalButton");
                check(language.TranslatePoint(default,window).Value.X>window.Bounds.Width-230 && toggle.FontSize>=18 && toggle.VerticalContentAlignment==Avalonia.Layout.VerticalAlignment.Center && toggle.HorizontalContentAlignment==Avalonia.Layout.HorizontalAlignment.Center,"Language switch is not centered at upper right");
                var settings=window.FindControl<Button>("SettingsButton");
                check(settings.TranslatePoint(default,window).Value.X>=language.TranslatePoint(new Point(language.Bounds.Width,0),window).Value.X && settings.Content is PathIcon,"Settings is not an icon at the far right");
                var version=window.FindControl<TextBlock>("VersionLabel");
                check(version.Text=="v"+typeof(MainWindow).Assembly.GetName().Version.ToString(3) && version.TranslatePoint(default,window).Value.X<178 && version.TranslatePoint(default,window).Value.Y<100,"Version is not under the logo or does not match build");
                var bold=window.FindControl<ToggleButton>("BoldToggle");
                check(bold.Padding==new Thickness(0) && bold.HorizontalContentAlignment==Avalonia.Layout.HorizontalAlignment.Center && bold.VerticalContentAlignment==Avalonia.Layout.VerticalAlignment.Center,"Bold button is not centered");
                var chip=thumbs.GetVisualDescendants().OfType<Border>().First(b=>b.Name=="ThumbnailTag");
                check(chip.Parent is Grid && chip.HorizontalAlignment==Avalonia.Layout.HorizontalAlignment.Left && chip.VerticalAlignment==Avalonia.Layout.VerticalAlignment.Bottom && ((TextBlock)chip.Child).Text.Contains('['),"Thumbnail label is not inside the bottom left of image");
                var scrollBar=thumbs.GetVisualDescendants().OfType<ScrollBar>().Single(b=>b.Orientation==Avalonia.Layout.Orientation.Vertical);
                var barLeft=scrollBar.TranslatePoint(default,thumbs).Value.X;
                check(scrollBar.IsVisible && thumbs.GetVisualDescendants().OfType<ListBoxItem>().All(c=>c.TranslatePoint(new Point(c.Bounds.Width,0),thumbs).Value.X+7<=barLeft),"Thumbnail cards overlap the scrollbar or lack a gap");
            } finally { window.Close(); }
        });
        test("Both previews pan and zoom the same background while the camera stays fixed; changes persist per tab",()=> {
            var store=new LocalStore(Path.Combine(root,"editor-image-"+Guid.NewGuid().ToString("N")));
            var plan=Demo.Create(); plan.Items[1].Preview=new PreviewLayout { X=2,Y=2,BackgroundSource="Camera" }; store.Save(plan);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var item=Item(window); var preview=(CompositionPreview)window.FindControl<Viewbox>("SlideHost").Child;
                var toolbar=window.FindControl<Border>("ImageControls");
                var backgroundBox=window.FindControl<Border>("BackgroundBox");
                var background=(CompositionPreview)window.FindControl<Viewbox>("BackgroundHost").Child;
                check(toolbar.GetVisualAncestors().Contains(backgroundBox),"Image controls are not on the top background preview");
                check(!window.FindControl<Viewbox>("SlideHost").GetVisualDescendants().OfType<Button>().Any(),"Projection contains editing controls");
                var opacityControl=window.FindControl<Slider>("BackgroundOpacity");
                var knob=opacityControl.GetVisualDescendants().OfType<Thumb>().First();
                var knobTop=knob.TranslatePoint(default,opacityControl).Value.Y;
                check(knobTop>=0 && knobTop+knob.Bounds.Height<=opacityControl.Bounds.Height+1,"Opacity knob is clipped");
                check(Math.Abs((100-item.Preview.X-item.Preview.ScaleX)*1280-(100-item.Preview.Y-item.Preview.ScaleY)*720)<.01 && item.Preview.Y==58 && item.Preview.BackgroundSource=="Slide" && window.FindControl<ToggleButton>("CompositionToggle")==null,"Fixed PiP does not have equal right and bottom margins");
                check(!preview.Children.OfType<Border>().Any(b=>b.Name?.StartsWith("PipHandle")==true),"PiP resize handles remain");
                void Drag(CompositionPreview target,Point from,Point to) {
                    window.MouseDown(At(target,window,from.X,from.Y),MouseButton.Left); Pump();
                    window.MouseMove(At(target,window,to.X,to.Y),RawInputModifiers.LeftMouseButton); Pump();
                    window.MouseUp(At(target,window,to.X,to.Y),MouseButton.Left); Pump();
                }
                Drag(preview,new Point(1000,500),new Point(800,400));
                check(item.Preview.X==PreviewLayout.DefaultX && item.BackgroundTransform.X==0,"Dragging camera moved camera or background");
                window.MouseWheel(At(preview,window,500,250),new Vector(0,3)); Pump();
                check(item.BackgroundTransform.Zoom>1,"Main preview wheel did not zoom image");
                var beforePan=item.BackgroundTransform.X;
                Drag(preview,new Point(500,250),new Point(580,280));
                check(item.BackgroundTransform.X>beforePan && item.Preview.X==PreviewLayout.DefaultX,"Main preview drag did not pan background independently");
                void CheckSynced() {
                    foreach(var target in new[]{preview,background}) {
                        var image=(Image)((Canvas)target.Children.OfType<Viewbox>().Single(v=>v.Name=="CompositionBackground").Child).Children[0];
                        check(Math.Abs(image.Width-1280*item.BackgroundTransform.Zoom)<.01 && Math.Abs(Canvas.GetLeft(image)-((1280-image.Width)/2+item.BackgroundTransform.X*12.8))<.01,"Preview framing is out of sync");
                    }
                }
                CheckSynced();
                var beforeCamera=item.BackgroundTransform.Key;
                window.MouseWheel(At(preview,window,1000,500),new Vector(0,3)); Pump();
                Drag(preview,new Point(1000,500),new Point(800,400));
                check(item.BackgroundTransform.Key==beforeCamera,"Camera gestures changed background framing");
                window.MouseDown(At(preview,window,500,250),MouseButton.Left); Pump();
                window.MouseMove(At(preview,window,540,270),RawInputModifiers.LeftMouseButton); Pump();
                Press(window,Key.Escape);
                window.MouseUp(At(preview,window,540,270),MouseButton.Left); Pump();
                check(!preview.IsDragging && item.BackgroundTransform.Key==beforeCamera,"Escape did not cancel the main preview pan");
                CheckSynced();
                var anchor=At(background,window,900,100); window.MouseWheel(anchor,new Vector(0,3)); Pump();
                check(item.BackgroundTransform.Zoom>1,"Top image wheel did not zoom image");
                var x=item.BackgroundTransform.X; Drag(background,new Point(900,100),new Point(1000,180));
                check(item.BackgroundTransform.X>x && item.Preview.X==PreviewLayout.DefaultX,"Image pan did not move background independently");
                CheckSynced();
                void Click(string name) { window.FindControl<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
                var zoom=item.BackgroundTransform.Zoom; Click("ZoomIn"); check(item.BackgroundTransform.Zoom>zoom,"Map plus did not zoom"); Click("ZoomOut");
                x=item.BackgroundTransform.X; var y=item.BackgroundTransform.Y;
                Click("PanLeft"); check(item.BackgroundTransform.X<x,"Map left did not pan"); Click("PanRight");
                Click("PanUp"); check(item.BackgroundTransform.Y<y,"Map up did not pan"); Click("PanDown");
                check(Math.Abs(item.BackgroundTransform.X-x)<.01 && Math.Abs(item.BackgroundTransform.Y-y)<.01,"Opposite arrows do not cancel");
                var flip=window.FindControl<ToggleButton>("FlipBackground"); flip.IsChecked=true; flip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.FindControl<Slider>("BackgroundOpacity").Value=45; Pump();
                check(item.BackgroundTransform.Flip && item.BackgroundTransform.Opacity==.45,"Opacity/flip not captured");
                var tabs=window.FindControl<TabControl>("SongTabs"); tabs.SelectedIndex=2; Pump();
                check(Item(window).BackgroundTransform.Zoom==1 && !flip.IsChecked.Value,"Image edits leaked between songs");
                tabs.SelectedIndex=1; Pump();
                check(flip.IsChecked==true && window.FindControl<Slider>("BackgroundOpacity").Value==45,"Image edits lost on return");
            } finally { window.Close(); }
            var saved=store.LoadHistory().Single().Items[1];
            check(saved.BackgroundTransform.Zoom>1 && saved.BackgroundTransform.Flip && saved.BackgroundTransform.Opacity==.45,"Image adjustments not saved");
        });
        test("Adjusted background pixels match export and section tags are padded inside the PiP footprint",()=> {
            var slide=new SlideData { Background="",Section="V1",BackgroundTransform=new() { Zoom=1.6,X=12,Y=-8,Flip=true,Opacity=.45 },Lines=new(){"平安"} };
            var visual=SlideVisual.Create(slide); var tag=visual.Children.OfType<Border>().Single(b=>b.Name=="SlideTag");
            check(Canvas.GetLeft(tag)>=PreviewLayout.DefaultX*12.8 && Canvas.GetLeft(tag)+tag.Width<(PreviewLayout.DefaultX+40)*12.8 && Canvas.GetTop(tag)>=58*7.2 && Canvas.GetTop(tag)+tag.Height<98*7.2,"Slide tag lies outside default PiP");
            var bare=SlideVisual.Create(new SlideData { BackgroundTransform=slide.BackgroundTransform }); bare.Measure(new Size(1280,720)); bare.Arrange(new Rect(0,0,1280,720));
            using var rendered=new RenderTargetBitmap(new PixelSize(1280,720),new Vector(96,96)); rendered.Render(bare);
            using var png=new MemoryStream(); rendered.Save(png,new PngBitmapEncoderOptions()); using var actual=SKBitmap.Decode(png.ToArray()); using var expected=SKBitmap.Decode(SlideVisual.BackgroundPng("",slide.BackgroundTransform));
            foreach(var point in new[]{new SKPointI(100,100),new SKPointI(640,360),new SKPointI(1150,640)}) {
                var a=actual.GetPixel(point.X,point.Y); var b=expected.GetPixel(point.X,point.Y);
                check(Math.Abs(a.Red-b.Red)<4 && Math.Abs(a.Green-b.Green)<4 && Math.Abs(a.Blue-b.Blue)<4,"Background preview/export pixels disagree");
            }
            var path=Path.Combine(root,"adjusted-background.pptx"); System.Threading.Tasks.Task.Run(()=>PptxWriter.Write(path,new[]{slide,new SlideData { Background="",Section="C" }},"Background edits")).GetAwaiter().GetResult();
            using(var doc=PresentationDocument.Open(path,false)) check(!new OpenXmlValidator().Validate(doc).Any(),"Adjusted PPTX is invalid");
            using var zip=ZipFile.OpenRead(path); check(zip.Entries.Count(e=>e.FullName.StartsWith("ppt/media/"))==2,"Different transforms incorrectly reused same image");
            XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",aNs="http://schemas.openxmlformats.org/drawingml/2006/main";
            using var stream=zip.GetEntry("ppt/slides/slide1.xml").Open(); var xml=XDocument.Load(stream);
            var shape=xml.Descendants(p+"sp").Single(s=>(string)s.Element(p+"nvSpPr").Element(p+"cNvPr").Attribute("name")=="Section tag");
            check(shape.Descendants(aNs+"t").Single().Value=="[V1]" && shape.Element(p+"spPr").Element(aNs+"solidFill").Element(aNs+"srgbClr").Attribute("val").Value=="ECEEEC","Exported section tag missing or not gray");
            check(xml.Descendants(p+"pic").Count()==1,"Camera illustration exported");
        });
    }
}
