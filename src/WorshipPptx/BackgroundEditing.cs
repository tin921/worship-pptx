using System;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Interactivity;

namespace WorshipPptx;

public partial class MainWindow
{
    private bool syncingBackground;
    private void Preview_SizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(ImageControls==null) return;
        var scale=Math.Min(e.NewSize.Width/1280,e.NewSize.Height/720);
        ImageControls.Margin=new Thickness((e.NewSize.Width-1280*scale)/2+10,0,0,(e.NewSize.Height-720*scale)/2+10);
    }
    private void SyncBackgroundControls()
    {
        if(selected==null) return;
        selected.BackgroundTransform ??= new(); selected.BackgroundTransform.Normalize();
        syncingBackground=true;
        try {
            ZoomLabel.Content=Math.Round(selected.BackgroundTransform.Zoom*100)+"%";
            BackgroundOpacity.Value=selected.BackgroundTransform.Opacity*100;
            OpacityLabel.Text=Math.Round(BackgroundOpacity.Value)+"%";
            FlipBackground.IsChecked=selected.BackgroundTransform.Flip;
        } finally { syncingBackground=false; }
    }
    private void BackgroundAdjusted() { SyncBackgroundControls(); Changed(); }
    private void BackgroundEditCompleted() { SaveDraft(); RefreshPreview(); }
    private void ZoomIn_Click(object sender,RoutedEventArgs e) { if(SlideHost.Child is CompositionPreview preview) preview.ZoomBy(1.12); }
    private void ZoomOut_Click(object sender,RoutedEventArgs e) { if(SlideHost.Child is CompositionPreview preview) preview.ZoomBy(1/1.12); }
    private void ResetBackground_Click(object sender,RoutedEventArgs e) { if(SlideHost.Child is CompositionPreview preview) preview.ResetBackground(); }
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
        if(slides.Count>0 && SlideHost.Child is CompositionPreview preview) preview.SetSlide(slides[page],selected.Preview,selected.BackgroundTransform);
    }
}
