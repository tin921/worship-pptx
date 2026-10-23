using System.Collections.Generic;
using Avalonia;
using Avalonia.Layout;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
namespace WorshipPptx
{
    public sealed class PreviewWindow : Window
    {
        private readonly IList<SlideData> slides;
        private readonly Viewbox screen = new Viewbox { Stretch=Stretch.Uniform };
        private readonly TextBlock caption = new TextBlock { VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(18,0,18,0) };
        private readonly TextBlock itemCaption = new TextBlock { Name="ItemSlideCount",HorizontalAlignment=HorizontalAlignment.Center,FontWeight=FontWeight.SemiBold };
        private int index;
        public PreviewWindow(IList<SlideData> slides)
        {
            this.slides=slides; Title=ChineseText.Ui("預覽整個崇拜 · 方向鍵翻頁，Esc 關閉"); Width=1100; Height=760; MinWidth=600; MinHeight=450; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            var dock=new DockPanel { Margin=new Thickness(22) }; Content=dock;
            var nav=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,18,0,0) }; DockPanel.SetDock(nav,Dock.Bottom); dock.Children.Add(nav);
            var prev=new Button { Content=ChineseText.Ui("← 上一頁") }; prev.Click+=(s,e)=>Move(-1); nav.Children.Add(prev);
            var counters=new StackPanel { Spacing=4,VerticalAlignment=VerticalAlignment.Center }; counters.Children.Add(caption); counters.Children.Add(itemCaption); nav.Children.Add(counters);
            var next=new Button { Content=ChineseText.Ui("下一頁 →") }; next.Click+=(s,e)=>Move(1); nav.Children.Add(next); dock.Children.Add(screen);
            AddHandler(KeyDownEvent,(_,e)=>
            {
                if(e.Key==Key.Right || e.Key==Key.Down || e.Key==Key.Space) { Move(1); e.Handled=true; }
                else if(e.Key==Key.Left || e.Key==Key.Up) { Move(-1); e.Handled=true; }
                else if(e.Key==Key.Escape) { Close(); e.Handled=true; }
            },RoutingStrategies.Tunnel,true);
            ShowPage();
        }
        private void ShowPage()
        {
            var slide=slides[index]; screen.Child=SlideVisual.Create(slide,true);
            caption.Text=(index+1)+" / "+slides.Count+"  ·  "+slide.Title+"  ·  "+slide.Section;
            itemCaption.Text=ChineseText.Ui("本項目")+"  "+slide.ItemSlideNumber+" / "+slide.ItemSlideCount;
        }
        private void Move(int delta) { index=(index+delta+slides.Count)%slides.Count; ShowPage(); }
    }
}
