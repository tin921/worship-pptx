using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace WorshipPptx;

// The camera stays fixed. Only the slide background responds to pointer gestures.
public sealed class CompositionPreview : Canvas
{
    private readonly bool editable;
    private readonly Border pip=new() { Name="PipFrame",BorderBrush=Brushes.White,CornerRadius=new CornerRadius(0),ClipToBounds=true,IsHitTestVisible=false };
    private PreviewLayout layout;
    private BackgroundTransform adjustment;
    private Image backgroundImage;
    private Point dragStart;
    private double startX,startY,screenScale=1;
    private IPointer dragPointer;
    public bool IsDragging => dragPointer!=null;
    public event Action BackgroundChanged;
    public event Action InteractionCompleted;
    public CompositionPreview(SlideData slide,PreviewLayout layout,bool editable=false,BackgroundTransform adjustment=null)
    {
        this.editable=editable; Width=1280; Height=720; Background=Brushes.White; ClipToBounds=true; Focusable=editable;
        SetSlide(slide,layout,adjustment);
        LayoutUpdated+=(_,_)=> {
            var root=TopLevel.GetTopLevel(this); if(root==null) return;
            var a=this.TranslatePoint(default,root); var b=this.TranslatePoint(new Point(1,0),root);
            if(a is null || b is null) return;
            var scale=Math.Abs(b.Value.X-a.Value.X);
            if(scale>.01 && Math.Abs(scale-screenScale)>.001) { screenScale=scale; UpdateGeometry(); }
        };
    }
    public void SetSlide(SlideData slide,PreviewLayout value,BackgroundTransform transform=null)
    {
        layout=value; layout.Normalize(); adjustment=transform ?? slide.BackgroundTransform ?? new();
        slide.BackgroundTransform=adjustment;
        Children.Clear(); var raw=SlideVisual.Create(slide); backgroundImage=(Image)raw.Children[0];
        Children.Add(new Viewbox { Name="CompositionBackground",Tag=layout.BackgroundSource,Width=1280,Height=720,Stretch=Stretch.Fill,Child=layout.BackgroundSource=="Camera" ? PresenterGuide.Create(slide.IsSong) : raw,IsHitTestVisible=false });
        pip.Tag=layout.PipSource;
        pip.Child=new Viewbox { Stretch=Stretch.Fill,Child=layout.PipSource=="Camera" ? PresenterGuide.Create(slide.IsSong) : SlideVisual.Create(slide) };
        Children.Add(pip); UpdateGeometry();
    }
    private void UpdateGeometry()
    {
        pip.IsVisible=layout.IsPip; pip.Width=layout.ScaleX*12.8; pip.Height=layout.ScaleY*7.2;
        pip.BorderThickness=new Thickness(2/screenScale);
        SetLeft(pip,layout.X*12.8); SetTop(pip,layout.Y*7.2);
    }
    private bool OverCamera(Point point) => pip.IsVisible && new Rect(GetLeft(pip),GetTop(pip),pip.Width,pip.Height).Contains(point);
    private void UpdateBackground()
    {
        adjustment.Normalize(); SlideVisual.UpdateBackground(backgroundImage,adjustment); BackgroundChanged?.Invoke();
    }
    public void ZoomBy(double factor,Point? anchor=null)
    {
        if(!editable) return;
        var point=anchor ?? new Point(640,360); var old=adjustment.Zoom;
        adjustment.Zoom=Math.Clamp(old*factor,1,4); var ratio=adjustment.Zoom/old;
        adjustment.X=(point.X-640-(point.X-640-adjustment.X*12.8)*ratio)/12.8;
        adjustment.Y=(point.Y-360-(point.Y-360-adjustment.Y*7.2)*ratio)/7.2;
        UpdateBackground(); InteractionCompleted?.Invoke();
    }
    public void ResetBackground()
    {
        adjustment.Zoom=1; adjustment.X=adjustment.Y=0;
        UpdateBackground(); InteractionCompleted?.Invoke();
    }
    public void PanBy(double x,double y)
    {
        if(!editable) return;
        adjustment.X+=x; adjustment.Y+=y;
        UpdateBackground(); InteractionCompleted?.Invoke();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); var point=e.GetPosition(this);
        if(!editable || OverCamera(point) || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        Focus(); dragStart=point; startX=adjustment.X; startY=adjustment.Y;
        dragPointer=e.Pointer; e.Pointer.Capture(this); e.Handled=true;
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if(!editable) return;
        var point=e.GetPosition(this); Cursor=new Cursor(OverCamera(point) ? StandardCursorType.Arrow : StandardCursorType.SizeAll);
        if(dragPointer==null) return;
        adjustment.X=startX+(point.X-dragStart.X)/12.8; adjustment.Y=startY+(point.Y-dragStart.Y)/7.2;
        UpdateBackground(); e.Handled=true;
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if(!editable || OverCamera(e.GetPosition(this))) return;
        ZoomBy(Math.Pow(1.12,e.Delta.Y),e.GetPosition(this)); e.Handled=true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if(dragPointer==null) return;
        dragPointer=null; e.Pointer.Capture(null); InteractionCompleted?.Invoke(); e.Handled=true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e); if(dragPointer==null) return;
        dragPointer=null; InteractionCompleted?.Invoke();
    }
    public void CancelPan()
    {
        if(dragPointer==null) return;
        adjustment.X=startX; adjustment.Y=startY;
        var pointer=dragPointer; dragPointer=null; pointer.Capture(null); UpdateBackground(); InteractionCompleted?.Invoke();
    }
}
