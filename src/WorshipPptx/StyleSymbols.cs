using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Shapes;

namespace WorshipPptx;

// Purpose-drawn symbols stay legible at toolbar size; backgrounds reserve most
// of their area for lyrics and are unsuitable as tiny style illustrations.
public static class StyleSymbols
{
    public static Control Create(string style)
    {
        var canvas=new Canvas { Width=48,Height=32 };
        void Shape(string path,string color) => canvas.Children.Add(new Path { Data=Geometry.Parse(path),Fill=Brush.Parse(color) });
        switch(style) {
            case "photo":
                Shape("M6,8 H14 L18,3 H30 L34,8 H42 V29 H6 Z","#28543E");
                Shape("M32,18 A8,8 0 1 0 16,18 A8,8 0 1 0 32,18 Z","#FFFFFF");
                Shape("M29,18 A5,5 0 1 0 19,18 A5,5 0 1 0 29,18 Z M36,11 H40 V14 H36 Z","#D49C57"); break;
            case "illustration":
                Shape("M17,18 L35,1 Q40,0 39,5 L23,24 Z","#28543E");
                Shape("M14,19 L24,25 Q19,33 7,29 Q12,26 11,23 Z","#D49C57");
                Shape("M17,18 L22,14 L28,20 L23,25 Z","#8297A3"); break;
            case "abstract":
                Shape("M2,22 C13,-3 29,32 46,5 L46,16 C30,39 14,6 2,30 Z","#28543E");
                Shape("M2,10 C17,29 30,-8 46,4 L46,10 C29,-1 16,37 2,18 Z","#D49C57"); break;
            case "sculpted":
                Shape("M24,1 L43,10 L24,20 L5,10 Z","#B7D0BE");
                Shape("M5,10 L24,20 V32 L5,22 Z","#426B54");
                Shape("M24,20 L43,10 V22 L24,32 Z","#203C30"); break;
            default:
                Shape("M21,10 A9,9 0 1 0 3,10 A9,9 0 1 0 21,10 Z","#D49C57");
                Shape("M22,31 L34,8 L47,31 Z","#28543E");
                Shape("M6,23 H19 V31 H6 Z","#8297A3"); break;
        }
        return new Viewbox { Child=canvas,Stretch=Stretch.Uniform };
    }
}
