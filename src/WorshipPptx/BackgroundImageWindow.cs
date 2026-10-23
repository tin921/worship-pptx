using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace WorshipPptx;

public sealed class BackgroundImageWindow : Window
{
    private readonly Bitmap bitmap;
    public BackgroundImageWindow(MediaAsset asset)
    {
        Title=ChineseText.Ui(asset.Title);
        Background=Brushes.Black;
        WindowState=WindowState.FullScreen;
        WindowDecorations=WindowDecorations.None;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;
        bitmap=new Bitmap(MediaLibrary.FilePath(asset.Still));
        var root=new Grid();
        root.Children.Add(new Image { Source=bitmap,Stretch=Stretch.Uniform });
        var close=new Button { Name="CloseImage",Content="×",FontSize=26,Width=44,Height=44,Padding=new Thickness(0),Margin=new Thickness(16),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Background=Brush.Parse("#B3000000"),Foreground=Brushes.White };
        AutomationProperties.SetName(close,ChineseText.Ui("關閉圖片"));
        ToolTip.SetTip(close,ChineseText.Ui("關閉 · Esc"));
        close.Click+=(_,_)=>Close(); root.Children.Add(close); Content=root;
        AddHandler(KeyDownEvent,(_,e)=> { if(e.Key==Key.Escape) { e.Handled=true; Close(); } },RoutingStrategies.Tunnel,true);
        Opened+=(_,_)=>Focus();
        Closed+=(_,_)=>bitmap.Dispose();
    }
}
