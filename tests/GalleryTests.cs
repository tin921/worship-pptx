using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace WorshipPptx.Tests;

static class GalleryTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,ImageStore images)
    {
        test("Gallery fits complete rows on resize and appends results on wheel scrolling",()=> {
            var picker=new BackgroundDialog(images,"");
            void Flush() { Dispatcher.UIThread.RunJobs(); picker.UpdateLayout(); Dispatcher.UIThread.RunJobs(); picker.UpdateLayout(); }
            picker.Show(); Flush();
            try {
                var gallery=picker.FindControl<WrapPanel>("Gallery");
                var scroll=picker.FindControl<ScrollViewer>("GalleryScroll");
                check(picker.FindControl<ComboBox>("CollectionFilter").Items.Count==5,"Old sample category is still offered");
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
                }
                check(observedColumns.Count>1,"Resizing never changed column count");
                var initial=gallery.Children.Cast<Button>().Select(c=>((MediaAsset)c.Tag).Key).ToArray();
                void Wheel(double delta) {
                    var point=scroll.TranslatePoint(new Point(scroll.Bounds.Width/2,scroll.Bounds.Height/2),picker).Value;
                    picker.MouseMove(point); picker.MouseWheel(point,new Vector(0,delta),RawInputModifiers.None); Flush();
                }
                Wheel(-10);
                check(gallery.Children.Count>initial.Length && scroll.Offset.Y>0,"First wheel gesture did not append and scroll");
                check(gallery.Children.Cast<Button>().Take(initial.Length).Select(c=>((MediaAsset)c.Tag).Key).SequenceEqual(initial),"Appending replaced earlier results");
                var selected=gallery.Children.Cast<Button>().Last(); var selectedKey=((MediaAsset)selected.Tag).Key;
                var count=gallery.Children.Count; var offset=scroll.Offset.Y;
                selected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Flush();
                check(picker.ResultPath==selectedKey && gallery.Children.Count==count && Math.Abs(scroll.Offset.Y-offset)<1,"Selecting an image reset browsing");
                picker.Width=1320; Flush();
                check(picker.ResultPath==selectedKey && gallery.Children.Count>=count,"Resize lost selected/loaded results");
                for(var i=0;i<100 && gallery.Children.Count<MediaLibrary.All.Count;i++) Wheel(-20);
                var keys=gallery.Children.Cast<Button>().Select(c=>((MediaAsset)c.Tag).Key).ToList();
                check(keys.Count==MediaLibrary.All.Count && keys.Distinct().Count()==keys.Count,"Infinite scrolling did not reach every asset exactly once");
                Wheel(30); check(scroll.Offset.Y<scroll.Extent.Height-scroll.Viewport.Height,"Cannot scroll back through appended results");
                picker.FindControl<ComboBox>("StyleFilter").SelectedIndex=4; Flush();
                check(gallery.Children.Count==1 && scroll.Offset.Y==0 && picker.ResultPath==selectedKey,"Filtering did not reset paging or lost selection");
                picker.FindControl<ComboBox>("StyleFilter").SelectedIndex=0; Flush();
                check(scroll.Offset.Y==0 && gallery.Children.Count<MediaLibrary.All.Count,"Clearing filter did not restore first batch");
                picker.FindControl<ComboBox>("CollectionFilter").SelectedIndex=3; Flush();
                picker.FindControl<ComboBox>("StyleFilter").SelectedIndex=1; Flush();
                check(gallery.Children.Count==0,"Empty collection/style combination ignored the style filter");
                check(picker.FindControl<TextBlock>("GalleryInfo").Text.StartsWith("0 / 0"),"Empty result count is stale");
                check(!MediaSearch.IsModelLoaded,"Resizing/scrolling/filtering initialized the embedding model");
            } finally { picker.Close(); }
        });
    }
}
