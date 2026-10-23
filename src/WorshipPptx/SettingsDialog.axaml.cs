using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
namespace WorshipPptx
{
    public partial class SettingsDialog : Window
    {
        public UserSettings Settings { get; private set; }
        private readonly string language;
        public SettingsDialog(UserSettings settings) { InitializeComponent(); InputFocus.Attach(this); LicenseText.Text=settings.ChurchLicense; language=settings.Language; }
        private void Cancel_Click(object sender,RoutedEventArgs e) => Close(false);
        private void Save_Click(object sender,RoutedEventArgs e)
        {
            Settings=new UserSettings { ChurchLicense=(LicenseText.Text ?? "").Trim(),Language=language }; Close(true);
        }
    }
}
