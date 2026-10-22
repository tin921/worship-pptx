using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace WorshipPptx;

// Pointer coordinates are taken in this 720p canvas, so dragging remains correct inside a Viewbox.
public sealed class CompositionPreview : Canvas
{
    private readonly bool editable;
    private readonly Border pip=new() { Name="PipFrame",BorderBrush=Brushes.White,CornerRadius=new CornerRadius(0),ClipToBounds=true,IsHitTestVisible=false };
    private readonly Border[] handles=new Border[4];
    private PreviewLayout layout;
    private SlideData renderedSlide;
    private string renderedSource;
    private Point start;
    private double startX,startY,startWidth,startHeight,screenScale=1;
    private string dragEdge;
    private IPointer dragPointer;
    public bool IsDragging => dragEdge!=null;
    public event Action LayoutChanged;
    public event Action DragCompleted;
    public CompositionPreview(SlideData slide,PreviewLayout layout,bool editable=true)
    {
        this.editable=editable; Width=1280; Height=720; Background=Brushes.Black; ClipToBounds=true; Focusable=editable;
        for(var i=0;i<handles.Length;i++) handles[i]=new Border { Name="PipHandle"+i,Background=Brushes.White,BorderBrush=Brushes.White,CornerRadius=new CornerRadius(0),IsHitTestVisible=false };
        SetSlide(slide,layout);
        LayoutUpdated+=(_,_)=> {
            var root=TopLevel.GetTopLevel(this); if(root==null) return;
            var a=this.TranslatePoint(default,root); var b=this.TranslatePoint(new Point(1,0),root);
            if(a is null || b is null) return;
            var scale=Math.Abs(b.Value.X-a.Value.X);
            if(scale>.01 && Math.Abs(scale-screenScale)>.001) { screenScale=scale; UpdateGeometry(); }
        };
    }
    private static Control Source(string source,SlideData slide) => source=="Camera" ? SingerGuide.Create() : SlideVisual.Create(slide);
    public void SetSlide(SlideData slide,PreviewLayout value)
    {
        layout=value; layout.Normalize();
        if(ReferenceEquals(renderedSlide,slide) && renderedSource==layout.BackgroundSource) { UpdateGeometry(); return; }
        renderedSlide=slide; renderedSource=layout.BackgroundSource;
        Children.Clear();
        var background=new Viewbox { Name="CompositionBackground",Tag=layout.BackgroundSource,Width=1280,Height=720,Stretch=Stretch.Fill,Child=Source(layout.BackgroundSource,slide),IsHitTestVisible=false };
        Children.Add(background);
        pip.Tag=layout.PipSource;
        pip.Child=new Viewbox { Stretch=Stretch.Fill,Child=Source(layout.PipSource,slide) };
        Children.Add(pip);
        if(editable) foreach(var handle in handles) Children.Add(handle);
        UpdateGeometry();
    }
    public void UpdateGeometry()
    {
        pip.IsVisible=layout.IsPip;
        pip.Width=layout.ScaleX*12.8; pip.Height=layout.ScaleY*7.2;
        pip.BorderThickness=new Thickness(2/screenScale);
        SetLeft(pip,layout.X*12.8); SetTop(pip,layout.Y*7.2);
        var offsets=new[]{(0d,0d),(1d,0d),(1d,1d),(0d,1d)};
        for(var i=0;i<handles.Length;i++) {
            var handle=handles[i]; var size=8/screenScale;
            handle.IsVisible=editable && layout.IsPip; handle.Width=handle.Height=size; handle.BorderThickness=new Thickness(1/screenScale);
            SetLeft(handle,Math.Clamp(layout.X*12.8+offsets[i].Item1*pip.Width-size/2,0,1280-size));
            SetTop(handle,Math.Clamp(layout.Y*7.2+offsets[i].Item2*pip.Height-size/2,0,720-size));
        }
    }
    private string EdgeAt(Point point)
    {
        var left=layout.X*12.8; var top=layout.Y*7.2; var right=left+layout.ScaleX*12.8; var bottom=top+layout.ScaleY*7.2;
        var reach=7/screenScale;
        if(point.X<left-reach || point.X>right+reach || point.Y<top-reach || point.Y>bottom+reach) return null;
        var edge="";
        if(Math.Abs(point.Y-top)<=reach) edge+="N"; else if(Math.Abs(point.Y-bottom)<=reach) edge+="S";
        if(Math.Abs(point.X-left)<=reach) edge+="W"; else if(Math.Abs(point.X-right)<=reach) edge+="E";
        return edge.Length==0 ? "Move" : edge;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if(!editable || !layout.IsPip || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var edge=EdgeAt(e.GetPosition(this)); if(edge==null) return;
        Focus(); dragEdge=edge; start=e.GetPosition(this); startX=layout.X; startY=layout.Y; startWidth=layout.ScaleX; startHeight=layout.ScaleY;
        dragPointer=e.Pointer; e.Pointer.Capture(this); e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if(!editable || !layout.IsPip) return;
        var point=e.GetPosition(this);
        var edge=dragEdge ?? EdgeAt(point);
        Cursor=new Cursor(edge switch { "Move"=>StandardCursorType.SizeAll,"N" or "S"=>StandardCursorType.SizeNorthSouth,"W" or "E"=>StandardCursorType.SizeWestEast,"NW" or "SE"=>StandardCursorType.TopLeftCorner,"NE" or "SW"=>StandardCursorType.TopRightCorner,_=>StandardCursorType.Arrow });
        if(dragEdge==null) return;
        var dx=(point.X-start.X)/12.8; var dy=(point.Y-start.Y)/7.2;
        if(dragEdge=="Move") { layout.X=Math.Clamp(startX+dx,0,100-startWidth); layout.Y=Math.Clamp(startY+dy,0,100-startHeight); }
        else {
            var left=startX; var top=startY; var right=startX+startWidth; var bottom=startY+startHeight;
            if(dragEdge.Contains('W')) left=Math.Clamp(startX+dx,0,right-PreviewLayout.MinimumScale);
            if(dragEdge.Contains('E')) right=Math.Clamp(right+dx,left+PreviewLayout.MinimumScale,100);
            if(dragEdge.Contains('N')) top=Math.Clamp(startY+dy,0,bottom-PreviewLayout.MinimumScale);
            if(dragEdge.Contains('S')) bottom=Math.Clamp(bottom+dy,top+PreviewLayout.MinimumScale,100);
            layout.X=left; layout.Y=top; layout.ScaleX=right-left; layout.ScaleY=bottom-top;
        }
        layout.X=Math.Round(layout.X,2); layout.Y=Math.Round(layout.Y,2); layout.ScaleX=Math.Round(layout.ScaleX,2); layout.ScaleY=Math.Round(layout.ScaleY,2); layout.Normalize();
        UpdateGeometry(); LayoutChanged?.Invoke(); e.Handled=true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if(dragEdge==null) return;
        dragEdge=null; dragPointer=null; e.Pointer.Capture(null); DragCompleted?.Invoke(); e.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e); if(dragEdge==null) return; dragEdge=null; dragPointer=null; DragCompleted?.Invoke();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if(e.Key!=Key.Escape || dragEdge==null) return;
        layout.X=startX; layout.Y=startY; layout.ScaleX=startWidth; layout.ScaleY=startHeight;
        dragEdge=null; dragPointer?.Capture(null); dragPointer=null; UpdateGeometry(); LayoutChanged?.Invoke(); DragCompleted?.Invoke(); e.Handled=true;
    }
}
