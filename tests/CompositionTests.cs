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
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Camera artwork follows lyric sections, including songs with no repeat order",()=> {
            var settings=new UserSettings();
            foreach(var order in new[]{"v1 c", "", "  "}) {
                var song=new ServiceItem { Title="詩歌",Lyrics="[v1]\n恩典長存\n[c]\n讚美主",Order=order };
                var slides=SlideLayout.BuildItem(song,settings);
                check(slides[0].IsTitle && slides.All(s=>s.IsSong),"Song title or lyrics switched to reading because the order was empty");
                check(SlideLayout.BuildItem(song,settings,FontCatalog.DefaultName).All(s=>s.IsSong),"Explicit formatting overload lost song classification");
            }
            foreach(var item in new[]{
                new ServiceItem {Kind="scripture",Title="讀經",Lyrics="耶和華是我的牧者",Order=""},
                new ServiceItem {Title="宣召經文",Lyrics="普天下當向耶和華歡呼",Order="v1 c"},
                new ServiceItem {Title="公告",Lyrics="[1]\n會後團契\n[2]\n下週聚會",Order="v1 c"},
                new ServiceItem {Title="Announcement",Lyrics="Please meet [team] after the service.",Order=""}
            }) check(SlideLayout.BuildItem(item,settings).All(s=>!s.IsSong),"Spoken item selected the musician from stale order or inline brackets");
        });
        test("PiP and full camera previews switch between the musician and Bible reader",()=> {
            var settings=new UserSettings();
            var song=SlideLayout.BuildItem(new ServiceItem {Title="詩歌",Lyrics="[v1]\n讚美主",Order=""},settings)[0];
            var reading=SlideLayout.BuildItem(new ServiceItem {Title="讀經",Lyrics="耶和華是我的牧者",Order=""},settings)[0];
            var layout=new PreviewLayout(); var preview=new CompositionPreview(song,layout);
            var window=new Window {Width=640,Height=360,Content=new Viewbox {Child=preview}}; window.Show(); Pump();
            try {
                object music=null,reader=null;
                foreach(var background in new[]{"Slide","Camera"}) foreach(var slide in new[]{song,reading,song}) {
                    layout.BackgroundSource=background; preview.SetSlide(slide,layout); Pump();
                    var guide=preview.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="PresenterGuide");
                    check((string)guide.Tag==(slide.IsSong ? "Music" : "Reading"),"Camera artwork did not switch with the item/source");
                    var source=guide.GetVisualDescendants().OfType<Image>().Single().Source;
                    if(slide.IsSong) music=source; else reader=source;
                }
                check(music!=null && reader!=null && !ReferenceEquals(music,reader),"Both modes display the same image asset");
                layout.ApplyPreset("FullCamera"); preview.SetSlide(reading,layout); Pump();
                check((string)preview.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="PresenterGuide").Tag=="Reading","Full-screen camera uses the musician for scripture");
            } finally {window.Close();}
        });
        test("Legacy layout data still supports its recorded text regions and source selections",()=> {
            var item=new ServiceItem { Title="版面測試",Lyrics="[v1]\n恩典與慈愛\n永遠跟隨我",Order="v1",Preview=new PreviewLayout() };
            foreach(var corner in new[]{"TopLeft","TopRight","BottomLeft","BottomRight"}) {
                item.Preview.ApplyPreset(corner);
                var xMargin=corner.EndsWith("Left") ? item.Preview.X : 100-item.Preview.X-item.Preview.ScaleX;
                var yMargin=corner.StartsWith("Top") ? item.Preview.Y : 100-item.Preview.Y-item.Preview.ScaleY;
                check(Math.Abs(xMargin*1280-yMargin*720)<.01 && yMargin==2,"Corner margins are not visually equal");
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
                window.ActiveServiceField<CalendarDatePicker>("ServiceDate").SelectedDate=DateTime.Today.AddDays(30); Pump();
                check(((ServicePlan)history.Items[0]).Id==current.Id && ((ServicePlan)history.SelectedItem).Id==current.Id,"Editing date did not re-sort / preserve selected service");
                window.ActiveServiceField<CalendarDatePicker>("ServiceDate").SelectedDate=DateTime.Today.AddDays(-1); Pump();
                check(history.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text=="本週崇拜").Classes.Contains("past-service"),"Date editing did not grey title immediately");
            } finally { window.Close(); }
        });
        test("Bottom-half text and credits export without exporting the camera illustration",()=> {
            var item=new ServiceItem { Title="下半部",Lyrics="[v1]\n平安喜樂",Order="v1",Copyright="作者",SongId="123",Preview=new PreviewLayout() };
            item.Preview.ApplyPreset("TopLeft"); var slides=SlideLayout.BuildItem(item,new UserSettings());
            slides.AddRange(SlideLayout.BuildItem(new ServiceItem {Title="讀經",Lyrics="耶和華是我的牧者",Order="",Preview=item.Preview},new UserSettings()));
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
    }
}
