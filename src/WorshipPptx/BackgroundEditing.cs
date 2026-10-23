using System;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;

namespace WorshipPptx;

public partial class MainWindow
{
    private bool syncingBackground;
    private void RefreshBackgroundEditor()
    {
        var slide=new SlideData { Background=selected.Background,BackgroundTransform=selected.BackgroundTransform };
        var layout=new PreviewLayout { IsPip=false };
        if(BackgroundHost.Child is CompositionPreview preview) preview.SetSlide(slide,layout,selected.BackgroundTransform);
        else {
            preview=new CompositionPreview(slide,layout,true,selected.BackgroundTransform);
            preview.BackgroundChanged+=BackgroundAdjusted;
            preview.InteractionCompleted+=BackgroundEditCompleted;
            BackgroundHost.Child=preview;
        }
    }
    private void SyncBackgroundControls()
    {
        if(selected==null) return;
        selected.BackgroundTransform ??= new(); selected.BackgroundTransform.Normalize();
        syncingBackground=true;
        try {
            var transform=selected.BackgroundTransform;
            ToolTip.SetTip(ZoomIn,ChineseText.Ui("放大背景")+" · "+Math.Round(transform.Zoom*100)+"%");
            ToolTip.SetTip(ZoomOut,ChineseText.Ui("縮小背景")+" · "+Math.Round(transform.Zoom*100)+"%");
            ZoomIn.IsEnabled=transform.Zoom<4; ZoomOut.IsEnabled=transform.Zoom>1;
            var limit=(transform.Zoom-1)*50;
            PanLeft.IsEnabled=transform.X>-limit; PanRight.IsEnabled=transform.X<limit;
            PanUp.IsEnabled=transform.Y>-limit; PanDown.IsEnabled=transform.Y<limit;
            BackgroundOpacity.Value=selected.BackgroundTransform.Opacity*100;
            OpacityLabel.Text=Math.Round(BackgroundOpacity.Value)+"%";
            FlipBackground.IsChecked=selected.BackgroundTransform.Flip;
        } finally { syncingBackground=false; }
    }
    private void UpdateProjection()
    {
        if(slides.Count>0 && SlideHost.Child is CompositionPreview preview) preview.SetSlide(slides[page],selected.Preview,selected.BackgroundTransform);
    }
    private void BackgroundAdjusted() { SyncBackgroundControls(); UpdateProjection(); Changed(); }
    private void BackgroundEditCompleted() { SaveDraft(); RefreshPreview(); }
    private void ZoomIn_Click(object sender,RoutedEventArgs e) { if(BackgroundHost.Child is CompositionPreview preview) preview.ZoomBy(1.12); }
    private void ZoomOut_Click(object sender,RoutedEventArgs e) { if(BackgroundHost.Child is CompositionPreview preview) preview.ZoomBy(1/1.12); }
    private void PanBackground_Click(object sender,RoutedEventArgs e)
    {
        if(BackgroundHost.Child is not CompositionPreview preview || sender is not Button button) return;
        var direction=button.Tag as string;
        preview.PanBy(direction=="Left" ? -3 : direction=="Right" ? 3 : 0,direction=="Up" ? -3 : direction=="Down" ? 3 : 0);
    }
    private void ResetBackground_Click(object sender,RoutedEventArgs e) { if(BackgroundHost.Child is CompositionPreview preview) preview.ResetBackground(); }
    private void FlipBackground_Click(object sender,RoutedEventArgs e)
    {
        if(loading || selected==null) return;
        selected.BackgroundTransform.Flip=FlipBackground.IsChecked==true; SaveDraft(); RefreshPreview();
    }
    private void BackgroundOpacity_Changed(object sender,RangeBaseValueChangedEventArgs e)
    {
        if(loading || syncingBackground || selected==null) return;
        selected.BackgroundTransform.Opacity=BackgroundOpacity.Value/100;
        SyncBackgroundControls(); Changed();
        RefreshBackgroundEditor(); UpdateProjection();
    }
}
