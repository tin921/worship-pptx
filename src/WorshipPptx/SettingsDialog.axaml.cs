using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
namespace WorshipPptx
{
    public partial class SettingsDialog : Window
    {
        public UserSettings Settings { get; private set; }
        public SettingsDialog(UserSettings settings) { InitializeComponent(); LicenseText.Text=settings.ChurchLicense; }
        private void Cancel_Click(object sender,RoutedEventArgs e) => Close(false);
        private void Save_Click(object sender,RoutedEventArgs e)
        {
            Settings=new UserSettings { ChurchLicense=(LicenseText.Text ?? "").Trim() }; Close(true);
        }
    }
}
