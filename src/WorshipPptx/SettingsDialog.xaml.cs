using System.IO;
using System.Windows;
namespace WorshipPptx
{
    public partial class SettingsDialog : Window
    {
        public UserSettings Settings { get; private set; }
        public SettingsDialog(UserSettings settings) { InitializeComponent(); LicenseText.Text=settings.ChurchLicense; }
        private void Save_Click(object sender,RoutedEventArgs e)
        {
            Settings=new UserSettings { ChurchLicense=LicenseText.Text.Trim() }; DialogResult=true;
        }
    }
}
