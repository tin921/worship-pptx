using System;
using System.IO;
using System.Net;
using System.Windows;
namespace WorshipPptx
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            DispatcherUnhandledException += (s, a) => { MessageBox.Show(a.Exception.Message, "敬拜簡報", MessageBoxButton.OK, MessageBoxImage.Error); a.Handled = true; };
            var root = Environment.GetEnvironmentVariable("WORSHIP_PPTX_DATA");
            var store = new LocalStore(string.IsNullOrWhiteSpace(root) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorshipPptx") : root);
            var main = new MainWindow(store);
            MainWindow = main;
            main.Show();
        }
    }
}
