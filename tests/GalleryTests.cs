using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WorshipPptx.Tests;

static class GalleryTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,ImageStore images)
    {
        test("Style symbols are legible vectors and full-screen viewing does not select a result",()=> {
            var picker=new BackgroundDialog(images,"");picker.Show();Dispatcher.UIThread.RunJobs();picker.UpdateLayout();Dispatcher.UIThread.RunJobs();
            try {
                var filters=picker.FindControl<WrapPanel>("StyleFilters").Children.Cast<ToggleButton>().ToList();
                check(filters.All(b=>b.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Any() && !b.GetVisualDescendants().OfType<Image>().Any()),"Style buttons still use entire photo thumbnails");
                var before=picker.ResultPath;
                var expand=picker.FindControl<WrapPanel>("Gallery").GetVisualDescendants().OfType<Button>().First(b=>b.Name?.StartsWith("Expand_")==true);
                expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
                var full=picker.OwnedWindows.OfType<BackgroundImageWindow>().Single();
                check(full.WindowState==WindowState.FullScreen && picker.ResultPath==before,"Opening an image selected it or was not fullscreen");
                full.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape});Dispatcher.UIThread.RunJobs();
                check(!full.IsVisible && picker.IsVisible && picker.ResultPath==before,"Escape did not return to unchanged picker");
            } finally {picker.Close();}
        });
        test("Double-clicking a gallery image opens one fullscreen viewer and Escape returns to the picker",()=> {
            var picker=new BackgroundDialog(images,""); picker.Show(); Dispatcher.UIThread.RunJobs(); picker.UpdateLayout(); Dispatcher.UIThread.RunJobs();
            try {
                var card=picker.FindControl<WrapPanel>("Gallery").Children.Cast<Button>().First();
                var point=card.TranslatePoint(new Point(card.Bounds.Width/2,card.Bounds.Height/3),picker).Value;
                void Click() { picker.MouseDown(point,MouseButton.Left); picker.MouseUp(point,MouseButton.Left); Dispatcher.UIThread.RunJobs(); }
                Click();
                check(picker.ResultPath==((MediaAsset)card.Tag).Key && !picker.OwnedWindows.Any(),"Single click should only select the result");
                Click();
                var full=picker.OwnedWindows.OfType<BackgroundImageWindow>().Single();
                check(full.WindowState==WindowState.FullScreen && full.Title==ChineseText.Ui(((MediaAsset)card.Tag).Title),"Double-click did not open the clicked image fullscreen");
                full.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Escape }); Dispatcher.UIThread.RunJobs();
                check(!full.IsVisible && picker.IsVisible,"Escape did not close the fullscreen image");
            } finally { picker.Close(); }
        });
        test("Gallery fits complete rows on resize and appends results on wheel scrolling",()=> {
            var picker=new BackgroundDialog(images,"");
            void Flush() { Dispatcher.UIThread.RunJobs(); picker.UpdateLayout(); Dispatcher.UIThread.RunJobs(); picker.UpdateLayout(); }
            picker.Show(); Flush();
            try {
                var gallery=picker.FindControl<WrapPanel>("Gallery");
                var scroll=picker.FindControl<ScrollViewer>("GalleryScroll");
                var styles=picker.FindControl<WrapPanel>("StyleFilters").Children.Cast<ToggleButton>().ToList();
                var themed=MediaLibrary.All.Count(a=>a.Style!="generic");
                var total=picker.FindControl<TextBlock>("GalleryInfo");
                check(total.Text==gallery.Children.Count+" / "+themed && gallery.Children.Count<themed,"Initial count should show loaded and total results");
                check(styles.Count==5 && styles.All(b=>b.IsChecked==true),"Image styles are not all selected by default");
                check(picker.FindControl<Button>("ShowAllButton")==null && !picker.FindControl<TextBlock>("Message").IsVisible,"Removed gallery controls or status remain");
                check(scroll.VerticalScrollBarVisibility==ScrollBarVisibility.Visible && !scroll.AllowAutoHide,"Gallery scrollbar is not visible");
                var observedColumns=new System.Collections.Generic.HashSet<int>();
                foreach(var size in new[]{new Size(880,680),new Size(1440,940),new Size(1080,800)}) {
                    picker.Width=size.Width; picker.Height=size.Height; Flush();
                    var cards=gallery.Children.Cast<Button>().ToList();
                    var columns=cards.Count(c=>Math.Abs(c.Bounds.Y-cards[0].Bounds.Y)<1);
                    observedColumns.Add(columns);
                    check(cards.Count>0 && cards.Count%columns==0,"Initial page has a partial row");
                    check(scroll.Extent.Height>scroll.Viewport.Height,"Initial extra row does not make scrolling discoverable");
                    check(scroll.Extent.Height-scroll.Viewport.Height<=cards[0].Bounds.Height+11,"Too much content loaded initially");
                    check(total.Text==cards.Count+" / "+themed,"Resize did not update loaded / total count");
                }
                check(observedColumns.Count>1,"Resizing never changed column count");
                var initial=gallery.Children.Cast<Button>().Select(c=>((MediaAsset)c.Tag).Key).ToArray();
                void Wheel(double delta) {
                    var point=scroll.TranslatePoint(new Point(scroll.Bounds.Width/2,scroll.Bounds.Height/2),picker).Value;
                    picker.MouseMove(point); picker.MouseWheel(point,new Vector(0,delta),RawInputModifiers.None); Flush();
                }
                Wheel(-10);
                check(gallery.Children.Count>initial.Length && scroll.Offset.Y>0,"First wheel gesture did not append and scroll");
                check(total.Text==gallery.Children.Count+" / "+themed,"Appending did not update loaded / total count");
                check(gallery.Children.Cast<Button>().Take(initial.Length).Select(c=>((MediaAsset)c.Tag).Key).SequenceEqual(initial),"Appending replaced earlier results");
                var selected=gallery.Children.Cast<Button>().Last(); var selectedKey=((MediaAsset)selected.Tag).Key;
                var count=gallery.Children.Count; var offset=scroll.Offset.Y;
                selected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
                check(picker.ResultPath==selectedKey && gallery.Children.Count==count && Math.Abs(scroll.Offset.Y-offset)<1,"Selecting an image reset browsing");
                picker.Width=1320; Flush();
                check(picker.ResultPath==selectedKey && gallery.Children.Count>=count,"Resize lost selected/loaded results");
                for(var i=0;i<100 && gallery.Children.Count<themed;i++) Wheel(-20);
                var keys=gallery.Children.Cast<Button>().Select(c=>((MediaAsset)c.Tag).Key).ToList();
                check(keys.Count==themed && keys.Distinct().Count()==keys.Count,"Infinite scrolling did not reach every themed asset exactly once");
                Wheel(30); check(scroll.Offset.Y<scroll.Extent.Height-scroll.Viewport.Height,"Cannot scroll back through appended results");
                foreach(var button in styles) button.IsChecked=(string)button.Tag=="minimal"; Flush();
                check(total.Text==gallery.Children.Count+" / "+MediaLibrary.All.Count(a=>a.Style=="minimal"),"Count did not follow the style filter");
                check(gallery.Children.Count>0 && gallery.Children.Cast<Button>().All(c=>((MediaAsset)c.Tag).Style=="minimal") && scroll.Offset.Y==0 && picker.ResultPath==selectedKey,"Filtering did not reset paging or lost selection");
                foreach(var button in styles) button.IsChecked=true; Flush();
                check(scroll.Offset.Y==0 && gallery.Children.Count<MediaLibrary.All.Count,"Clearing filter did not restore first batch");
                foreach(var button in styles) button.IsChecked=false; Flush();
                check(gallery.Children.Count==0,"Deselecting all styles still shows results");
                check(total.Text=="0 / 0","Empty result count is stale");
                picker.FindControl<ToggleButton>("GenericFilter").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
                check(total.Text==gallery.Children.Count+" / "+MediaLibrary.All.Count(a=>a.Style=="generic"),"Count did not follow the generic collection");
                for(var i=0;i<20 && gallery.Children.Count<20;i++) Wheel(-20);
                var generic=gallery.Children.Cast<Button>().Select(b=>(MediaAsset)b.Tag).ToList();
                check(generic.Count==20 && generic.All(a=>a.Style=="generic") && generic.Select(a=>a.Lighting).Distinct().Count()==2,"Generic mode does not include both daylight and night images");
                check(!picker.FindControl<WrapPanel>("StyleFilters").IsEnabled && picker.ResultPath==selectedKey,"Generic mode changed selection or applies themed styles");
                picker.FindControl<ToggleButton>("MatchedFilter").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
                check(styles.All(b=>b.IsChecked==false) && gallery.Children.Count==0,"Returning to matches lost style choices");
                check(!MediaSearch.IsModelLoaded,"Resizing/scrolling/filtering initialized the embedding model");
            } finally { picker.Close(); }
        });
    }
}
