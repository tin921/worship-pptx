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
        private readonly TextBlock caption = new TextBlock { TextTrimming=TextTrimming.CharacterEllipsis,FontWeight=FontWeight.SemiBold };
        private readonly TextBlock sectionCaption = new TextBlock { TextTrimming=TextTrimming.CharacterEllipsis };
        private readonly TextBlock totalCaption = new TextBlock { Name="TotalSlideCount",VerticalAlignment=VerticalAlignment.Center };
        private readonly TextBlock itemCaption = new TextBlock { Name="ItemSlideCount",VerticalAlignment=VerticalAlignment.Center,FontWeight=FontWeight.SemiBold };
        private int index;
        public PreviewWindow(IList<SlideData> slides)
        {
            this.slides=slides; Title=ChineseText.Ui("預覽整個崇拜 · 方向鍵翻頁，Esc 關閉"); Width=1100; Height=760; MinWidth=600; MinHeight=450; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            var dock=new DockPanel { Margin=new Thickness(22) }; Content=dock;
            var nav=new Grid { ColumnDefinitions=new ColumnDefinitions("104,16,*,16,160,16,104"),MaxWidth=900,Height=52,Margin=new Thickness(0,18,0,0) }; DockPanel.SetDock(nav,Dock.Bottom); dock.Children.Add(nav);
            var prev=new Button { Content=ChineseText.Ui("← 上一頁"),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center }; prev.Click+=(s,e)=>Move(-1); nav.Children.Add(prev);
            var status=new StackPanel { Spacing=4,VerticalAlignment=VerticalAlignment.Center }; status.Children.Add(caption); status.Children.Add(sectionCaption); Grid.SetColumn(status,2); nav.Children.Add(status);
            var counters=new Grid { ColumnDefinitions=new ColumnDefinitions("72,*"),RowDefinitions=new RowDefinitions("26,26") };
            counters.Children.Add(new TextBlock { Text=ChineseText.Ui("整份簡報"),VerticalAlignment=VerticalAlignment.Center });
            var itemLabel=new TextBlock { Text=ChineseText.Ui("本項目"),VerticalAlignment=VerticalAlignment.Center,FontWeight=FontWeight.SemiBold }; Grid.SetRow(itemLabel,1); counters.Children.Add(itemLabel);
            Grid.SetColumn(totalCaption,1); counters.Children.Add(totalCaption);
            Grid.SetColumn(itemCaption,1); Grid.SetRow(itemCaption,1); counters.Children.Add(itemCaption); Grid.SetColumn(counters,4); nav.Children.Add(counters);
            var next=new Button { Content=ChineseText.Ui("下一頁 →"),HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center }; next.Click+=(s,e)=>Move(1); Grid.SetColumn(next,6); nav.Children.Add(next); dock.Children.Add(screen);
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
            caption.Text=slide.Title; ToolTip.SetTip(caption,slide.Title);
            sectionCaption.Text=slide.Section;
            totalCaption.Text=(index+1)+" / "+slides.Count;
            itemCaption.Text=slide.ItemSlideNumber+" / "+slide.ItemSlideCount;
        }
        private void Move(int delta) { var next=System.Math.Clamp(index+delta,0,slides.Count-1); if(next==index) return; index=next; ShowPage(); }
    }
}
