using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Avalonia.Automation;
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
        private bool exporting;
        private readonly DispatcherTimer debounce = new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(650) };
        private List<SlideData> slides = new List<SlideData>();
        private int page;
        private bool updatingThumbnails;
        private ColumnDefinition EditorColumn => WorkArea.ColumnDefinitions[0];
        private ColumnDefinition PreviewColumn => WorkArea.ColumnDefinitions[2];
        private ColumnDefinition ThumbnailColumn => WorkArea.ColumnDefinitions[4];
        private Point tabDragStart;
        private TabItem pressedTab,draggingTab;
        private TabItem addTab;
        private PointerPressedEventArgs tabPress;
        private static readonly DataFormat<TabItem> TabDragFormat=DataFormat.CreateInProcessFormat<TabItem>("WorshipPptx.ServiceTab");
        public MainWindow(LocalStore store)
        {
            loading=true; this.store=store; settings=store.LoadSettings();
            ChineseText.SetLanguage(settings.Language); InitializeComponent(); FontPicker.ItemsSource=FontCatalog.All;
            VersionLabel.Text="v"+typeof(MainWindow).Assembly.GetName().Version.ToString(3);
            UpdateLanguageButtons();
            BuildColorPalette();
            InitializeNavigation();
            FontSizePicker.ItemsSource=fontSizes;
            var history=store.LoadHistory(); plan=history.FirstOrDefault() ?? Demo.Create();
            debounce.Tick += (s,e) => { debounce.Stop(); SaveDraft(); RefreshPreview(); };
            LoadPlan(); RefreshHistory(); loading=false;
            RefreshPreview(); SaveDraft();
            if(!string.IsNullOrEmpty(store.RecoveryWarning)) StatusText.Text=store.RecoveryWarning;
            Closing += OnClosing;
            SongTabs.AddHandler(PointerPressedEvent,Tabs_MouseDown,RoutingStrategies.Tunnel,true);
            SongTabs.AddHandler(PointerMovedEvent,Tabs_MouseMove,RoutingStrategies.Tunnel,true);

            KeyDown += (s,e) => { if(e.Key==Key.F5) { Preview_Click(s,e); e.Handled=true; } if(e.Key==Key.S && (e.KeyModifiers & (KeyModifiers.Control|KeyModifiers.Meta))!=0) { SaveDraft(); e.Handled=true; } };
        }
        private void LoadPlan()
        {
            loading=true; ServiceTitle.Text=plan.Title; ServiceDate.SelectedDate=plan.Date;
            foreach(var item in plan.Items) { item.Preview=new PreviewLayout(); item.Formatting ??= new(); item.Formatting.FullSlide=false; }
            SongTabs.Items.Clear(); foreach(var item in plan.Items) SongTabs.Items.Add(new TabItem { Header=item.Title, Tag=item });
            addTab=new TabItem { Header="＋", Classes={"add-tab"} };
            AutomationProperties.SetName(addTab,ChineseText.Ui("新增詩歌分頁")); ToolTip.SetTip(addTab,ChineseText.Ui("新增詩歌")); SongTabs.Items.Add(addTab);
            var firstSong=plan.Items.FindIndex(i=>SlideLayout.HasSectionTags(i.Lyrics)); SongTabs.SelectedIndex=Math.Max(0,firstSong);
            selected=plan.Items[SongTabs.SelectedIndex]; FillEditor(); loading=false; page=0;
        }
        private void FormattingControls_SizeChanged(object sender,SizeChangedEventArgs e)
        {
            if(!e.WidthChanged) return;
            // Keep both alignment rows together when the splitter leaves too little room beside them.
            var compact=e.NewSize.Width<198;
            TextColorPicker.Width=compact ? 42 : 56;
            TextColorPicker.Padding=new Thickness(compact ? 4 : 7);
            TextColorPicker.Margin=new Thickness(0,0,compact ? 4 : 6,6);
            FontSizePicker.Width=compact ? 60 : 64;
            FontSizePicker.Margin=new Thickness(0,0,compact ? 4 : 6,6);
            BoldToggle.Width=compact ? 32 : 36;
            BoldToggle.Margin=new Thickness(0,0,compact ? 0 : 6,6);
            Grid.SetColumnSpan(TypographyRow,compact ? 2 : 1);
            TypographyRow.Margin=new Thickness(0,0,compact ? 0 : 8,0);
            Grid.SetColumn(AlignmentControls,compact ? 0 : 1);
            Grid.SetColumnSpan(AlignmentControls,compact ? 2 : 1);
            Grid.SetRow(AlignmentControls,compact ? 1 : 0);
        }
        private void WorkArea_SizeChanged(object sender,SizeChangedEventArgs e)
        {
            if(!e.WidthChanged) return;
            // Keep the preview usable when a window is narrowed after dragging either divider.
            var excess=EditorColumn.ActualWidth+ThumbnailColumn.ActualWidth+16+PreviewColumn.MinWidth-e.NewSize.Width;
            if(excess<=0) return;
            var shrink=Math.Min(excess,Math.Max(0,EditorColumn.ActualWidth-EditorColumn.MinWidth));
            if(shrink>0) EditorColumn.Width=new GridLength(EditorColumn.ActualWidth-shrink);
            excess-=shrink;
            if(excess>0) ThumbnailColumn.Width=new GridLength(Math.Max(ThumbnailColumn.MinWidth,ThumbnailColumn.ActualWidth-excess));
        }
        private void FillEditor()
        {
            SongTitle.Text=selected.Title; Lyrics.Text=selected.Lyrics; SongOrder.Text=selected.Order;
            CopyrightText.Text=selected.Copyright; SongId.Text=selected.SongId;
            UpdateContentMode();
            selected.Formatting ??= new SlideFormatting();
            FontPicker.SelectedItem=FontCatalog.Get(selected.Formatting.FontName);
            SyncFontSizePicker();
            BoldToggle.IsChecked=selected.Formatting.Bold;
            selected.Preview ??= PreviewLayout.FromLegacy(selected.Formatting.FullSlide);
            UpdateAlignmentButtons(); UpdatePositionButtons(); UpdateColorSwatch(); UpdateLayoutButtons(); SyncBackgroundControls();
        }
        private void UpdateContentMode()
        {
            var song=SlideLayout.HasSectionTags(Lyrics.Text);
            var announcement=SlideLayout.HasAnnouncementTags(Lyrics.Text);
            SongTitle.PlaceholderText=song ? ChineseText.Ui("詩歌名稱") : ChineseText.Ui("經文／公告名稱");
            Lyrics.PlaceholderText=ChineseText.Ui("歌詞 [v1]、[c] · 公告 [1]、[2] · 經文直接輸入");
            OrderPanel.IsVisible=song; SongId.IsEnabled=song;
            LyricsHint.Text=song ? ChineseText.Ui(selected.Formatting.AutoFontSize ? "[v1]、[c] 每段一頁，自動調整字號；Ctrl+Tab 插入縮排。" : "[v1]、[c] 標記段落；固定字號，自動分頁；Ctrl+Tab 插入縮排。") : announcement ? ChineseText.Ui("[1]、[2] 每項一頁；字號為上限，長文自動縮小；Ctrl+Tab 插入縮排。") : ChineseText.Ui("經文自動分頁；公告用 [1]、[2] 每項一頁。保留空行及縮排；Ctrl+Tab 插入縮排。");
            ToolTip.SetTip(FontSizePicker,song || announcement ? ChineseText.Ui("最大字號（pt）；每段文字自動縮小至一頁") : ChineseText.Ui("本分頁字號（pt）"));
        }
        private void Capture()
        {
            if(loading || selected==null) return;
            plan.Title=ServiceTitle.Text; plan.Date=ServiceDate.SelectedDate ?? plan.Date;
            selected.Title=SongTitle.Text; selected.Lyrics=Lyrics.Text; selected.Order=SongOrder.Text; selected.Copyright=CopyrightText.Text; selected.SongId=SongId.Text;
            if(SongTabs.SelectedItem is TabItem tab && tab.Tag is ServiceItem) tab.Header=selected.Title;
            plan.NotifyTabsChanged();
        }
        private void Changed()
        {
            if(loading) return; Capture(); dirty=true; StatusText.Text=ChineseText.Ui("正在儲存…"); debounce.Stop(); debounce.Start();
        }
        private void Editor_Changed(object sender,TextChangedEventArgs e) { Changed(); if(!loading && sender==Lyrics) UpdateContentMode(); }
        private void Metadata_Changed(object sender,TextChangedEventArgs e) => Changed();
        private void Date_Changed(object sender,SelectionChangedEventArgs e) { Changed(); if(!loading) RefreshHistory(); }
        private void SaveDraft()
        {
            if(plan==null) return;
            try { Capture(); store.Save(plan); dirty=false; StatusText.Text=plan.Items.Count+ChineseText.Ui(" 個項目 · ")+plan.Items.Count(i=>SlideLayout.HasSectionTags(i.Lyrics))+ChineseText.Ui(" 首詩歌 · 已儲存 ")+DateTime.Now.ToString("HH:mm"); }
            catch(Exception ex) { dirty=true; StatusText.Text=ChineseText.Ui("未能儲存：")+ex.Message; }
        }
        private async void OnClosing(object sender,WindowClosingEventArgs e)
        {
            if(exporting) { e.Cancel=true; StatusText.Text=ChineseText.Ui("正在製作 PowerPoint，完成後便可關閉。"); return; }
            debounce.Stop(); SaveDraft();
            if(dirty) { e.Cancel=true; await Dialogs.Show(this,ChineseText.Ui("未能儲存本次修改。請先檢查磁碟空間或資料夾權限，再關閉程式。"),ChineseText.Ui("儲存失敗")); }
        }
        private void RefreshHistory()
        {
            var prior=loading; loading=true;
            var items=store.LoadHistory().Select(p=>p.Id==plan.Id ? plan : p).OrderByDescending(p=>p.Date.Date).ThenByDescending(p=>p.Updated).ToList(); HistoryList.ItemsSource=items; HistoryList.SelectedItem=items.FirstOrDefault(p=>p.Id==plan.Id); loading=prior;
        }
        private void History_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(loading || !(HistoryList.SelectedItem is ServicePlan next)) return;
            debounce.Stop(); SaveDraft(); if(dirty) return; plan=next; LoadPlan(); RefreshPreview();
        }
        private void Tab_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(loading || !(SongTabs.SelectedItem is TabItem tab)) return;
            if(tab==addTab) { AddSong_Click(sender,e); return; }
            debounce.Stop(); // TextChanged already copied the previous item's fields.
            selected=(ServiceItem)tab.Tag; loading=true; FillEditor(); loading=false; page=0; SaveDraft(); RefreshPreview();
        }
        private TabItem TabAt(object source) => (source as Visual)?.GetSelfAndVisualAncestors().OfType<TabItem>().FirstOrDefault();
        private void Tabs_MouseDown(object sender,PointerPressedEventArgs e)
        {
            if(!e.GetCurrentPoint(SongTabs).Properties.IsLeftButtonPressed) return;
            pressedTab=TabAt(e.Source); if(pressedTab?.Tag is not ServiceItem) pressedTab=null;
            tabDragStart=e.GetPosition(SongTabs); tabPress=e;
        }
        private async void Tabs_MouseMove(object sender,PointerEventArgs e)
        {
            if(!e.GetCurrentPoint(SongTabs).Properties.IsLeftButtonPressed) { pressedTab=null; return; }
            if(pressedTab==null || draggingTab!=null || tabPress==null) return;
            var delta=e.GetPosition(SongTabs)-tabDragStart;
            if(Math.Abs(delta.X)<6 && Math.Abs(delta.Y)<6) return;
            draggingTab=pressedTab;
            using var data=new DataTransfer(); data.Add(DataTransferItem.Create(TabDragFormat,draggingTab));
            try { await DragDrop.DoDragDropAsync(tabPress,data,DragDropEffects.Move); }
            finally { ClearTabMarker(); draggingTab=null; pressedTab=null; tabPress=null; }
        }
        private bool IsOwnTabDrag(DragEventArgs e) => draggingTab!=null && e.DataTransfer.TryGetValue(TabDragFormat)==draggingTab && SongTabs.Items.Contains(draggingTab);
        private int TabInsertion(DragEventArgs e,out TabItem target,out bool after)
        {
            target=TabAt(e.Source); after=target==null || e.GetPosition(target).X>target.Bounds.Width/2;
            if(target==null || target==addTab) { target=SongTabs.Items.OfType<TabItem>().LastOrDefault(t=>t.Tag is ServiceItem); after=true; return plan.Items.Count; }
            return SongTabs.Items.IndexOf(target)+(after ? 1 : 0);
        }
        private void Tabs_DragOver(object sender,DragEventArgs e)
        {
            e.Handled=true; e.DragEffects=DragDropEffects.None;
            if(!IsOwnTabDrag(e)) return;
            e.DragEffects=DragDropEffects.Move;
            TabInsertion(e,out var target,out var after); ClearTabMarker();
            target?.Classes.Add(after ? "drop-after" : "drop-before");
            var x=e.GetPosition(TabScroll).X;
            if(x<30) TabScroll.Offset=new Vector(Math.Max(0,TabScroll.Offset.X-16),0);
            else if(x>TabScroll.Bounds.Width-30) TabScroll.Offset=new Vector(TabScroll.Offset.X+16,0);
        }
        private void Tabs_DragLeave(object sender,DragEventArgs e) => ClearTabMarker();
        private void ClearTabMarker()
        {
            foreach(var tab in SongTabs.Items.OfType<TabItem>()) { tab.Classes.Remove("drop-before"); tab.Classes.Remove("drop-after"); }
        }
        private void Tabs_Drop(object sender,DragEventArgs e)
        {
            e.Handled=true; e.DragEffects=DragDropEffects.None; ClearTabMarker();
            if(!IsOwnTabDrag(e)) return;
            var insertion=TabInsertion(e,out _,out _);
            Capture(); debounce.Stop(); var active=SongTabs.SelectedItem;
            if(plan.MoveItem((ServiceItem)draggingTab.Tag,insertion))
            {
                loading=true;
                try { SongTabs.Items.Remove(draggingTab); SongTabs.Items.Insert(plan.Items.IndexOf((ServiceItem)draggingTab.Tag),draggingTab); SongTabs.SelectedItem=active; }
                finally { loading=false; }
                SaveDraft(); RefreshHistory();
            }
            e.DragEffects=DragDropEffects.Move;
        }
        private void NewService_Click(object sender,RoutedEventArgs e)
        {
            SaveDraft(); if(dirty) return;
            plan=Demo.Create(); LoadPlan(); SaveDraft(); RefreshHistory(); RefreshPreview();
        }
        private void AddSong_Click(object sender,RoutedEventArgs e)
        {
            Capture(); selected=new ServiceItem(); plan.Items.Add(selected);
            loading=true; var tab=new TabItem { Header=selected.Title,Tag=selected }; SongTabs.Items.Insert(SongTabs.Items.Count-1,tab); SongTabs.SelectedItem=tab; FillEditor(); loading=false; page=0; SaveDraft(); RefreshPreview();
            Dispatcher.UIThread.Post(()=>tab.BringIntoView());
        }
        private async void RemoveItem_Click(object sender,RoutedEventArgs e)
        {
            if(plan.Items.Count==1) { await Dialogs.Show(this,ChineseText.Ui("崇拜安排至少需要一個項目。")); return; }
            if(!await Dialogs.Show(this,ChineseText.Ui("移除「")+selected.Title+"」？", ChineseText.Ui("移除項目"),true)) return;
            plan.Items.Remove(selected); LoadPlan(); SaveDraft(); RefreshPreview();
        }
        private void RefreshPreview()
        {
            try
            {
                Capture(); SyncBackgroundControls(); slides=SlideLayout.BuildItem(selected,settings); ValidationText.Text="";
                RefreshBackgroundEditor();
                updatingThumbnails=true; Thumbnails.Items.Clear();
                for(var i=0;i<slides.Count;i++)
                {
                    var thumb=new Grid(); thumb.Children.Add(new Viewbox { Stretch=Stretch.Uniform,Child=SlideVisual.Create(slides[i],false,false) });
                    thumb.Children.Add(new Border { Name="ThumbnailTag",HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(4),Padding=new Thickness(5,2),Background=Brush.Parse("#ECEEEC"),Child=new TextBlock { Text=(i+1)+" · "+SlideVisual.TagLabel(slides[i]),FontSize=12,Foreground=Brush.Parse("#38483E") } });
                    var item=new ListBoxItem { Content=thumb }; ToolTip.SetTip(item,slides[i].Text);
                    AutomationProperties.SetName(item,(i+1)+" · "+slides[i].Section+" · "+slides[i].Text);
                    Thumbnails.Items.Add(item);
                }
                updatingThumbnails=false; page=Math.Min(page,Math.Max(0,slides.Count-1)); ShowPage();
            }
            catch(Exception ex) { updatingThumbnails=true; slides.Clear(); SlideHost.Child=null; Thumbnails.Items.Clear(); PreviewInfo.Text=ChineseText.Ui("請先完成內容"); ValidationText.Text=ex.Message; try { RefreshBackgroundEditor(); } catch { BackgroundHost.Child=null; } }
            finally { updatingThumbnails=false; }
        }
        private void ShowPage()
        {
            if(slides.Count==0) return;
            if(SlideHost.Child is CompositionPreview preview) preview.SetSlide(slides[page],selected.Preview,selected.BackgroundTransform);
            else {
                preview=new CompositionPreview(slides[page],selected.Preview,false,selected.BackgroundTransform);
                SlideHost.Child=preview;
            }
            PreviewInfo.Text=(page+1)+" / "+slides.Count+"  ·  "+slides[page].Section;
            if(SlideLayout.HasAnnouncementTags(selected.Lyrics) || SlideLayout.HasSectionTags(selected.Lyrics)) PreviewInfo.Text+="  ·  "+slides[page].FontSize+" pt";
            SyncFontSizePicker(slides[page].FontSize);
            if(Thumbnails.SelectedIndex!=page) Thumbnails.SelectedIndex=page;
            Thumbnails.ScrollIntoView(Thumbnails.SelectedItem);
        }
        private void Thumbnail_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(updatingThumbnails || Thumbnails.SelectedIndex<0) return;
            page=Thumbnails.SelectedIndex; ShowPage();
        }
        private void Previous_Click(object sender,RoutedEventArgs e) { if(slides.Count==0) return; page=(page+slides.Count-1)%slides.Count; ShowPage(); }
        private void Next_Click(object sender,RoutedEventArgs e) { if(slides.Count==0) return; page=(page+1)%slides.Count; ShowPage(); }
        private async void Background_Click(object sender,RoutedEventArgs e)
        {
            Capture();
            var dialog=new BackgroundDialog(new ImageStore(store.Assets),selected.Background,selected.Title,selected.Lyrics);
            if(await dialog.ShowDialog<bool>(this)) { if(selected.Background!=dialog.ResultPath) selected.BackgroundTransform=new(); selected.Background=dialog.ResultPath; SyncBackgroundControls(); SaveDraft(); RefreshPreview(); }
        }
        private void Background_DragOver(object sender,DragEventArgs e) { e.DragEffects=BackgroundDialog.CanImport(e.DataTransfer) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled=true; }
        private async void Background_Drop(object sender,DragEventArgs e)
        {
            e.Handled=true; var item=selected; var targetPlan=plan;
            try
            {
                var path=await BackgroundDialog.ImportData(new ImageStore(store.Assets),e.DataTransfer);
                item.Background=path; item.BackgroundTransform=new();
                if(plan==targetPlan) { SaveDraft(); RefreshPreview(); } else store.Save(targetPlan);
            }
            catch(Exception ex) { await Dialogs.Show(this,ex.Message,ChineseText.Ui("圖片匯入失敗")); }
        }
        private async void Settings_Click(object sender,RoutedEventArgs e)
        {
            var dialog=new SettingsDialog(settings);
            if(await dialog.ShowDialog<bool>(this)) { settings=dialog.Settings; store.SaveSettings(settings); RefreshPreview(); }
        }
        private void UpdateLanguageButtons()
        {
            TraditionalButton.IsChecked=!ChineseText.IsSimplified;
            SimplifiedButton.IsChecked=ChineseText.IsSimplified;
        }
        private async void Language_Click(object sender,RoutedEventArgs e)
        {
            if(loading || exporting || sender is not ToggleButton button) return;
            var language=(string)button.Tag;
            if(language==ChineseText.Current.Language) { UpdateLanguageButtons(); return; }
            debounce.Stop(); SaveDraft();
            if(dirty) { UpdateLanguageButtons(); return; }
            try
            {
                // Commit the preference before changing the view. The source strings in
                // every service stay untouched, including the currently edited draft.
                var next=new UserSettings { ChurchLicense=settings.ChurchLicense,Language=language };
                store.SaveSettings(next); settings=next;
                loading=true;
                ChineseText.SetLanguage(language); UpdateLanguageButtons();
                plan.NotifyLanguageChanged(); ServiceTitle.Text=plan.Title;
                foreach(var tab in SongTabs.Items.OfType<TabItem>()) if(tab.Tag is ServiceItem item) tab.Header=item.Title;
                AutomationProperties.SetName(addTab,ChineseText.Ui("新增詩歌分頁")); ToolTip.SetTip(addTab,ChineseText.Ui("新增詩歌"));
                FontPicker.ItemsSource=null; FontPicker.ItemsSource=FontCatalog.All;
                ColorSwatches.Children.Clear(); BuildColorPalette();
                FillEditor(); RefreshHistory();
            }
            catch(Exception ex) { await Dialogs.Show(this,ex.Message,ChineseText.Ui("未能切換語言")); }
            finally { loading=false; UpdateLanguageButtons(); }
            RefreshPreview(); SaveDraft();
        }
        private void Typography_Changed(object sender,SelectionChangedEventArgs e)
        {
            if(loading || syncingFontSize || selected==null || !(FontPicker.SelectedItem is FontChoice font)) return;
            selected.Formatting.FontName=font.Name;
            if(sender==FontSizePicker) {
                if(FontSizePicker.SelectedItem is double size) { selected.Formatting.AutoFontSize=false; selected.Formatting.FontSize=size; }
                else return;
            }
            UpdateContentMode();
            SaveDraft(); RefreshPreview();
        }
        private void Alignment_Click(object sender,RoutedEventArgs e)
        {
            if(loading || plan==null || sender is not ToggleButton button) return;
            selected.Formatting.Alignment=(string)button.Tag;
            UpdateAlignmentButtons(); SaveDraft(); RefreshPreview();
        }
        private void UpdateAlignmentButtons()
        {
            AlignLeft.IsChecked=selected.Formatting.Alignment=="Left";
            AlignCenter.IsChecked=selected.Formatting.Alignment=="Center";
            AlignRight.IsChecked=selected.Formatting.Alignment=="Right";
        }
        private void Position_Click(object sender,RoutedEventArgs e)
        {
            if(loading || selected==null || sender is not ToggleButton button) return;
            selected.Formatting.TextPosition=(string)button.Tag;
            UpdatePositionButtons(); SaveDraft(); RefreshPreview();
        }
        private void UpdatePositionButtons()
        {
            PositionTop.IsChecked=selected.Formatting.TextPosition=="Top";
            PositionMiddle.IsChecked=selected.Formatting.TextPosition=="Middle";
            PositionBottom.IsChecked=selected.Formatting.TextPosition=="Bottom";
        }
        private void BuildColorPalette()
        {
            foreach(var color in TextColors.All) {
                var swatch=new Button { Width=32,Height=32,Padding=new Thickness(3),Margin=new Thickness(2),Tag=color,Content=new Border { Width=24,Height=24,Background=color.Brush,BorderBrush=Brush.Parse("#94A197"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3) } };
                ToolTip.SetTip(swatch,color.Name); AutomationProperties.SetName(swatch,color.Name);
                swatch.Click+=(_,_)=> { if(loading || selected==null) return; selected.Formatting.TextColor=color.Hex; UpdateColorSwatch(); TextColorPicker.Flyout.Hide(); SaveDraft(); RefreshPreview(); };
                ColorSwatches.Children.Add(swatch);
            }
        }
        private void UpdateColorSwatch()
        {
            var color=TextColors.Get(selected.Formatting.TextColor); SelectedColorSwatch.Background=color.Brush;
            foreach(var button in ColorSwatches.Children.OfType<Button>()) button.BorderBrush=((TextColorChoice)button.Tag).Hex==color.Hex ? Brush.Parse("#28543E") : Brushes.Transparent;
        }
        private void FormatOption_Click(object sender,RoutedEventArgs e)
        {
            if(loading || selected==null) return;
            selected.Formatting.Bold=BoldToggle.IsChecked==true;
            SaveDraft(); RefreshPreview();
        }
        private void UpdateLayoutButtons() => LayoutModeLabel.Text=ChineseText.Ui("16:9 · 上半部文字");
        private List<SlideData> ExportSlides() { Capture(); SaveDraft(); return SlideLayout.Build(plan,settings); }
        private async void Preview_Click(object sender,RoutedEventArgs e)
        {
            try { var all=ExportSlides(); await new PreviewWindow(all).ShowDialog(this); }
            catch(Exception ex) { await Dialogs.Show(this,ex.Message,ChineseText.Ui("請檢查內容")); }
        }
        private string FileName()
        {
            var name=string.Concat((plan.Title ?? ChineseText.Ui("主日崇拜")).Select(c=>Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            if(name.Length>70) name=name.Substring(0,70); return plan.Date.ToString("yyyy-MM-dd")+" "+name+".pptx";
        }
        private async void Export_Click(object sender,RoutedEventArgs e)
        {
            if(exporting) return;
            exporting=true; LanguagePanel.IsEnabled=false; var button=sender as Button; if(button!=null) button.IsEnabled=false;
            try
            {
                var all=ExportSlides(); var title=plan.Title;
                var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title=ChineseText.Ui("製作 PowerPoint"), SuggestedFileName=FileName(), DefaultExtension="pptx", ShowOverwritePrompt=true, FileTypeChoices=new[]{new FilePickerFileType(ChineseText.Ui("PowerPoint 簡報")) { Patterns=new[]{"*.pptx"} }} });
                if(file==null) return;
                StatusText.Text=ChineseText.Ui("正在製作 PowerPoint…");
                var path=file.TryGetLocalPath();
                if(path!=null) await Task.Run(()=>PptxWriter.Write(path,all,title));
                else
                {
                    var temp=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pptx");
                    try { await Task.Run(()=>PptxWriter.Write(temp,all,title)); using var input=File.OpenRead(temp); using var output=await file.OpenWriteAsync(); await input.CopyToAsync(output); }
                    finally { if(File.Exists(temp)) File.Delete(temp); }
                }
                StatusText.Text=ChineseText.Ui("已製作 ")+all.Count+ChineseText.Ui(" 張投影片：")+file.Name; RefreshHistory();
            }
            catch(Exception ex) { await Dialogs.Show(this,ex.Message,ChineseText.Ui("製作失敗")); }
            finally { exporting=false; LanguagePanel.IsEnabled=true; if(button!=null) button.IsEnabled=true; }
        }
    }
}
