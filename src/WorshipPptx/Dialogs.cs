using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace WorshipPptx
{
    internal static class Dialogs
    {
        public static Task<bool> Show(Window owner, string message, string title = "敬拜工坊", bool confirm = false)
        {
            var window = new Window { Title=ChineseText.Ui(title), Width=480, SizeToContent=SizeToContent.Height, CanResize=false, WindowStartupLocation=WindowStartupLocation.CenterOwner };
            var panel = new StackPanel { Margin=new Thickness(26), Spacing=24 };
            panel.Children.Add(new TextBlock { Text=ChineseText.Ui(message), TextWrapping=TextWrapping.Wrap });
            var buttons = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Spacing=10 };
            if (confirm) { var cancel=new Button { Content=ChineseText.Ui("取消") }; cancel.Click+=(_,_)=>window.Close(false); buttons.Children.Add(cancel); }
            var ok=new Button { Content=ChineseText.Ui(confirm ? "移除" : "確定") }; ok.Classes.Add("primary"); ok.Click+=(_,_)=>window.Close(true); buttons.Children.Add(ok);
            panel.Children.Add(buttons); window.Content=panel;
            window.KeyDown+=(_,e)=> { if(e.Key==Key.Escape) window.Close(false); };
            return window.ShowDialog<bool>(owner);
        }
    }
}
