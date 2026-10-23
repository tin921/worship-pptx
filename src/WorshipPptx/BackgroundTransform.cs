using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace WorshipPptx;

[DataContract]
public sealed class BackgroundTransform
{
    [DataMember] public double Zoom { get; set; }=1;
    [DataMember] public double X { get; set; }
    [DataMember] public double Y { get; set; }
    [DataMember] public double Opacity { get; set; }=1;
    [DataMember] public bool Flip { get; set; }
    [OnDeserializing] private void Defaults(StreamingContext _) { Zoom=Opacity=1; }
    [OnDeserialized] private void Loaded(StreamingContext _) => Normalize();
    public void Normalize()
    {
        Zoom=Math.Clamp(double.IsFinite(Zoom) ? Zoom : 1,1,4);
        Opacity=Math.Clamp(double.IsFinite(Opacity) ? Opacity : 1,0,1);
        var limit=(Zoom-1)*50;
        X=Math.Clamp(double.IsFinite(X) ? X : 0,-limit,limit);
        Y=Math.Clamp(double.IsFinite(Y) ? Y : 0,-limit,limit);
    }
    public BackgroundTransform Copy() => new() { Zoom=Zoom,X=X,Y=Y,Opacity=Opacity,Flip=Flip };
    public string Key => FormattableString.Invariant($"{Zoom:R}|{X:R}|{Y:R}|{Opacity:R}|{Flip}");
}
