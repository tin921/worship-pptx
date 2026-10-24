using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
namespace WorshipPptx.Tests;
static class UiReview
{
    private static void Draw(Window window,string path)
    {
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
        using var image=new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width,(int)window.Bounds.Height),new Vector(96,96));
        image.Render(window);image.Save(path,PngBitmapEncoderOptions.Default);
    }
    public static void Run(string root)
    {
        var fontSheet=new StackPanel {Margin=new Thickness(28),Spacing=8};
        fontSheet.Children.Add(new TextBlock {Text="中文字體比較",FontSize=28,FontWeight=FontWeight.SemiBold,FontFamily=FontCatalog.Get(FontCatalog.DefaultName).Family});
        fontSheet.Children.Add(new TextBlock {Text="後四款為新增字體；標示「簡體」的字體請用於簡體內容。",FontSize=15,Margin=new Thickness(0,0,0,8),FontFamily=FontCatalog.Get(FontCatalog.DefaultName).Family});
        foreach(var font in FontCatalog.Bundled) {
            var row=new Grid {ColumnDefinitions=new ColumnDefinitions("330,*"),Height=78};
            row.Children.Add(new TextBlock {Text=font.DisplayName.Split('·')[0].Trim(),FontSize=20,VerticalAlignment=VerticalAlignment.Center,FontFamily=FontCatalog.Get(FontCatalog.DefaultName).Family});
            var sample=new TextBlock {Text=FontTests.Sample(font),FontFamily=font.Family,FontSize=46,VerticalAlignment=VerticalAlignment.Center}; Grid.SetColumn(sample,1); row.Children.Add(sample);
            fontSheet.Children.Add(new Border {Child=row,BorderBrush=Brushes.LightGray,BorderThickness=new Thickness(0,0,0,1)});
        }
        var fontWindow=new Window {Width=1180,Height=850,Background=Brushes.White,Content=fontSheet}; fontWindow.Show(); Draw(fontWindow,Path.Combine(root,"font-comparison.png")); fontWindow.Close();
        var store=new LocalStore(Path.Combine(root,"data")); var plan=Demo.Create();
        plan.Items[1].Background="media:ocean-photo";store.Save(plan);
        var main=new MainWindow(store); main.Show(); Draw(main,Path.Combine(root,"editor.png"));
        main.FindControl<ListBox>("Thumbnails").SelectedIndex=1; Draw(main,Path.Combine(root,"editor-lyrics.png"));
        main.FindControl<TabControl>("SongTabs").SelectedIndex=0; Draw(main,Path.Combine(root,"editor-scripture.png"));
        main.FindControl<TabControl>("SongTabs").SelectedIndex=1;
        foreach(var font in FontCatalog.Bundled) {
            main.FindControl<ComboBox>("FontPicker").SelectedItem=font;
            Draw(main,Path.Combine(root,"font-"+font.Name.Replace(' ','-')+".png"));
        }
        main.FindControl<ComboBox>("FontPicker").SelectedItem=FontCatalog.Bundled[0];
        main.Width=1920; main.Height=760; Draw(main,Path.Combine(root,"editor-wide.png"));
        main.Width=1120; Draw(main,Path.Combine(root,"editor-compact.png"));
        main.Width=1440; main.Height=940;
        main.FindControl<Slider>("BackgroundOpacity").Value=0; Draw(main,Path.Combine(root,"opacity-zero.png"));
        var gallery=new BackgroundDialog(new ImageStore(store.Assets),"media:ocean-photo");gallery.Show();Draw(gallery,Path.Combine(root,"gallery.png"));
        gallery.Width=880; gallery.Height=680; Draw(gallery,Path.Combine(root,"gallery-compact.png"));gallery.Close();
        var preview=new PreviewWindow(SlideLayout.Build(plan,new UserSettings()));preview.Show();Draw(preview,Path.Combine(root,"preview.png"));
        for(var i=0;i<9;i++) preview.RaiseEvent(new Avalonia.Input.KeyEventArgs { RoutedEvent=Avalonia.Input.InputElement.KeyDownEvent,Key=Avalonia.Input.Key.Right });
        Draw(preview,Path.Combine(root,"preview-song.png"));
        preview.Width=600; preview.Height=450; Draw(preview,Path.Combine(root,"preview-compact.png"));preview.Close();
        var upload=new DriveUploadWindow(SlideLayout.Build(plan,new UserSettings()),plan.Title,DriveFileName.Create(plan,"CHURCH PC",new DateTime(2026,10,23),ChineseText.Traditional),store.Root);
        upload.Show();Draw(upload,Path.Combine(root,"upload.png"));upload.Close();main.Close();
        var settings=new SettingsDialog(store.LoadSettings()); settings.Show();Draw(settings,Path.Combine(root,"settings.png"));settings.Close();
        Console.WriteLine("UI screenshots: "+root);
    }
}
