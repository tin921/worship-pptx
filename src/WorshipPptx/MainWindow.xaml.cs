using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
namespace WorshipPptx
{
    public partial class MainWindow : Window
    {
        private readonly LocalStore store;
        private UserSettings settings;
        private ServicePlan plan;
        private ServiceItem selected;
        private bool loading;
        private bool dirty;
        private readonly DispatcherTimer debounce = new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(650) };
        private List<SlideData> slides = new List<SlideData>();
        private int page;
        public MainWindow(LocalStore store)
        {
            loading=true; InitializeComponent(); this.store=store;
            settings=store.LoadSettings(); FontPicker.ItemsSource=FontCatalog.All;
            var history=store.LoadHistory(); plan=history.FirstOrDefault() ?? Demo.Create();
            debounce.Tick += (s,e) => { debounce.Stop(); SaveDraft(); RefreshPreview(); };
            LoadPlan(); RefreshHistory(); loading=false;
            RefreshPreview(); SaveDraft();
            if(!string.IsNullOrEmpty(store.RecoveryWarning)) StatusText.Text=store.RecoveryWarning;
            Closing += OnClosing;
            PreviewKeyDown += (s,e) => { if(e.Key==Key.F5) { Preview_Click(s,e); e.Handled=true; } if(e.Key==Key.S && Keyboard.Modifiers==ModifierKeys.Control) { SaveDraft(); e.Handled=true; } };
        }
        private void LoadPlan()
        {
            loading=true; ServiceTitle.Text=plan.Title; ServiceDate.SelectedDate=plan.Date; FontPicker.SelectedItem=FontCatalog.Get(plan.FontName);
            SongTabs.Items.Clear(); foreach(var item in plan.Items) SongTabs.Items.Add(new TabItem { Header=item.Title, Tag=item });
            var firstSong=plan.Items.FindIndex(i=>i.Kind=="song"); SongTabs.SelectedIndex=Math.Max(0,firstSong);
            selected=plan.Items[SongTabs.SelectedIndex]; FillEditor(); loading=false; page=0;
        }
        private void FillEditor()
        {
            SongTitle.Text=selected.Title; Lyrics.Text=selected.Lyrics; SongOrder.Text=selected.Order;
            CopyrightText.Text=selected.Copyright; SongId.Text=selected.SongId;
            var song=selected.Kind=="song"; TitleLabel.Text=song ? "詩歌名稱" : "經文名稱／出處"; LyricsLabel.Text=song ? "歌詞" : "經文";
            OrderPanel.Visibility=song ? Visibility.Visible : Visibility.Collapsed;
            LyricsHint.Text=song ? "[v1]、[c] 標記段落；空行分頁。" : "直接輸入經文；用空行指定分頁，長句會自動換行。";
            SongId.IsEnabled=song;
        }
        private void Capture()
        {
            if(loading || selected==null) return;
            plan.Title=ServiceTitle.Text; plan.Date=ServiceDate.SelectedDate ?? plan.Date;
            selected.Title=SongTitle.Text; selected.Lyrics=Lyrics.Text; selected.Order=SongOrder.Text; selected.Copyright=CopyrightText.Text; selected.SongId=SongId.Text;
            if(SongTabs.SelectedItem is TabItem tab) tab.Header=selected.Title;
        }
        private void Changed()
        {
            if(loading) return; Capture(); dirty=true; StatusText.Text="正在儲存…"; debounce.Stop(); debounce.Start();
        }
        private void Editor_Changed(object sender,TextChangedEventArgs e) => Changed();
        private void Metadata_Changed(object sender,TextChangedEventArgs e) => Changed();
        private void Date_Changed(object sender,SelectionChangedEventArgs e) => Changed();
        private void SaveDraft()
        {
            if(plan==null) return;
            try { Capture(); store.Save(plan); dirty=false; StatusText.Text=plan.Items.Count+" 個項目 · "+plan.Items.Count(i=>i.Kind=="song")+" 首詩歌 · 已儲存 "+DateTime.Now.ToString("HH:mm"); }
            catch(Exception ex) { dirty=true; StatusText.Text="未能儲存："+ex.Message; }
        }
        private void OnClosing(object sender,CancelEventArgs e)
        {
            debounce.Stop(); SaveDraft();
            if(dirty) { e.Cancel=true; MessageBox.Show(this,"未能儲存本次修改。請先檢查磁碟空間或資料夾權限，再關閉程式。","儲存失敗",MessageBoxButton.OK,MessageBoxImage.Error); }
        }
        private void RefreshHistory()
        {
            var prior=loading; loading=true;
            var items=store.LoadHistory(); HistoryList.ItemsSource=items; HistoryList.SelectedItem=items.FirstOrDefault(p=>p.Id==plan.Id); loading=prior;
        }
        private void History_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(loading || !(HistoryList.SelectedItem is ServicePlan next)) return;
            debounce.Stop(); SaveDraft(); if(dirty) return; plan=next; LoadPlan(); RefreshPreview();
        }
        private void Tab_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(loading || !(SongTabs.SelectedItem is TabItem tab)) return;
            debounce.Stop(); // TextChanged already copied the previous item's fields.
            selected=(ServiceItem)tab.Tag; loading=true; FillEditor(); loading=false; page=0; SaveDraft(); RefreshPreview();
        }
        private void NewService_Click(object sender,RoutedEventArgs e)
        {
            SaveDraft(); if(dirty) return;
            plan=Demo.Create(); LoadPlan(); SaveDraft(); RefreshHistory(); RefreshPreview();
        }
        private void AddSong_Click(object sender,RoutedEventArgs e)
        {
            Capture(); selected=new ServiceItem(); plan.Items.Add(selected);
            loading=true; var tab=new TabItem { Header=selected.Title,Tag=selected }; SongTabs.Items.Add(tab); SongTabs.SelectedItem=tab; FillEditor(); loading=false; page=0; SaveDraft(); RefreshPreview();
        }
        private void RemoveItem_Click(object sender,RoutedEventArgs e)
        {
            if(plan.Items.Count==1) { MessageBox.Show(this,"崇拜安排至少需要一個項目。"); return; }
            if(MessageBox.Show(this,"移除「"+selected.Title+"」？", "移除項目",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) return;
            plan.Items.Remove(selected); LoadPlan(); SaveDraft(); RefreshPreview();
        }
        private void RefreshPreview()
        {
            try
            {
                Capture(); slides=SlideLayout.BuildItem(selected,settings,plan.FontName); ValidationText.Text="";
                BackgroundImage.Source=SlideVisual.Background(selected.Background);
                Thumbnails.Children.Clear();
                for(var i=0;i<slides.Count;i++)
                {
                    var index=i;
                    var stack=new StackPanel(); stack.Children.Add(new Viewbox { Width=112,Height=63,Child=SlideVisual.Create(slides[i]) });
                    stack.Children.Add(new TextBlock { Text=(i+1)+" · "+slides[i].Section, FontSize=13, Margin=new Thickness(0,6,0,2), HorizontalAlignment=HorizontalAlignment.Center });
                    var button=new Button { Content=stack,Padding=new Thickness(3),Margin=new Thickness(0,0,0,10),ToolTip=slides[i].Text };
                    button.Click += (s,e) => { page=index; ShowPage(); }; Thumbnails.Children.Add(button);
                }
                page=Math.Min(page,Math.Max(0,slides.Count-1)); ShowPage();
            }
            catch(Exception ex) { slides.Clear(); SlideHost.Child=null; Thumbnails.Children.Clear(); PreviewInfo.Text="請先完成內容"; ValidationText.Text=ex.Message; try { BackgroundImage.Source=SlideVisual.Background(selected.Background); } catch { BackgroundImage.Source=null; } }
        }
        private void ShowPage()
        {
            if(slides.Count==0) return;
            SlideHost.Child=SlideVisual.Create(slides[page]); PreviewInfo.Text=(page+1)+" / "+slides.Count+"  ·  "+slides[page].Section;
            for(var i=0;i<Thumbnails.Children.Count;i++) ((Button)Thumbnails.Children[i]).BorderBrush=new SolidColorBrush(i==page ? Color.FromRgb(66,107,84) : Color.FromRgb(222,229,221));
        }
        private void Previous_Click(object sender,RoutedEventArgs e) { if(slides.Count==0) return; page=(page+slides.Count-1)%slides.Count; ShowPage(); }
        private void Next_Click(object sender,RoutedEventArgs e) { if(slides.Count==0) return; page=(page+1)%slides.Count; ShowPage(); }
        private void Background_Click(object sender,RoutedEventArgs e)
        {
            var dialog=new BackgroundDialog(new ImageStore(store.Assets),selected.Background) { Owner=this };
            if(dialog.ShowDialog()==true) { selected.Background=dialog.ResultPath; SaveDraft(); RefreshPreview(); }
        }
        private void Background_DragOver(object sender,DragEventArgs e) { e.Effects=BackgroundDialog.CanImport(e.Data) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled=true; }
        private async void Background_Drop(object sender,DragEventArgs e)
        {
            e.Handled=true; var item=selected;
            try { var path=await BackgroundDialog.ImportData(new ImageStore(store.Assets),e.Data); item.Background=path; SaveDraft(); RefreshPreview(); }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"圖片匯入失敗"); }
        }
        private void Settings_Click(object sender,RoutedEventArgs e)
        {
            var dialog=new SettingsDialog(settings) { Owner=this };
            if(dialog.ShowDialog()==true) { settings=dialog.Settings; store.SaveSettings(settings); RefreshPreview(); }
        }
        private void Font_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(loading || plan==null || !(FontPicker.SelectedItem is BundledFont font)) return;
            plan.FontName=font.Name; SaveDraft(); RefreshPreview();
        }
        private List<SlideData> ExportSlides() { Capture(); SaveDraft(); return SlideLayout.Build(plan,settings); }
        private void Preview_Click(object sender,RoutedEventArgs e)
        {
            try { var all=ExportSlides(); new PreviewWindow(all) { Owner=this }.ShowDialog(); }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"請檢查內容",MessageBoxButton.OK,MessageBoxImage.Information); }
        }
        private string FileName()
        {
            var name=string.Concat((plan.Title ?? "主日崇拜").Select(c=>Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            if(name.Length>70) name=name.Substring(0,70); return plan.Date.ToString("yyyy-MM-dd")+" "+name+".pptx";
        }
        private void Export_Click(object sender,RoutedEventArgs e)
        {
            try
            {
                var all=ExportSlides(); var dialog=new SaveFileDialog { Filter="PowerPoint 簡報 (*.pptx)|*.pptx",FileName=FileName(),AddExtension=true,DefaultExt=".pptx" };
                if(dialog.ShowDialog(this)!=true) return;
                PptxWriter.Write(dialog.FileName,all,plan.Title); StatusText.Text="已製作 "+all.Count+" 張投影片："+Path.GetFileName(dialog.FileName); RefreshHistory();
            }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,"製作失敗",MessageBoxButton.OK,MessageBoxImage.Error); }
        }
    }
}
