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
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static ServiceItem Item(MainWindow window) => (ServiceItem)((TabItem)window.FindControl<TabControl>("SongTabs").SelectedItem).Tag;
    private static Point At(Control c,Window w,double x,double y) => c.TranslatePoint(new Point(x,y),w).Value;
    private static void Press(Window window,Key key) { window.KeyPress(key,RawInputModifiers.None,PhysicalKey.None,null); window.KeyRelease(key,RawInputModifiers.None,PhysicalKey.None,null); Pump(); }
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
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
                    Press(window,Key.Tab); check(window.FindControl<TextBox>(name).IsFocused,"Unexpected tab order at "+name);
                }
                var language=window.FindControl<StackPanel>("LanguagePanel"); var toggle=window.FindControl<ToggleButton>("TraditionalButton");
                check(language.TranslatePoint(default,window).Value.X>window.Bounds.Width-170 && toggle.FontSize>=18 && toggle.VerticalContentAlignment==Avalonia.Layout.VerticalAlignment.Center && toggle.HorizontalContentAlignment==Avalonia.Layout.HorizontalAlignment.Center,"Language switch is not centered at upper right");
                var chip=thumbs.GetVisualDescendants().OfType<Border>().First(b=>b.Name=="ThumbnailTag");
                check(chip.Parent is Grid && chip.HorizontalAlignment==Avalonia.Layout.HorizontalAlignment.Right && chip.VerticalAlignment==Avalonia.Layout.VerticalAlignment.Bottom && ((TextBlock)chip.Child).Text.Contains('['),"Thumbnail label is outside image");
            } finally { window.Close(); }
        });
        test("Fixed PiP ignores drag; image pan/zoom, opacity and flip persist separately per tab",()=> {
            var store=new LocalStore(Path.Combine(root,"editor-image-"+Guid.NewGuid().ToString("N")));
            var plan=Demo.Create(); plan.Items[1].Preview=new PreviewLayout { X=2,Y=2,BackgroundSource="Camera" }; store.Save(plan);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var item=Item(window); var preview=(CompositionPreview)window.FindControl<Viewbox>("SlideHost").Child;
                var toolbar=window.FindControl<Border>("ImageControls");
                var toolbarBottom=toolbar.TranslatePoint(new Point(0,toolbar.Bounds.Height),window).Value.Y;
                var pictureBottom=preview.TranslatePoint(new Point(0,720),window).Value.Y;
                check(Math.Abs(toolbarBottom-(pictureBottom-10))<1,$"Image toolbar outside picture: toolbar={toolbarBottom}, picture={pictureBottom}, margin={toolbar.Margin}, host={window.FindControl<Viewbox>("SlideHost").Bounds}, parent={((Control)toolbar.Parent).Bounds}");
                check(item.Preview.X==58 && item.Preview.Y==58 && item.Preview.BackgroundSource=="Slide" && window.FindControl<ToggleButton>("CompositionToggle")==null,"PiP did not become fixed at default corner");
                check(!preview.Children.OfType<Border>().Any(b=>b.Name?.StartsWith("PipHandle")==true),"PiP resize handles remain");
                void Drag(Point from,Point to) {
                    window.MouseDown(At(preview,window,from.X,from.Y),MouseButton.Left); Pump();
                    window.MouseMove(At(preview,window,to.X,to.Y),RawInputModifiers.LeftMouseButton); Pump();
                    window.MouseUp(At(preview,window,to.X,to.Y),MouseButton.Left); Pump();
                }
                Drag(new Point(1000,500),new Point(800,400));
                check(item.Preview.X==58 && item.BackgroundTransform.X==0,"Dragging camera moved camera or background");
                var anchor=At(preview,window,500,250); window.MouseWheel(anchor,new Vector(0,3)); Pump();
                check(item.BackgroundTransform.Zoom>1,"Wheel did not zoom image");
                var x=item.BackgroundTransform.X; Drag(new Point(500,250),new Point(580,280));
                check(item.BackgroundTransform.X>x && item.Preview.X==58,"Image pan did not move background independently");
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
            check(Canvas.GetLeft(tag)>=58*12.8 && Canvas.GetLeft(tag)+tag.Width<98*12.8 && Canvas.GetTop(tag)>=58*7.2 && Canvas.GetTop(tag)+tag.Height<98*7.2,"Slide tag lies outside default PiP");
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
