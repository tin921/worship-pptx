using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
namespace WorshipPptx
{
    public static class SlideVisual
    {
        private static BitmapSource defaultImage;
        public static BitmapSource DefaultImage
        {
            get
            {
                if (defaultImage != null) return defaultImage;
                var visual = new DrawingVisual();
                using (var d = visual.RenderOpen())
                {
                    d.DrawRectangle(new LinearGradientBrush(Color.FromRgb(194,211,195), Color.FromRgb(245,232,200), 90), null, new Rect(0,0,1280,720));
                    d.DrawEllipse(new SolidColorBrush(Color.FromArgb(95,255,249,211)), null, new Point(900,235), 105,105);
                    d.DrawGeometry(new SolidColorBrush(Color.FromRgb(158,181,164)), null, Geometry.Parse("M0,490 L170,300 340,440 600,245 850,465 1050,350 1280,470 1280,720 0,720 Z"));
                    d.DrawGeometry(new SolidColorBrush(Color.FromRgb(105,145,124)), null, Geometry.Parse("M0,555 L230,415 440,555 660,400 850,555 1120,425 1280,540 1280,720 0,720 Z"));
                    d.DrawGeometry(new SolidColorBrush(Color.FromRgb(63,107,87)), null, Geometry.Parse("M0,650 C260,480 430,725 690,590 S1100,650 1280,555 L1280,720 0,720 Z"));
                }
                var bmp = new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32); bmp.Render(visual); bmp.Freeze(); defaultImage = bmp; return bmp;
            }
        }
        public static BitmapSource Background(string path) => string.IsNullOrWhiteSpace(path) ? DefaultImage : ImageStore.Load(path);
        public static Canvas Create(SlideData slide)
        {
            var canvas = new Canvas { Width=1280, Height=720, ClipToBounds=true, Background=Brushes.Black };
            var image = new Image { Source=Background(slide.Background), Width=1280, Height=720, Stretch=Stretch.UniformToFill }; canvas.Children.Add(image);
            canvas.Children.Add(new Rectangle { Width=1280, Height=360, Fill=new SolidColorBrush(Color.FromArgb(125,0,0,0)) });
            for (var i=0; i<slide.Lines.Count; i++)
            {
                var text = new TextBlock { Text=slide.Lines[i], Width=SlideLayout.TextWidth, Height=SlideLayout.LineHeight, FontFamily=FontCatalog.Get(slide.FontName).Family, FontSize=SlideLayout.FontSize, Foreground=Brushes.White, TextAlignment=TextAlignment.Center, TextWrapping=TextWrapping.NoWrap };
                Canvas.SetLeft(text,SlideLayout.TextX); Canvas.SetTop(text,SlideLayout.TextY + i*SlideLayout.LineHeight); canvas.Children.Add(text);
            }
            var footer = new TextBlock { Text=slide.Footer, Width=SlideLayout.TextWidth, FontFamily=FontCatalog.Get(slide.FontName).Family, FontSize=SlideLayout.FooterSize(slide.Footer, slide.FontName), Foreground=new SolidColorBrush(Color.FromRgb(228,234,224)), TextAlignment=TextAlignment.Center, TextWrapping=TextWrapping.NoWrap };
            Canvas.SetLeft(footer,SlideLayout.TextX); Canvas.SetTop(footer,330); canvas.Children.Add(footer); return canvas;
        }
        public static byte[] BackgroundPng(string path)
        {
            var visual = new DrawingVisual();
            var bitmap = Background(path);
            var scale = Math.Max(1280.0/bitmap.PixelWidth,720.0/bitmap.PixelHeight);
            var w=bitmap.PixelWidth*scale; var h=bitmap.PixelHeight*scale;
            using (var d = visual.RenderOpen()) { d.PushClip(new RectangleGeometry(new Rect(0,0,1280,720))); d.DrawImage(bitmap,new Rect((1280-w)/2,(720-h)/2,w,h)); }
            var render = new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32); render.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(render));
            using (var stream = new MemoryStream()) { encoder.Save(stream); return stream.ToArray(); }
        }
    }
}
