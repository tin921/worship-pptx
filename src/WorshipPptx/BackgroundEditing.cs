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
        => Avalonia.Threading.Dispatcher.UIThread.Post(PositionBackgroundControls,Avalonia.Threading.DispatcherPriority.Background);
    private void PositionBackgroundControls()
    {
        if(ImageControls==null || SlideHost?.Parent is not Control surface) return;
        var picture=SlideHost.Bounds;
        ImageControls.Margin=new Thickness(picture.Left+10,0,0,Math.Max(0,surface.Bounds.Height-picture.Bottom)+10);
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
