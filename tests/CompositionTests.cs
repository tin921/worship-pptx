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
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WorshipPptx.Tests;

static class CompositionTests
{
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static ToggleButton View(Window window,string name) => window.GetVisualDescendants().OfType<ToggleButton>().Single(b=>b.Name=="View"+name);
    private static void Click(Window window,string name) { window.FindControl<ToggleButton>("CompositionToggle").IsChecked=true; Pump(); View(window,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(); }
    private static ServiceItem Item(MainWindow window) => (ServiceItem)((TabItem)window.FindControl<TabControl>("SongTabs").SelectedItem).Tag;
    private static Point PointIn(Control control,Window window,double x,double y) => control.TranslatePoint(new Point(x,y),window).Value;
    private static void Drag(Window window,CompositionPreview editor,Point from,Point to)
    {
        window.MouseDown(PointIn(editor,window,from.X,from.Y),MouseButton.Left); Pump();
        window.MouseMove(PointIn(editor,window,to.X,to.Y),RawInputModifiers.LeftMouseButton); Pump();
        window.MouseUp(PointIn(editor,window,to.X,to.Y),MouseButton.Left); Pump();
    }
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Six views use inset corners, opposite text halves and independent source selection",()=> {
            var item=new ServiceItem { Title="版面測試",Lyrics="[v1]\n恩典與慈愛\n永遠跟隨我",Order="v1",Preview=new PreviewLayout() };
            foreach(var corner in new[]{"TopLeft","TopRight","BottomLeft","BottomRight"}) {
                item.Preview.ApplyPreset(corner);
                check(item.Preview.X==(corner.EndsWith("Left") ? 2 : 58) && item.Preview.Y==(corner.StartsWith("Top") ? 2 : 58),"Corner margin is not 2%");
                var slides=SlideLayout.BuildItem(item,new UserSettings());
                check(slides.All(s=>!s.FullSlide && s.BottomHalf==corner.StartsWith("Top")),"Camera corner did not select opposite text half");
                foreach(var slide in slides) {
                    var textTop=SlideLayout.TextTop(slide); var bottom=textTop+slide.Lines.Count*SlideLayout.LineSpacing(slide.FontSize,slide.FontName,slide.Bold);
                    check(corner.StartsWith("Top") ? textTop>=360 && bottom<=720 : textTop>=0 && bottom<=360,"Text crossed its allowed half");
                }
                item.Preview.BackgroundSource="Camera";
                check(SlideLayout.BuildItem(item,new UserSettings()).All(s=>s.FullSlide && !s.BottomHalf),"Slide inside PiP is not full-width/full-height text area");
                item.Preview.BackgroundSource="Slide";
            }
            foreach(var preset in new[]{"FullSlide","FullCamera"}) {
                item.Preview.ApplyPreset(preset);
                var slide=SlideLayout.BuildItem(item,new UserSettings())[0]; var visual=SlideVisual.Create(slide,true);
                check(slide.FullSlide && !item.Preview.IsPip && !visual.Children.OfType<Border>().Any(b=>b.Name=="PipFrame" && b.IsVisible),"Full mode has a PiP or half-page text");
                check((string)visual.Children.OfType<Viewbox>().Single().Tag==(preset=="FullCamera" ? "Camera" : "Slide"),"Wrong full-screen source");
            }
        });
        test("PiP move and all eight resize edges work through a scaled Viewbox and stay bounded",()=> {
            var store=new LocalStore(Path.Combine(root,"composition-drag-"+Guid.NewGuid().ToString("N"))); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var item=Item(window); var controls=window.FindControl<CompositionControls>("CompositionOptions");
                var editor=(CompositionPreview)window.FindControl<Viewbox>("SlideHost").Child;
                check(editor.Bounds.Width==1280 && window.FindControl<Viewbox>("SlideHost").Bounds.Width<1280,"Test is not using a scaled preview");
                Drag(window,editor,new Point(998.4,561.6),new Point(230.4,129.6));
                check(Math.Abs(item.Preview.X)<.05 && Math.Abs(item.Preview.Y)<.05,"Drag did not follow the pointer / clamp at top left");
                check(item.Preview.TextArea=="Bottom" && controls.PositionX.Value==0 && controls.PositionY.Value==0,"Drag did not update fields/text half immediately");
                check(ReferenceEquals(editor,window.FindControl<Viewbox>("SlideHost").Child),"Changing text halves replaced the captured editor");
                var edges=new[]{new Point(0,0),new Point(.5,0),new Point(1,0),new Point(1,.5),new Point(1,1),new Point(.5,1),new Point(0,1),new Point(0,.5)};
                foreach(var edge in edges) {
                    Click(window,"BottomRight");
                    var beforeX=item.Preview.X; var beforeY=item.Preview.Y;
                    var from=new Point((58+40*edge.X)*12.8,(58+40*edge.Y)*7.2);
                    var to=from+new Vector(edge.X==.5 ? 0 : edge.X==0 ? 128 : -128,edge.Y==.5 ? 0 : edge.Y==0 ? 72 : -72);
                    Drag(window,editor,from,to);
                    check(Math.Abs(item.Preview.ScaleX-(edge.X==.5 ? 40 : 30))<.1 && Math.Abs(item.Preview.ScaleY-(edge.Y==.5 ? 40 : 30))<.1,"Resize failed at "+edge);
                    check(Math.Abs(item.Preview.X-(edge.X==0 ? 68 : beforeX))<.1 && Math.Abs(item.Preview.Y-(edge.Y==0 ? 68 : beforeY))<.1,"Opposite edge moved at "+edge);
                }
                Click(window,"TopLeft");
                Drag(window,editor,new Point(25.6,14.4),new Point(-1280,-720));
                check(item.Preview.X==0 && item.Preview.Y==0 && item.Preview.ScaleX<=100 && item.Preview.ScaleY<=100,"Resize escaped canvas");
                var frame=editor.Children.OfType<Border>().Single(b=>b.Name=="PipFrame");
                var handles=editor.Children.OfType<Border>().Where(b=>b.Name?.StartsWith("PipHandle")==true && b.IsVisible).ToArray();
                check(frame.CornerRadius==new CornerRadius(0) && ((ISolidColorBrush)frame.BorderBrush).Color==Colors.White && frame.BorderThickness.Left>0 && handles.Length==4 && handles.All(h=>h.CornerRadius==new CornerRadius(0) && ((ISolidColorBrush)h.Background).Color==Colors.White && ((ISolidColorBrush)h.BorderBrush).Color==Colors.White),"White outline and four white corner handles missing");
            } finally { window.Close(); }
            var saved=store.LoadHistory().Single().Items[1].Preview;
            check(saved.X==0 && saved.Y==0,"Final drag position did not save");
        });
        test("Numeric PiP fields, focused wheel, source swaps and per-tab saving",()=> {
            var store=new LocalStore(Path.Combine(root,"composition-fields-"+Guid.NewGuid().ToString("N"))); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var controls=window.FindControl<CompositionControls>("CompositionOptions"); var tabs=window.FindControl<TabControl>("SongTabs"); var item=Item(window);
                window.FindControl<ToggleButton>("CompositionToggle").IsChecked=true; Pump();
                controls.PositionX.Value=12; controls.PositionY.Value=15; controls.ScaleX.Value=32; controls.ScaleY.Value=24; Pump();
                check(item.Preview.X==12 && item.Preview.Y==15 && item.Preview.ScaleX==32 && item.Preview.ScaleY==24,"Number fields did not update layout");
                var frame=((CompositionPreview)window.FindControl<Viewbox>("SlideHost").Child).Children.OfType<Border>().Single(b=>b.Name=="PipFrame");
                check(Math.Abs(frame.Width-409.6)<.01 && Math.Abs(frame.Height-172.8)<.01,"X/Y scales were not independent");
                window.FindControl<TextBox>("SongTitle").Focus(); Pump();
                var wheelPoint=PointIn(controls.PositionX,window,15,15);
                window.MouseWheel(wheelPoint,new Vector(0,1)); Pump();
                check(controls.PositionX.Value==12,"Hover wheel changed unfocused field");
                var text=controls.PositionX.GetVisualDescendants().OfType<TextBox>().Single(); text.Focus(); Pump();
                window.MouseWheel(wheelPoint,new Vector(0,1)); Pump(); check(item.Preview.X==13,"Focused wheel up did not increase number");
                window.MouseWheel(wheelPoint,new Vector(0,-1)); Pump(); check(item.Preview.X==12,"Focused wheel down did not decrease number");
                text.Text="18"; Pump(); text.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter }); Pump();
                check(item.Preview.X==18,"Typing a position did not update layout");
                controls.BackgroundSource.SelectedIndex=1; Pump();
                check(item.Preview.BackgroundSource=="Camera" && controls.PipSource.SelectedIndex==0 && SlideLayout.BuildItem(item,new UserSettings()).All(s=>s.FullSlide),"Camera background / slide PiP failed");
                controls.PipSource.SelectedIndex=1; Pump();
                check(controls.BackgroundSource.SelectedIndex==0 && item.Preview.BackgroundSource=="Slide","PiP source selection did not swap background");
                controls.ScaleX.Value=100; Pump(); check(item.Preview.X==0 && controls.PositionX.Value==0,"Oversized PiP did not clamp position");
                controls.ScaleX.Value=32; controls.PositionX.Value=18; controls.BackgroundSource.SelectedIndex=1; Pump();
                tabs.SelectedIndex=2; Pump(); check(controls.PositionX.Value==58 && controls.ScaleX.Value==40 && controls.BackgroundSource.SelectedIndex==0,"Layout leaked across tabs");
                tabs.SelectedIndex=1; Pump(); check(controls.PositionX.Value==18 && controls.ScaleX.Value==32 && controls.BackgroundSource.SelectedIndex==1,"Tab layout was lost");
                Click(window,"FullCamera"); check(!controls.PositionX.IsEnabled && !controls.PipSource.IsEnabled,"Full-screen view leaves PiP controls active");
                Click(window,"TopRight"); check(controls.PositionX.IsEnabled && item.Preview.BackgroundSource=="Camera","Returning to PiP lost chosen background");
            } finally { window.Close(); }
            var saved=store.LoadHistory().Single(); check(saved.Items[1].Preview.BackgroundSource=="Camera" && saved.Items[1].Preview.Y==2 && saved.Items[2].Preview.BackgroundSource=="Slide","Per-tab layout did not persist");
        });
        test("Sidebar sorts dates, greys past titles and updates tab names without saving",()=> {
            var store=new LocalStore(Path.Combine(root,"composition-history-"+Guid.NewGuid().ToString("N")));
            var past=Demo.Create(); past.Title="過去崇拜"; past.Date=DateTime.Today.AddDays(-7); store.Save(past);
            var future=Demo.Create(); future.Title="未來崇拜"; future.Date=DateTime.Today.AddDays(14); store.Save(future);
            var current=Demo.Create(); current.Title="本週崇拜"; current.Date=DateTime.Today; store.Save(current);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var history=window.FindControl<ListBox>("HistoryList");
                check(history.Items.Cast<ServicePlan>().Select(p=>p.Id).SequenceEqual(new[]{future.Id,current.Id,past.Id}),"History is sorted by edit time instead of service date");
                var pastTitle=history.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text=="過去崇拜");
                check(pastTitle.Classes.Contains("past-service") && ((ISolidColorBrush)pastTitle.Foreground).Color==Color.Parse("#8A918C"),"Past title is not grey");
                window.FindControl<TextBox>("SongTitle").Text="即時更新分頁"; Pump();
                check(history.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("即時更新分頁")==true),"Sidebar tab label waits for disk save");
                var plan=(ServicePlan)history.SelectedItem; var moved=plan.Items[1]; plan.MoveItem(moved,0); Pump();
                check(history.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.StartsWith("即時更新分頁\n")==true),"Sidebar tab order did not change live");
                window.FindControl<CalendarDatePicker>("ServiceDate").SelectedDate=DateTime.Today.AddDays(30); Pump();
                check(((ServicePlan)history.Items[0]).Id==current.Id && ((ServicePlan)history.SelectedItem).Id==current.Id,"Editing date did not re-sort / preserve selected service");
                window.FindControl<CalendarDatePicker>("ServiceDate").SelectedDate=DateTime.Today.AddDays(-1); Pump();
                check(history.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text=="本週崇拜").Classes.Contains("past-service"),"Date editing did not grey title immediately");
            } finally { window.Close(); }
        });
        test("Bottom-half text and credits export without exporting the camera illustration",()=> {
            var item=new ServiceItem { Title="下半部",Lyrics="[v1]\n平安喜樂",Order="v1",Copyright="作者",SongId="123",Preview=new PreviewLayout() };
            item.Preview.ApplyPreset("TopLeft"); var slides=SlideLayout.BuildItem(item,new UserSettings());
            var path=Path.Combine(root,"bottom-half-composition.pptx"); PptxWriter.Write(path,slides,"版面測試");
            using var zip=ZipFile.OpenRead(path); XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",a="http://schemas.openxmlformats.org/drawingml/2006/main";
            foreach(var entry in zip.Entries.Where(e=>e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))) {
                using var stream=entry.Open(); var xml=XDocument.Load(stream);
                check(xml.Descendants(p+"pic").Count()==1 && !xml.ToString().Contains("攝影機"),"PiP guide leaked into PowerPoint");
                foreach(var shape in xml.Descendants(p+"sp")) {
                    var transform=shape.Descendants(a+"xfrm").Single(); var y=(long)transform.Element(a+"off").Attribute("y"); var height=(long)transform.Element(a+"ext").Attribute("cy");
                    check(y>=360*9525 && y+height<=720*9525,"Export text or credits escaped lower half");
                }
            }
        });
        test("Composition controls fit the minimum preview width and window height",()=> {
            var store=new LocalStore(Path.Combine(root,"composition-small-"+Guid.NewGuid().ToString("N"))); store.Save(Demo.Create());
            var window=new MainWindow(store) { Width=1120,Height=760 }; window.Show(); Pump();
            try {
                var controls=window.FindControl<CompositionControls>("CompositionOptions");
                var toggle=window.FindControl<ToggleButton>("CompositionToggle");
                var host=window.FindControl<Viewbox>("SlideHost"); var collapsedHeight=host.Bounds.Height;
                check(toggle.IsChecked==false && !controls.IsVisible,"Composition controls should start collapsed");
                var togglePoint=PointIn(toggle,window,toggle.Bounds.Width/2,toggle.Bounds.Height/2);
                window.MouseDown(togglePoint,MouseButton.Left); window.MouseUp(togglePoint,MouseButton.Left); Pump();
                check(toggle.IsChecked==true && controls.IsVisible,"Layout toggle did not reveal controls");
                check(controls.Bounds.Width>=300,"Preview column is below minimum width");
                foreach(var c in controls.GetVisualDescendants().Where(c=>c is LayoutNumber || c is ComboBox || c is ToggleButton)) {
                    var pos=c.TranslatePoint(default,controls).Value;
                    check(pos.X>=-.1 && pos.X+c.Bounds.Width<=controls.Bounds.Width+.1 && pos.Y>=0 && pos.Y+c.Bounds.Height<=controls.Bounds.Height+.1,"Layout control clips: "+((Control)c).Name);
                }
                foreach(var number in controls.GetVisualDescendants().OfType<LayoutNumber>()) {
                    var text=number.GetVisualDescendants().OfType<TextBox>().Single();
                    check(text.Bounds.Height<=number.Bounds.Height+.1,"Numeric text input is taller than its border");
                }
                check(host.Bounds.Height>=90,"Preview collapsed at minimum window size: host="+host.Bounds+", controls="+controls.Bounds+", toolbar="+window.FindControl<Grid>("BackgroundToolbar").Bounds+", work="+window.FindControl<Grid>("WorkArea").Bounds);
                check(window.FindControl<ToggleButton>("FullSlideToggle")==null && window.FindControl<ToggleButton>("PipToggle")==null,"Old mode toggle remains");
                toggle.IsChecked=false; Pump();
                check(!controls.IsVisible && Math.Abs(host.Bounds.Height-collapsedHeight)<1,"Collapsing settings did not restore preview space");
                check(((CompositionPreview)host.Child).Children.OfType<Border>().Count(b=>b.Name?.StartsWith("PipHandle")==true && b.IsVisible)==4,"Collapsing settings hid the corner handles");
            } finally { window.Close(); }
        });
    }
}
