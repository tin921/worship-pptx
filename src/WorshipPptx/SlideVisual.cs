using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace WorshipPptx
{
    public static class SlideVisual
    {
        private static readonly Dictionary<string, WeakReference<Bitmap>> cache = new();
        public static Bitmap DefaultImage => Background("");
        public static Bitmap Background(string path)
        {
            var key=path ?? "";
            if(cache.TryGetValue(key,out var reference) && reference.TryGetTarget(out var image)) return image;
            using var stream=new MemoryStream(BackgroundPng(key));
            var result=new Bitmap(stream); cache[key]=new WeakReference<Bitmap>(result); return result;
        }
        public static Image CreateBackground(string key,BackgroundTransform transform=null)
        {
            var image=new Image { Source=Background(key),Stretch=Stretch.Fill,RenderTransformOrigin=RelativePoint.Center };
            UpdateBackground(image,transform ?? new()); return image;
        }
        public static void UpdateBackground(Image image,BackgroundTransform value)
        {
            value.Normalize(); image.Width=1280*value.Zoom; image.Height=720*value.Zoom;
            Canvas.SetLeft(image,(1280-image.Width)/2+value.X*12.8);
            Canvas.SetTop(image,(720-image.Height)/2+value.Y*7.2);
            image.Opacity=value.Opacity; image.RenderTransform=new ScaleTransform(value.Flip ? -1 : 1,1);
        }
        public static string TagLabel(SlideData slide) => "["+(slide.Section ?? "")+"]";
        public static double TagWidth(SlideData slide) => Math.Ceiling(SlideLayout.Measure(TagLabel(slide),20,slide.FontName))+24;
        public static Canvas Create(SlideData slide,bool showCameraGuide=false,bool showTag=true)
        {
            if(showCameraGuide) return new CompositionPreview(slide,slide.Preview ?? PreviewLayout.FromLegacy(slide.FullSlide),false);
            var canvas=new Canvas { Width=1280, Height=720, ClipToBounds=true, Background=Brushes.Black };
            canvas.Children.Add(CreateBackground(slide.Background,slide.BackgroundTransform));
            var color=TextColors.Get(slide.TextColor).Brush;
            var spacing=SlideLayout.LineSpacing(slide.FontSize,slide.FontName,slide.Bold);
            var alignment=Enum.Parse<TextAlignment>(slide.Alignment);
            for(var i=0;i<slide.Lines.Count;i++)
            {
                var text=new TextBlock { Text=slide.Lines[i], Width=SlideLayout.TextWidth, Height=spacing, FontFamily=FontCatalog.Get(slide.FontName).Family, FontSize=SlideLayout.FontPixels(slide.FontSize), FontWeight=slide.Bold ? FontWeight.Bold : FontWeight.Normal, Foreground=color, TextAlignment=alignment, TextWrapping=TextWrapping.NoWrap };
                Canvas.SetLeft(text,SlideLayout.TextX); Canvas.SetTop(text,SlideLayout.TextTop(slide)+i*spacing); canvas.Children.Add(text);
            }
            var credits=SlideLayout.FooterLines(slide);
            for(var i=0;i<credits.Count;i++)
            {
                var footer=new TextBlock { Text=credits[i], Width=SlideLayout.TextWidth, Height=SlideLayout.FooterLineSpacing(slide), FontFamily=FontCatalog.Get(slide.FontName).Family, FontSize=SlideLayout.FooterSize(slide), FontWeight=slide.Bold ? FontWeight.Bold : FontWeight.Normal, Foreground=color, TextAlignment=alignment, TextWrapping=TextWrapping.NoWrap };
                Canvas.SetLeft(footer,SlideLayout.TextX); Canvas.SetTop(footer,SlideLayout.FooterTop(slide)+i*SlideLayout.FooterLineSpacing(slide)); canvas.Children.Add(footer);
            }
            if(showTag && !string.IsNullOrEmpty(slide.Section)) {
                var tag=new Border { Name="SlideTag",Width=TagWidth(slide),Height=32,Background=Brush.Parse("#ECEEEC"),Padding=new Thickness(12,2),Child=new TextBlock { Text=TagLabel(slide),FontSize=20,FontFamily=FontCatalog.Get(slide.FontName).Family,Foreground=Brush.Parse("#38483E"),VerticalAlignment=Avalonia.Layout.VerticalAlignment.Center,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Center } };
                Canvas.SetLeft(tag,1280-32-tag.Width); Canvas.SetTop(tag,656); canvas.Children.Add(tag);
            }
            return canvas;
        }
        // Rendering and PPTX export use the same 720p background on Windows and macOS.
        public static byte[] BackgroundPng(string path,BackgroundTransform transform=null)
        {
            var bytes=BaseBackgroundPng(path);
            if(transform==null) return bytes;
            var value=transform.Copy(); value.Normalize();
            if(value.Zoom==1 && value.X==0 && value.Y==0 && value.Opacity==1 && !value.Flip) return bytes;
            using var bitmap=SKBitmap.Decode(bytes); using var image=SKImage.FromBitmap(bitmap);
            using var surface=SKSurface.Create(new SKImageInfo(1280,720)); var canvas=surface.Canvas; canvas.Clear(SKColors.Black);
            canvas.Translate((float)(640+value.X*12.8),(float)(360+value.Y*7.2));
            canvas.Scale((float)(value.Zoom*(value.Flip ? -1 : 1)),(float)value.Zoom);
            using var paint=new SKPaint { Color=SKColors.White.WithAlpha((byte)Math.Round(value.Opacity*255)) };
            canvas.DrawImage(image,new SKRect(-640,-360,640,360),new SKSamplingOptions(SKFilterMode.Linear),paint);
            using var snapshot=surface.Snapshot(); using var data=snapshot.Encode(SKEncodedImageFormat.Png,100); return data.ToArray();
        }
        private static byte[] BaseBackgroundPng(string path)
        {
            path=MediaLibrary.StillPath(path);
            using var surface=SKSurface.Create(new SKImageInfo(1280,720));
            var canvas=surface.Canvas;
            if(string.IsNullOrWhiteSpace(path))
            {
                using var gradient=SKShader.CreateLinearGradient(new SKPoint(0,0),new SKPoint(0,720),new[]{new SKColor(194,211,195),new SKColor(245,232,200)},null,SKShaderTileMode.Clamp);
                using var paint=new SKPaint { IsAntialias=true, Shader=gradient };
                canvas.DrawRect(0,0,1280,720,paint); paint.Shader=null; paint.Color=new SKColor(255,249,211,95); canvas.DrawCircle(900,235,105,paint);
                var shapes=new[] {
                    ("M0,490 L170,300 340,440 600,245 850,465 1050,350 1280,470 1280,720 0,720 Z",new SKColor(158,181,164)),
                    ("M0,555 L230,415 440,555 660,400 850,555 1120,425 1280,540 1280,720 0,720 Z",new SKColor(105,145,124)),
                    ("M0,650 C260,480 430,725 690,590 S1100,650 1280,555 L1280,720 0,720 Z",new SKColor(63,107,87))
                };
                foreach(var shape in shapes) { using var geometry=SKPath.ParseSvgPathData(shape.Item1); paint.Color=shape.Item2; canvas.DrawPath(geometry,paint); }
            }
            else
            {
                using var bitmap=SKBitmap.Decode(path) ?? throw new IOException(ChineseText.Ui("找不到或未能讀取背景圖片。"));
                using var image=SKImage.FromBitmap(bitmap);
                var scale=Math.Max(1280f/bitmap.Width,720f/bitmap.Height); var w=bitmap.Width*scale; var h=bitmap.Height*scale;
                canvas.DrawImage(image,new SKRect((1280-w)/2,(720-h)/2,(1280+w)/2,(720+h)/2),new SKSamplingOptions(SKFilterMode.Linear,SKMipmapMode.Linear));
            }
            using var snapshot=surface.Snapshot(); using var data=snapshot.Encode(SKEncodedImageFormat.Png,100); return data.ToArray();
        }
    }
}
