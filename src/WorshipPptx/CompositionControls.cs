using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace WorshipPptx;

// Wheel changes are deliberately limited to the number field holding keyboard focus.
public sealed class LayoutNumber : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);
    public LayoutNumber()
    {
        Minimum=0; Maximum=100; Increment=1; FormatString="0.##"; ShowButtonSpinner=false;
        FontSize=13; MinWidth=0; MinHeight=32; Height=32; Padding=new Thickness(5,3); HorizontalAlignment=HorizontalAlignment.Stretch;
        AddHandler(PointerWheelChangedEvent,Wheel,RoutingStrategies.Tunnel,true);
    }
    private void Wheel(object sender,PointerWheelEventArgs e)
    {
        // Block the built-in spinner from changing an unfocused field on hover.
        e.Handled=true;
        if(!IsKeyboardFocusWithin || !IsEnabled || e.Delta.Y==0) return;
        Value=Math.Clamp((Value ?? 0)+(e.Delta.Y>0 ? Increment : -Increment),Minimum,Maximum);
    }
}

public sealed class CompositionControls : Grid
{
    public event Action LayoutChanged;
    private PreviewLayout layout;
    private bool updating;
    private readonly Dictionary<string,ToggleButton> presets=new();
    public LayoutNumber PositionX { get; }=new() { Name="PipPositionX" };
    public LayoutNumber PositionY { get; }=new() { Name="PipPositionY" };
    public LayoutNumber ScaleX { get; }=new() { Name="PipScaleX",Minimum=10 };
    public LayoutNumber ScaleY { get; }=new() { Name="PipScaleY",Minimum=10 };
    public ComboBox BackgroundSource { get; }=new() { Name="BackgroundSource",ItemsSource=new[]{"投影片","攝影機"},MinWidth=0,Height=32,FontSize=13,Padding=new Thickness(5,3) };
    public ComboBox PipSource { get; }=new() { Name="PipSource",ItemsSource=new[]{"投影片","攝影機"},MinWidth=0,Height=32,FontSize=13,Padding=new Thickness(5,3) };
    public CompositionControls()
    {
        ColumnDefinitions=new ColumnDefinitions("*,12,*"); RowDefinitions=new RowDefinitions("Auto,Auto,Auto");
        var views=new Grid { Name="QuickViews",ColumnDefinitions=new ColumnDefinitions("*,*,*"),RowDefinitions=new RowDefinitions("Auto,Auto,Auto") };
        var heading=new TrText("快速畫面") {FontSize=13,Margin=new Thickness(0,0,0,5) }; Grid.SetColumnSpan(heading,3); views.Children.Add(heading);
        var keys=new[]{"TopLeft","TopRight","FullSlide","BottomLeft","BottomRight","FullCamera"};
        var names=new[]{"左上","右上","投影片","左下","右下","攝影機"};
        for(var i=0;i<keys.Length;i++) {
            var key=keys[i];
            var stack=new StackPanel { Spacing=1,HorizontalAlignment=HorizontalAlignment.Center };
            stack.Children.Add(PresetIcon(key)); stack.Children.Add(new TrText(names[i]) {FontSize=11,HorizontalAlignment=HorizontalAlignment.Center });
            var button=new ToggleButton { Name="View"+key,Tag=key,Content=stack,Padding=new Thickness(2,1),Margin=new Thickness(0,0,3,3),MinWidth=0,HorizontalAlignment=HorizontalAlignment.Stretch,CornerRadius=new CornerRadius(4) };
            button.Bind(AutomationProperties.NameProperty,TrExtension.For("快速畫面："+names[i])); button.Bind(ToolTip.TipProperty,TrExtension.For(i==2 || i==5 ? "全畫面："+names[i] : "畫中畫："+names[i]+"，邊距 2%"));
            Grid.SetColumn(button,i%3); Grid.SetRow(button,1+i/3); views.Children.Add(button); presets.Add(key,button);
            button.Click+=(_,_)=> { if(updating || layout==null) return; layout.ApplyPreset(key); Sync(layout); LayoutChanged?.Invoke(); };
        }
        Children.Add(views);
        var fields=new Grid { Name="PipFields",ColumnDefinitions=new ColumnDefinitions("Auto,*,*"),RowDefinitions=new RowDefinitions("Auto,Auto,Auto") };
        Add(fields,new TrText("位置 / 大小（%）") {FontSize=13,Margin=new Thickness(0,0,0,5) },0,0,3);
        Add(fields,new TrText("位置") {FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,4,0) },1,0);
        Add(fields,new TrText("大小") {FontSize=12,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,4,0) },2,0);
        Add(fields,Field("X",PositionX),1,1); Add(fields,Field("Y",PositionY),1,2);
        Add(fields,Field("X",ScaleX),2,1); Add(fields,Field("Y",ScaleY),2,2);
        Grid.SetColumn(fields,2); Children.Add(fields);
        var sources=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*,12,Auto,*"),Margin=new Thickness(0,6,0,0) };
        Add(sources,new TrText("背景") {FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0) },0,0);
        Add(sources,BackgroundSource,0,1);
        Add(sources,new TrText("畫中畫") {FontSize=13,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0) },0,3);
        Add(sources,PipSource,0,4); Grid.SetRow(sources,1); Grid.SetColumnSpan(sources,3); Children.Add(sources);
        var note=new TrText("拖動移位 · 拉邊縮放 · 聚焦後滾輪調數值") {FontSize=11,Foreground=Brush.Parse("#738077"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0) };
        Grid.SetRow(note,2); Grid.SetColumnSpan(note,3); Children.Add(note);
        foreach(var field in new[]{PositionX,PositionY,ScaleX,ScaleY}) field.ValueChanged+=(_,_)=>NumberChanged(field);
        BackgroundSource.SelectionChanged+=(_,_)=>SourceChanged(false);
        PipSource.SelectionChanged+=(_,_)=>SourceChanged(true);
        BackgroundSource.Bind(AutomationProperties.NameProperty,TrExtension.For("背景來源")); PipSource.Bind(AutomationProperties.NameProperty,TrExtension.For("畫中畫來源")); RefreshLanguage();
    }
    public void RefreshLanguage()
    {
        updating=true;
        try {
            var background=BackgroundSource.SelectedIndex; var pip=PipSource.SelectedIndex;
            BackgroundSource.ItemsSource=new[]{ChineseText.Ui("投影片"),ChineseText.Ui("攝影機")};
            PipSource.ItemsSource=new[]{ChineseText.Ui("投影片"),ChineseText.Ui("攝影機")};
            BackgroundSource.SelectedIndex=background; PipSource.SelectedIndex=pip;
        } finally { updating=false; }
    }
    private static Control Field(string axis,LayoutNumber number)
    {
        var field=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),Margin=new Thickness(0,0,0,5) };
        field.Children.Add(new TextBlock { Text=axis,FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,3,0) });
        Grid.SetColumn(number,1); field.Children.Add(number);
        number.Bind(AutomationProperties.NameProperty,TrExtension.For((number.Name.Contains("Position") ? "位置 " : "大小 ")+axis+"（百分比）"));
        number.Bind(ToolTip.TipProperty,TrExtension.For("佔完整畫面的百分比；聚焦後滑鼠滾輪每格調整 1%")); return field;
    }
    private static void Add(Grid grid,Control control,int row,int column,int span=1) { Grid.SetRow(control,row); Grid.SetColumn(control,column); Grid.SetColumnSpan(control,span); grid.Children.Add(control); }
    private static Control PresetIcon(string key)
    {
        var canvas=new Canvas { Width=28,Height=16,Background=Brush.Parse("#DCE5DB") };
        if(key.StartsWith("Full")) canvas.Children.Add(new TextBlock { Text=key=="FullSlide" ? "▤" : "●",FontSize=13,Width=28,Height=16,TextAlignment=TextAlignment.Center,Foreground=Brush.Parse("#426B54") });
        else {
            var small=new Border { Width=11,Height=6,Background=Brush.Parse("#426B54"),BorderBrush=Brushes.White,BorderThickness=new Thickness(1) };
            Canvas.SetLeft(small,key.EndsWith("Left") ? 2 : 15); Canvas.SetTop(small,key.StartsWith("Top") ? 2 : 8); canvas.Children.Add(small);
        }
        return canvas;
    }
    public void Sync(PreviewLayout value)
    {
        updating=true;
        try {
            layout=value; layout.Normalize();
            PositionX.Maximum=(decimal)(100-layout.ScaleX); PositionY.Maximum=(decimal)(100-layout.ScaleY);
            PositionX.Value=(decimal)layout.X; PositionY.Value=(decimal)layout.Y; ScaleX.Value=(decimal)layout.ScaleX; ScaleY.Value=(decimal)layout.ScaleY;
            foreach(var field in new[]{PositionX,PositionY,ScaleX,ScaleY}) field.IsEnabled=layout.IsPip;
            foreach(var pair in presets) pair.Value.IsChecked=layout.Matches(pair.Key);
            BackgroundSource.SelectedIndex=layout.BackgroundSource=="Camera" ? 1 : 0;
            PipSource.SelectedIndex=layout.PipSource=="Camera" ? 1 : 0; PipSource.IsEnabled=layout.IsPip;
        } finally { updating=false; }
    }
    private void NumberChanged(LayoutNumber field)
    {
        if(updating || layout==null || field.Value==null) return;
        var value=(double)field.Value.Value;
        if(field==PositionX) layout.X=value; else if(field==PositionY) layout.Y=value; else if(field==ScaleX) layout.ScaleX=value; else layout.ScaleY=value;
        layout.Normalize(); Sync(layout); LayoutChanged?.Invoke();
    }
    private void SourceChanged(bool pip)
    {
        if(updating || layout==null) return;
        var index=(pip ? PipSource : BackgroundSource).SelectedIndex; if(index<0) return;
        layout.BackgroundSource=(pip ? index==0 : index==1) ? "Camera" : "Slide";
        Sync(layout); LayoutChanged?.Invoke();
    }
}
