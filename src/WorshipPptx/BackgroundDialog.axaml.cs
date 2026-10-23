using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace WorshipPptx
{
    public partial class BackgroundDialog : Window
    {
        private readonly ImageStore images;
        private readonly CancellationTokenSource cancellation=new();
        private bool busy;
        private bool ready;
        private int page;
        private readonly string songTitle,songLyrics;
        private IReadOnlyList<MediaAsset> results;
        private readonly List<Bitmap> thumbnails=new();
        private const int PageSize=12;
        public string ResultPath { get; private set; }
        public BackgroundDialog(ImageStore images,string current,string title="",string lyrics="")
        {
            InitializeComponent(); this.images=images; songTitle=title; songLyrics=lyrics; ResultPath=current ?? "";
            try { SelectBackground(ResultPath); }
            catch { ResultPath=""; ImagePreview.Source=SlideVisual.DefaultImage; }
            ready=true; ShowPage();
            Opened+=async (_,_)=>await MatchSong();
            Closed+=(_,_)=> { cancellation.Cancel(); foreach(var bitmap in thumbnails) bitmap.Dispose(); thumbnails.Clear(); };
        }
        private void SelectBackground(string key)
        {
            ResultPath=key; ImagePreview.Source=SlideVisual.Background(key);
            var asset=MediaLibrary.Find(key); SelectedTitle.Text=asset==null ? ChineseText.Ui("目前圖片") : ChineseText.Ui(asset.Title)+" · "+asset.StyleLabel;
        }
        private void ShowPage()
        {
            if(!ready) return;
            Gallery.Children.Clear(); foreach(var bitmap in thumbnails) bitmap.Dispose(); thumbnails.Clear();
            IEnumerable<MediaAsset> all=results ?? MediaLibrary.All;
            all=CollectionFilter.SelectedIndex switch {
                1=>all.Where(a=>a.Core && a.Style!="generic" && a.Style!="legacy"),
                2=>all.Where(a=>!a.Core && a.Style!="generic" && a.Style!="legacy"),
                3=>all.Where(a=>a.Style=="generic" && a.Lighting=="daylight"),
                4=>all.Where(a=>a.Style=="generic" && a.Lighting=="night"),
                5=>all.Where(a=>a.Style=="legacy"), _=>all };
            if(StyleFilter.SelectedIndex>0) { var style=new[]{"","photo","illustration","abstract","watercolour","sculpted"}[StyleFilter.SelectedIndex]; all=all.Where(a=>a.Style==style); }
            var list=all.ToList(); var pages=Math.Max(1,(list.Count+PageSize-1)/PageSize); page=Math.Clamp(page,0,pages-1);
            foreach(var asset in list.Skip(page*PageSize).Take(PageSize))
            {
                var stack=new StackPanel { Width=182,Spacing=7 };
                using var stream=File.OpenRead(MediaLibrary.FilePath(asset.Still));
                var bitmap=Bitmap.DecodeToWidth(stream,364); thumbnails.Add(bitmap);
                stack.Children.Add(new Image { Source=bitmap,Width=182,Height=102,Stretch=Stretch.UniformToFill });
                stack.Children.Add(new TextBlock { Text=ChineseText.Ui(asset.Title),FontSize=13,TextTrimming=TextTrimming.CharacterEllipsis });
                stack.Children.Add(new TextBlock { Text=asset.StyleLabel,FontSize=12,Foreground=Brushes.Gray });
                var button=new Button { Content=stack,Padding=new Thickness(7),Margin=new Thickness(0,0,8,10),BorderBrush=Brush.Parse(asset.Key==ResultPath ? "#28543E" : "#DEE4DC"),BorderThickness=new Thickness(asset.Key==ResultPath?2:1) };
                button.Click+=(_,_)=> { if(!busy) { SelectBackground(asset.Key); ShowPage(); } }; Gallery.Children.Add(button);
            }
            GalleryInfo.Text=ChineseText.Ui($"{list.Count} 個素材 · {page+1} / {pages}");
            PreviousPage.IsEnabled=page>0; NextPage.IsEnabled=page+1<pages;
        }
        private async void Search_Click(object sender,RoutedEventArgs e) => await MatchSong();
        private async Task MatchSong()
        {
            if(string.IsNullOrWhiteSpace(songTitle+songLyrics)) { MatchedTheme.Text=ChineseText.Ui("可自行輸入圖片關鍵字"); Message.Text=ChineseText.Ui("填寫歌名或歌詞後，可自動配對主題。"); return; }
            if(busy) return; busy=true; SetEnabled(false); Message.Text=ChineseText.Ui("正在擷取主題及關鍵字…");
            MatchingStatus.IsVisible=true; ShowMatchProgress(new(null,ChineseText.Ui("正在準備分析…")));
            try
            {
                var progress=new Progress<MediaSearchProgress>(value=> { if(busy && !cancellation.IsCancellationRequested) ShowMatchProgress(value); });
                var match=await MediaSearch.MatchAsync(songTitle,songLyrics,cancellation.Token,progress);
                if(cancellation.IsCancellationRequested) return;
                results=match.Assets; SearchText.Text=ChineseText.Content(match.Keywords); MatchedTheme.Text=ChineseText.Ui("配對主題：")+ChineseText.Ui(match.Theme);
                CollectionFilter.SelectedIndex=StyleFilter.SelectedIndex=0; page=0; ShowPage();
                Message.Text=ChineseText.Ui("已按歌名及歌詞配對主題，並填入圖片關鍵字。可選擇內置圖片，或開啟右方 Google 圖片搜尋。");
                ShowMatchProgress(new(100,ChineseText.Ui("主題及關鍵字已就緒")));
            }
            catch(OperationCanceledException) { }
            catch(Exception ex) { if(!cancellation.IsCancellationRequested) Message.Text=ChineseText.Ui("未能完成配對：")+ex.Message; }
            finally { busy=false; if(!cancellation.IsCancellationRequested) { MatchingStatus.IsVisible=false; SetEnabled(true); } }
        }
        private void ShowMatchProgress(MediaSearchProgress value)
        {
            MatchProgressMessage.Text=value.Message;
            MatchProgressBar.IsIndeterminate=value.Percent==null;
            if(value.Percent is int percent) MatchProgressBar.Value=percent;
            MatchProgressPercent.Text=value.Percent is int number ? number+"%" : "";
        }
        private void Keywords_Changed(object sender,TextChangedEventArgs e) => UpdateGoogleLink();
        private void UpdateGoogleLink()
        {
            if(GoogleImageLink==null) return;
            var keywords=SearchText.Text?.Trim();
            try { GoogleImageLink.NavigateUri=string.IsNullOrEmpty(keywords) ? null : ImageSearchLink.Create(keywords); }
            catch(ArgumentException) { GoogleImageLink.NavigateUri=null; }
            GoogleImageLink.IsEnabled=!busy && GoogleImageLink.NavigateUri!=null;
        }
        private void Filter_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(ready && CollectionFilter.SelectedIndex>=3) { results=null; StyleFilter.SelectedIndex=0; }
            page=0; ShowPage();
        }
        private void Previous_Click(object sender,RoutedEventArgs e) { page--; ShowPage(); GalleryScroll.Offset=new Vector(); }
        private void Next_Click(object sender,RoutedEventArgs e) { page++; ShowPage(); GalleryScroll.Offset=new Vector(); }
        private void ShowAll_Click(object sender,RoutedEventArgs e) { results=null; page=0; CollectionFilter.SelectedIndex=StyleFilter.SelectedIndex=0; ShowPage(); Message.Text=ChineseText.Ui("瀏覽素材不會執行歌詞分析。"); }
        private async Task Import(Func<Task<string>> action)
        {
            if(busy) return;
            busy=true; SetEnabled(false); Message.Text=ChineseText.Ui("正在載入圖片…");
            try { var path=await action(); if(cancellation.IsCancellationRequested) return; SelectBackground(path); Message.Text=ChineseText.Ui("圖片已就緒，按「套用背景」儲存。"); }
            catch(Exception ex) { if(!cancellation.IsCancellationRequested) Message.Text=ex is OperationCanceledException ? ChineseText.Ui("下載逾時，請再試一次。") : ChineseText.Ui(ex.Message); }
            finally { busy=false; SetEnabled(true); }
        }
        private void SetEnabled(bool enabled)
        {
            SearchText.IsEnabled=SearchButton.IsEnabled=ShowAllButton.IsEnabled=CollectionFilter.IsEnabled=StyleFilter.IsEnabled=BrowseButton.IsEnabled=PasteButton.IsEnabled=UrlButton.IsEnabled=ApplyButton.IsEnabled=enabled;
            UpdateGoogleLink();
        }
        private async void Browse_Click(object sender,RoutedEventArgs e)
        {
            var files=await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title=ChineseText.Ui("選擇背景圖片"), AllowMultiple=false, FileTypeFilter=new[]{FilePickerFileTypes.ImageAll} });
            if(files.Count>0) await Import(()=>ImportFileAsync(images,files[0],cancellation.Token));
        }
        private async void Paste_Click(object sender,RoutedEventArgs e) => await Paste();
        private async Task Paste() => await Import(async ()=>
        {
            var clipboard=GetTopLevel(this)?.Clipboard;
            using var data=clipboard==null ? null : await clipboard.TryGetDataAsync();
            if(data==null) throw new IOException(ChineseText.Ui("剪貼簿沒有圖片、檔案或圖片網址。"));
            var file=(await data.TryGetFilesAsync())?.OfType<IStorageFile>().FirstOrDefault();
            if(file!=null) return await ImportFileAsync(images,file,cancellation.Token);
            using var bitmap=await data.TryGetBitmapAsync();
            if(bitmap!=null) return images.SaveBitmap(bitmap);
            return await ImportText(images,await data.TryGetTextAsync(),cancellation.Token);
        });
        private async void Url_Click(object sender,RoutedEventArgs e) => await Import(()=>images.ImportUrlAsync(UrlText.Text ?? "",cancellation.Token));
        private async void Window_KeyDown(object sender,KeyEventArgs e)
        {
            if(e.Key==Key.Escape) { Close(false); return; }
            if(e.Key==Key.V && (e.KeyModifiers & (KeyModifiers.Control|KeyModifiers.Meta))!=0 && e.Source is not TextBox) { e.Handled=true; await Paste(); }
        }
        private void Default_Click(object sender,RoutedEventArgs e) { if(busy) return; SelectBackground(""); Message.Text=ChineseText.Ui("已選擇預設山景。"); }
        private void Apply_Click(object sender,RoutedEventArgs e) { if(!busy) Close(true); }
        private void Cancel_Click(object sender,RoutedEventArgs e) => Close(false);
        private void Image_DragOver(object sender,DragEventArgs e) { e.DragEffects=CanImport(e.DataTransfer) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled=true; }
        private async void Image_Drop(object sender,DragEventArgs e) { e.Handled=true; var data=e.DataTransfer; await Import(()=>ImportData(images,data,cancellation.Token)); }
        public static bool CanImport(IDataTransfer data) => data.Contains(DataFormat.File) || data.Contains(DataFormat.Bitmap) || data.Contains(DataFormat.Text);
        public static Task<string> ImportData(ImageStore images,IDataTransfer data,CancellationToken cancellation=default)
        {
            var file=data.TryGetFiles()?.OfType<IStorageFile>().FirstOrDefault();
            if(file!=null) return ImportFileAsync(images,file,cancellation);
            var bitmap=data.TryGetBitmap();
            if(bitmap!=null) return Task.FromResult(images.SaveBitmap(bitmap));
            return ImportText(images,data.TryGetText(),cancellation);
        }
        private static Task<string> ImportText(ImageStore images,string text,CancellationToken cancellation)
        {
            if(!string.IsNullOrWhiteSpace(text)) return images.ImportUrlAsync(text,cancellation);
            throw new IOException(ChineseText.Ui("請拖放或複製一張圖片、圖片檔案或直接圖片網址。"));
        }
        public static async Task<string> ImportFileAsync(ImageStore images,IStorageFile file,CancellationToken cancellation=default)
        {
            var path=file.TryGetLocalPath();
            if(path!=null) return images.ImportFile(path);
            using var input=await file.OpenReadAsync(); using var output=new MemoryStream();
            var buffer=new byte[81920]; int count;
            while((count=await input.ReadAsync(buffer,cancellation))>0)
            {
                if(output.Length+count>ImageStore.MaxBytes) throw new IOException(ChineseText.Ui("圖片不能超過 25 MB。"));
                output.Write(buffer,0,count);
            }
            return images.ImportBytes(output.ToArray());
        }
    }
}
