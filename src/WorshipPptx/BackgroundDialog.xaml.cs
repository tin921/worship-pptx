using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
namespace WorshipPptx
{
    public partial class BackgroundDialog : Window
    {
        private readonly ImageStore images;
        private readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        private bool busy;
        public string ResultPath { get; private set; }
        public BackgroundDialog(ImageStore images,string current)
        {
            InitializeComponent(); this.images=images; ResultPath=current ?? "";
            try { ImagePreview.Source=SlideVisual.Background(ResultPath); } catch { ResultPath=""; ImagePreview.Source=SlideVisual.DefaultImage; }
            Closed+=(s,e)=>cancellation.Cancel();
        }
        private async Task Import(Func<Task<string>> action)
        {
            if(busy) return;
            busy=true; SetEnabled(false); Message.Text="正在載入圖片…";
            try { var path=await action(); if(cancellation.IsCancellationRequested) return; ResultPath=path; ImagePreview.Source=SlideVisual.Background(path); Message.Text="圖片已就緒，按「套用背景」儲存。"; }
            catch(Exception ex) { if(!cancellation.IsCancellationRequested) Message.Text=ex is OperationCanceledException ? "下載逾時，請再試一次。" : ex.Message; }
            finally { busy=false; SetEnabled(true); }
        }
        private void SetEnabled(bool enabled) { BrowseButton.IsEnabled=PasteButton.IsEnabled=UrlButton.IsEnabled=ApplyButton.IsEnabled=enabled; }
        private async void Browse_Click(object sender,RoutedEventArgs e)
        {
            var dialog=new OpenFileDialog { Filter="圖片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|所有檔案|*.*" };
            if(dialog.ShowDialog(this)==true) await Import(()=>Task.FromResult(images.ImportFile(dialog.FileName)));
        }
        private async void Paste_Click(object sender,RoutedEventArgs e) => await Paste();
        private async Task Paste()
        {
            await Import(()=> { var data=Clipboard.GetDataObject(); if(data==null) throw new IOException("剪貼簿沒有圖片、檔案或圖片網址。"); return ImportData(images,data,cancellation.Token); });
        }
        private async void Url_Click(object sender,RoutedEventArgs e) => await Import(()=>images.ImportUrlAsync(UrlText.Text,cancellation.Token));
        private async void Window_KeyDown(object sender,KeyEventArgs e)
        {
            if(e.Key==Key.V && Keyboard.Modifiers==ModifierKeys.Control && !(Keyboard.FocusedElement is TextBox)) { e.Handled=true; await Paste(); }
        }
        private void Default_Click(object sender,RoutedEventArgs e) { if(busy) return; ResultPath=""; ImagePreview.Source=SlideVisual.DefaultImage; Message.Text="已選擇預設山景。"; }
        private void Apply_Click(object sender,RoutedEventArgs e) { if(!busy) DialogResult=true; }
        private void Image_DragOver(object sender,DragEventArgs e) { e.Effects=CanImport(e.Data) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled=true; }
        private async void Image_Drop(object sender,DragEventArgs e) { e.Handled=true; var data=e.Data; await Import(()=>ImportData(images,data,cancellation.Token)); }
        public static bool CanImport(IDataObject data) => data.GetDataPresent(DataFormats.FileDrop) || data.GetDataPresent(DataFormats.Bitmap) || data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text);
        public static Task<string> ImportData(ImageStore images,IDataObject data,CancellationToken cancellation=default(CancellationToken))
        {
            if(data.GetDataPresent(DataFormats.FileDrop)) { var paths=data.GetData(DataFormats.FileDrop) as string[]; if(paths?.Length>0) return Task.FromResult(images.ImportFile(paths[0])); }
            if(data.GetDataPresent(DataFormats.Bitmap) && data.GetData(DataFormats.Bitmap) is BitmapSource bitmap) return Task.FromResult(images.SaveBitmap(bitmap));
            var text=data.GetData(DataFormats.UnicodeText) as string ?? data.GetData(DataFormats.Text) as string;
            if(!string.IsNullOrWhiteSpace(text)) return images.ImportUrlAsync(text,cancellation);
            throw new IOException("請拖放或複製一張圖片、圖片檔案或直接圖片網址。");
        }
    }
}
