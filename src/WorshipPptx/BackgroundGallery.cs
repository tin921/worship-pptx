using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace WorshipPptx;

public partial class BackgroundDialog
{
    private const double GalleryGap=10, MinimumCardWidth=168, CaptionHeight=28;
    private readonly Dictionary<string,Bitmap> galleryImages=new();
    private readonly List<Button> galleryCards=new();
    private List<MediaAsset> galleryResults=new();
    private int galleryColumns=1, galleryBatch=1, galleryLoaded, galleryRevision;
    private double galleryCardWidth, galleryCardHeight;
    private bool galleryScrolled, galleryQueued, galleryRestoring;
    private bool galleryImageOpen;
    private readonly List<ToggleButton> styleButtons=new();
    private readonly List<Bitmap> styleImages=new();

    private void InitializeGallery()
    {
        foreach(var style in new[]{"photo","illustration","abstract","sculpted","minimal"})
        {
            var sample=new MediaAsset { Style=style };
            var tile=new Grid { ClipToBounds=true };
            tile.Children.Add(StyleSymbols.Create(style));
            var label=new TextBlock { Text=sample.StyleLabel,FontSize=14,FontWeight=FontWeight.Normal,Foreground=Brush.Parse("#52635A") };
            tile.Children.Add(new Border { Background=Brush.Parse("#E6F0F1EF"),CornerRadius=new CornerRadius(3),Padding=new Thickness(3,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Child=label });
            var button=new ToggleButton { Tag=style,Name="Style_"+style,Content=tile,IsChecked=true,Width=60,Height=54,Padding=new Thickness(2),Margin=new Thickness(4,0,0,0),CornerRadius=new CornerRadius(5),ClipToBounds=true,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch };
            button.Classes.Add("alignment"); ToolTip.SetTip(button,sample.StyleLabel); AutomationProperties.SetName(button,sample.StyleLabel);
            button.IsCheckedChanged+=(_,_)=> { if(ready) ResetGallery(); };
            styleButtons.Add(button); StyleFilters.Children.Add(button);
        }
        GalleryScroll.SizeChanged+=(_,_)=>QueueGalleryLayout();
        GalleryScroll.ScrollChanged+=(_,e)=> {
            if(e.ViewportDelta.X!=0 || e.ViewportDelta.Y!=0) QueueGalleryLayout();
            if(!galleryRestoring && !galleryQueued && e.OffsetDelta.Y>0 && NearGalleryEnd()) AppendGallery();
        };
        // Near the end, append another batch before continuing the same gesture.
        GalleryScroll.AddHandler(PointerWheelChangedEvent,Gallery_Wheel,RoutingStrategies.Tunnel);
        GalleryScroll.AddHandler(KeyDownEvent,Gallery_KeyDown,RoutingStrategies.Tunnel);
    }

    private void GalleryToolbar_SizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(!e.WidthChanged) return;
        var compact=e.NewSize.Width<600;
        MatchedFilter.Width=GenericFilter.Width=compact ? 72 : 80;
        var available=e.NewSize.Width-(compact ? 144 : 160)-104;
        var width=Math.Clamp(Math.Floor(available/5)-4,44,60);
        foreach(var button in styleButtons) button.Width=width;
    }

    private void Collection_Click(object sender,RoutedEventArgs e)
    {
        MatchedFilter.IsChecked=sender==MatchedFilter;
        GenericFilter.IsChecked=sender==GenericFilter;
        StyleFilters.IsEnabled=!busy && GenericFilter.IsChecked!=true;
        ResetGallery();
    }
    private void ShowMatchedGallery()
    {
        MatchedFilter.IsChecked=true; GenericFilter.IsChecked=false;
        ResetGallery();
    }

    private void ResetGallery()
    {
        if(!ready) return;
        Gallery.Children.Clear(); galleryCards.Clear(); DisposeGalleryImages();
        var selectedStyles=styleButtons.Where(b=>b.IsChecked==true).Select(b=>(string)b.Tag).ToHashSet();
        IEnumerable<MediaAsset> all=GenericFilter.IsChecked==true
            ? MediaLibrary.All.Where(a=>a.Style=="generic")
            : (results ?? MediaLibrary.All).Where(a=>a.Style!="generic" && selectedStyles.Contains(a.Style));
        galleryResults=all.ToList(); galleryLoaded=0; galleryScrolled=false;
        UpdateGalleryCount();
        GalleryScroll.Offset=default; ReflowGallery(true);
    }

    private void QueueGalleryLayout()
    {
        if(galleryQueued || cancellation.IsCancellationRequested) return;
        galleryQueued=true;
        Dispatcher.UIThread.Post(()=> {
            galleryQueued=false;
            if(!cancellation.IsCancellationRequested) ReflowGallery();
        },DispatcherPriority.Background);
    }

    private void ReflowGallery(bool force=false)
    {
        var width=GalleryScroll.Viewport.Width-Gallery.Margin.Left-Gallery.Margin.Right; var height=GalleryScroll.Viewport.Height;
        if(width<1 || height<1) return;
        var columns=Math.Max(1,(int)Math.Floor((width+GalleryGap)/(MinimumCardWidth+GalleryGap)));
        var cardWidth=Math.Floor((width-GalleryGap*(columns-1))/columns);
        var cardHeight=Math.Ceiling((cardWidth-4)*9/16)+CaptionHeight+4;
        var rows=Math.Max(1,(int)Math.Floor((height+GalleryGap)/(cardHeight+GalleryGap)));
        var batch=columns*rows;
        // One extra row makes the scrollbar visibly useful from the first batch.
        var initial=batch+columns;
        var loaded=Math.Min(galleryResults.Count,galleryScrolled
            ? Math.Max(initial,((galleryLoaded+columns-1)/columns)*columns) : initial);
        if(!force && columns==galleryColumns && cardWidth==galleryCardWidth && batch==galleryBatch && loaded==galleryLoaded) return;
        var firstVisible=(int)Math.Floor(GalleryScroll.Offset.Y/Math.Max(1,galleryCardHeight+GalleryGap))*galleryColumns;
        galleryColumns=columns; galleryCardWidth=cardWidth; galleryCardHeight=cardHeight; galleryBatch=batch; galleryLoaded=loaded;
        Gallery.Children.Clear(); galleryCards.Clear(); AddGalleryCards(0);
        RestoreGalleryOffset(galleryScrolled ? (firstVisible/columns)*(cardHeight+GalleryGap) : 0);
    }

    private bool NearGalleryEnd() => GalleryScroll.Offset.Y+GalleryScroll.Viewport.Height>=GalleryScroll.Extent.Height-galleryCardHeight*.4;

    private bool AppendGallery()
    {
        if(busy || galleryLoaded>=galleryResults.Count || galleryCardWidth<1) return false;
        galleryScrolled=true;
        var previous=galleryLoaded;
        galleryLoaded=Math.Min(galleryResults.Count,galleryLoaded+galleryBatch);
        AddGalleryCards(previous);
        return true;
    }

    private void Gallery_Wheel(object sender,PointerWheelEventArgs e)
    {
        if(e.Delta.Y>=0 || !NearGalleryEnd() || !AppendGallery()) return;
        var nextOffset=GalleryScroll.Offset.Y-e.Delta.Y*72;
        e.Handled=true; RestoreGalleryOffset(nextOffset);
    }

    private void Gallery_KeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key is not (Key.PageDown or Key.End) || !NearGalleryEnd() || !AppendGallery()) return;
        e.Handled=true; RestoreGalleryOffset(GalleryScroll.Offset.Y+GalleryScroll.Viewport.Height);
    }

    private void RestoreGalleryOffset(double y)
    {
        galleryRestoring=true; var revision=++galleryRevision;
        Dispatcher.UIThread.Post(()=> {
            if(revision!=galleryRevision || cancellation.IsCancellationRequested) return;
            GalleryScroll.UpdateLayout();
            GalleryScroll.Offset=new Vector(0,Math.Clamp(y,0,Math.Max(0,GalleryScroll.Extent.Height-GalleryScroll.Viewport.Height)));
            galleryRestoring=false;
        },DispatcherPriority.Background);
    }

    private void AddGalleryCards(int start)
    {
        for(var i=start;i<galleryLoaded;i++)
        {
            var asset=galleryResults[i];
            if(!galleryImages.TryGetValue(asset.Key,out var bitmap))
            {
                using var stream=File.OpenRead(MediaLibrary.FilePath(asset.Still));
                galleryImages[asset.Key]=bitmap=Bitmap.DecodeToWidth(stream,400);
            }
            var content=new Grid { RowDefinitions=new RowDefinitions("*,28"),ClipToBounds=true };
            content.Children.Add(new Image { Source=bitmap,Stretch=Stretch.UniformToFill });
            var caption=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,26"),Margin=new Thickness(7,0) };
            caption.Children.Add(new TextBlock { Text=ChineseText.Ui(asset.Title),FontSize=12,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis });
            var style=new TextBlock { Text=asset.StyleLabel,FontSize=11,Foreground=Brush.Parse("#66716B"),Margin=new Thickness(7,0,0,0),VerticalAlignment=VerticalAlignment.Center };
            Grid.SetColumn(style,1); caption.Children.Add(style);
            var expand=new Button { Name="Expand_"+asset.Id,Content="⛶",Width=24,Height=24,Padding=new Thickness(0),Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,FontSize=18 };
            Grid.SetColumn(expand,2); caption.Children.Add(expand);
            ToolTip.SetTip(expand,ChineseText.Ui("全螢幕檢視 · 雙擊圖片或 Esc 關閉"));
            AutomationProperties.SetName(expand,ChineseText.Ui("全螢幕檢視：")+ChineseText.Ui(asset.Title));
            expand.Click+=async (_,e)=> { e.Handled=true; await ShowGalleryImage(asset); };
            var strip=new Border { Background=Brush.Parse("#F0F1EF"),Child=caption }; Grid.SetRow(strip,1); content.Children.Add(strip);
            var button=new Button { Tag=asset,Content=content,Width=galleryCardWidth,Height=galleryCardHeight,Padding=new Thickness(0),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(6),ClipToBounds=true,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch };
            var label=ChineseText.Ui(asset.Title)+" · "+asset.StyleLabel;
            ToolTip.SetTip(button,label); AutomationProperties.SetName(button,label);
            button.Click+=(_,_)=> { if(!busy) SelectBackground(asset.Key); };
            button.DoubleTapped+=async (_,e)=> { e.Handled=true; await ShowGalleryImage(asset); };
            galleryCards.Add(button); Gallery.Children.Add(button);
        }
        for(var i=0;i<galleryCards.Count;i++)
            galleryCards[i].Margin=new Thickness(0,0,(i+1)%galleryColumns==0 ? 0 : GalleryGap,i/galleryColumns==(galleryCards.Count-1)/galleryColumns ? 0 : GalleryGap);
        HighlightSelectedBackground();
        UpdateGalleryCount();
        ToolTip.SetTip(GalleryInfo,ChineseText.Ui(galleryLoaded<galleryResults.Count ? "向下捲動查看更多" : "已顯示全部"));
    }

    private void UpdateGalleryCount() => GalleryInfo.Text=galleryLoaded+" / "+galleryResults.Count;

    private async Task ShowGalleryImage(MediaAsset asset)
    {
        if(busy || galleryImageOpen) return;
        galleryImageOpen=true;
        try { await new BackgroundImageWindow(asset).ShowDialog(this); }
        finally { galleryImageOpen=false; }
    }

    private void HighlightSelectedBackground()
    {
        foreach(var button in galleryCards)
            button.BorderBrush=Brush.Parse(((MediaAsset)button.Tag).Key==ResultPath ? "#28543E" : "#DEE4DC");
    }

    private void DisposeGalleryImages()
    {
        foreach(var bitmap in galleryImages.Values) bitmap.Dispose();
        galleryImages.Clear();
    }

    private void DisposeGallery()
    {
        Gallery.Children.Clear(); galleryCards.Clear(); DisposeGalleryImages();
        foreach(var bitmap in styleImages) bitmap.Dispose();
        styleImages.Clear();
    }
}
