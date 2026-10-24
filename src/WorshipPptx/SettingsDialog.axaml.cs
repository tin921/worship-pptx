using System.IO;
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
namespace WorshipPptx
{
    public partial class SettingsDialog : Window
    {
        public UserSettings Settings { get; private set; }
        private readonly string language;
        public SettingsDialog(UserSettings settings) { InitializeComponent(); InputFocus.Attach(this); LicenseText.Text=settings.ChurchLicense; SaveFolder.Text=settings.PptxSaveFolder; language=settings.Language; }
        private void Cancel_Click(object sender,RoutedEventArgs e) => Close(false);
        private async void Save_Click(object sender,RoutedEventArgs e)
        {
            try { Settings=new UserSettings { ChurchLicense=(LicenseText.Text ?? "").Trim(),Language=language,PptxSaveFolder=LocalPresentationFiles.NormalizeFolder(SaveFolder.Text) }; Close(true); }
            catch(Exception ex) { await Dialogs.Show(this,ex.Message,ChineseText.Ui("請檢查儲存資料夾")); }
        }
        private async void Browse_Click(object sender,RoutedEventArgs e)
        {
            var folders=await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title=ChineseText.Ui("選擇儲存資料夾"),AllowMultiple=false });
            if(folders.Count>0 && folders[0].TryGetLocalPath() is string path) SaveFolder.Text=path;
        }
    }
}
