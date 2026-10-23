using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
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
        var store=new LocalStore(Path.Combine(root,"data")); var plan=Demo.Create();
        plan.Items[1].Background="media:ocean-photo";store.Save(plan);
        var main=new MainWindow(store); main.Show(); Draw(main,Path.Combine(root,"editor.png"));
        main.FindControl<Slider>("BackgroundOpacity").Value=0; Draw(main,Path.Combine(root,"opacity-zero.png"));
        var gallery=new BackgroundDialog(new ImageStore(store.Assets),"media:ocean-photo");gallery.Show();Draw(gallery,Path.Combine(root,"gallery.png"));gallery.Close();
        var upload=new DriveUploadWindow(SlideLayout.Build(plan,new UserSettings()),plan.Title,DriveFileName.Create(plan,"CHURCH PC",new DateTime(2026,10,23),ChineseText.Traditional),store.Root);
        upload.Show();Draw(upload,Path.Combine(root,"upload.png"));upload.Close();main.Close();
        Console.WriteLine("UI screenshots: "+root);
    }
}
