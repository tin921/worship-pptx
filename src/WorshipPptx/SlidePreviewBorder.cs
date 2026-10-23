using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WorshipPptx;

// Fit the frame to the slide, including when the available height is limiting.
// Keep the border outside the scaled preview so its thickness stays constant.
public sealed class SlidePreviewBorder : Border
{
    public SlidePreviewBorder()
    {
        HorizontalAlignment=HorizontalAlignment.Center;
        VerticalAlignment=VerticalAlignment.Top;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal=BorderThickness.Left+BorderThickness.Right+Padding.Left+Padding.Right;
        var vertical=BorderThickness.Top+BorderThickness.Bottom+Padding.Top+Padding.Bottom;
        var width=Math.Max(0,availableSize.Width-horizontal);
        var height=Math.Max(0,availableSize.Height-vertical);
        var ratio=SlideLayout.Width/SlideLayout.Height;
        width=Math.Min(width,height*ratio);
        if(double.IsPositiveInfinity(width)) width=SlideLayout.Width;
        var fitted=new Size(width+horizontal,width/ratio+vertical);
        base.MeasureOverride(fitted);
        return fitted;
    }
}
