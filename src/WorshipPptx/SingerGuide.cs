using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WorshipPptx;

// A preview-only camera illustration. The composition editor supplies its bounds.
public static class SingerGuide
{
    private static readonly Lazy<Bitmap> illustration=new(()=> {
        using var stream=AssetLoader.Open(new Uri("avares://WorshipPptx/Assets/worship-leader-guitar.png"));
        return Bitmap.DecodeToWidth(stream,1280);
    });

    public static Border Create()
    {
        var art=new Grid { Width=1280,Height=720 };
        art.Children.Add(new Image { Source=illustration.Value,Stretch=Stretch.UniformToFill });
        art.Children.Add(new Border {
            VerticalAlignment=VerticalAlignment.Bottom,Background=Brush.Parse("#ECFFFFFF"),Padding=new Thickness(0,4),
            Child=new TrText("攝影機位置示意 · 不會匯出") { FontSize=16,Foreground=Brush.Parse("#52616B"),TextAlignment=TextAlignment.Center }
        });
        return new Border { Name="SingerGuide",Width=1280,Height=720,ClipToBounds=true,Child=art };
    }
}
