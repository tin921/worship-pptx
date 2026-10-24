using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WorshipPptx;

// A preview-only camera illustration. The composition editor supplies its bounds.
public static class PresenterGuide
{
    private static readonly Lazy<Bitmap> musician=new(()=>Load("worship-leader-guitar.png"));
    private static readonly Lazy<Bitmap> reader=new(()=>Load("worship-leader-reading.png"));
    private static Bitmap Load(string name)
    {
        using var stream=AssetLoader.Open(new Uri("avares://WorshipPptx/Assets/"+name));
        return Bitmap.DecodeToWidth(stream,1280);
    }

    public static Border Create(bool isSong)
    {
        var art=new Grid { Width=1280,Height=720 };
        art.Children.Add(new Image { Source=isSong ? musician.Value : reader.Value,Stretch=Stretch.UniformToFill });
        art.Children.Add(new Border {
            VerticalAlignment=VerticalAlignment.Bottom,Background=Brush.Parse("#ECFFFFFF"),Padding=new Thickness(0,4),
            Child=new TrText("攝影機位置示意 · 不會匯出") { FontSize=16,Foreground=Brush.Parse("#52616B"),TextAlignment=TextAlignment.Center }
        });
        return new Border { Name="PresenterGuide",Tag=isSong ? "Music" : "Reading",Width=1280,Height=720,ClipToBounds=true,Child=art };
    }
}
