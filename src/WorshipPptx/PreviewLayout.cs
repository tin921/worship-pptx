using System;
using System.Runtime.Serialization;

namespace WorshipPptx;

// Percentages are relative to the complete 1280 x 720 composition, not the PiP image.
[DataContract]
public sealed class PreviewLayout
{
    public const double CornerMargin=2, MinimumScale=10;
    [DataMember] public bool IsPip { get; set; }=true;
    [DataMember] public string BackgroundSource { get; set; }="Slide";
    [DataMember] public double X { get; set; }=58;
    [DataMember] public double Y { get; set; }=58;
    [DataMember] public double ScaleX { get; set; }=40;
    [DataMember] public double ScaleY { get; set; }=40;
    public string PipSource => BackgroundSource=="Slide" ? "Camera" : "Slide";
    public string TextArea => !IsPip || BackgroundSource=="Camera" ? "Full" : Y+ScaleY/2<50 ? "Bottom" : "Top";
    public static PreviewLayout FromLegacy(bool fullSlide) => new() { IsPip=!fullSlide };
    [OnDeserializing] private void Defaults(StreamingContext _) { IsPip=true; BackgroundSource="Slide"; X=58; Y=58; ScaleX=40; ScaleY=40; }
    [OnDeserialized] private void Loaded(StreamingContext _) => Normalize();
    private static double Finite(double value,double fallback) => double.IsFinite(value) ? value : fallback;
    public void Normalize()
    {
        if(BackgroundSource!="Camera") BackgroundSource="Slide";
        ScaleX=Math.Clamp(Finite(ScaleX,40),MinimumScale,100);
        ScaleY=Math.Clamp(Finite(ScaleY,40),MinimumScale,100);
        X=Math.Clamp(Finite(X,58),0,100-ScaleX);
        Y=Math.Clamp(Finite(Y,58),0,100-ScaleY);
    }
    public void ApplyPreset(string preset)
    {
        if(preset=="FullSlide" || preset=="FullCamera") {
            IsPip=false; BackgroundSource=preset=="FullCamera" ? "Camera" : "Slide"; return;
        }
        IsPip=true; ScaleX=ScaleY=40;
        X=preset.EndsWith("Left",StringComparison.Ordinal) ? CornerMargin : 100-40-CornerMargin;
        Y=preset.StartsWith("Top",StringComparison.Ordinal) ? CornerMargin : 100-40-CornerMargin;
    }
    public bool Matches(string preset)
    {
        if(preset=="FullSlide" || preset=="FullCamera") return !IsPip && BackgroundSource==(preset=="FullCamera" ? "Camera" : "Slide");
        var target=new PreviewLayout(); target.ApplyPreset(preset);
        return IsPip && Math.Abs(X-target.X)<.05 && Math.Abs(Y-target.Y)<.05 && Math.Abs(ScaleX-40)<.05 && Math.Abs(ScaleY-40)<.05;
    }
}
