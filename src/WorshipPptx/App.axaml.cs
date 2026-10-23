using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace WorshipPptx
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                Dispatcher.UIThread.UnhandledException+=async (_,e)=>
                {
                    e.Handled=true;
                    if(desktop.MainWindow!=null) await Dialogs.Show(desktop.MainWindow,e.Exception.Message,"未能完成操作");
                };
                var root=Environment.GetEnvironmentVariable("WORSHIP_PPTX_DATA");
                // Keep the original Windows directory and JSON format so existing plans load in place.
                if (string.IsNullOrWhiteSpace(root)) root=OperatingSystem.IsMacOS()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Library","Application Support","WorshipPptx")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorshipPptx");
                var main=new MainWindow(new LocalStore(root));
                main.Opened+=(_,_)=>Dispatcher.UIThread.Post(async ()=>
                {
                    try { await MediaSearch.PreloadAsync(); }
                    catch(Exception ex) { System.Diagnostics.Trace.TraceError("Background model loading failed: {0}",ex); }
                },DispatcherPriority.Background);
                var primary=main.Screens.Primary;
                if(primary!=null)
                {
                    var area=primary.WorkingArea; var scale=primary.Scaling;
                    var availableWidth=area.Width/scale; var availableHeight=area.Height/scale-40;
                    main.MinWidth=Math.Min(main.MinWidth,availableWidth); main.MinHeight=Math.Min(main.MinHeight,availableHeight);
                    main.Width=Math.Min(main.Width,availableWidth); main.Height=Math.Min(main.Height,availableHeight);
                    main.WindowStartupLocation=WindowStartupLocation.Manual;
                    main.Position=new PixelPoint(area.X+Math.Max(0,(int)((area.Width-main.Width*scale)/2)),area.Y+Math.Max(0,(int)((area.Height-main.Height*scale)/2)));
                }
                desktop.MainWindow=main;
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
