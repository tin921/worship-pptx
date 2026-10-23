using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using Avalonia.Controls;

namespace WorshipPptx;

// UI preferences are independent of worship programs and language/license settings.
public sealed class WindowLayouts
{
    [DataContract]
    public sealed class Layout
    {
        [DataMember] public double Width { get; set; }
        [DataMember] public double Height { get; set; }
        [DataMember] public bool Maximized { get; set; }
        [DataMember] public Dictionary<string,double> Columns { get; set; } = new();
    }

    private readonly string path;
    private readonly Dictionary<string,Layout> layouts;

    public WindowLayouts(string root)
    {
        path=Path.Combine(root,"window-layout.json");
        try { layouts=File.Exists(path) ? LocalStore.Read<Dictionary<string,Layout>>(path) : new(); }
        catch(Exception ex) { Trace.TraceWarning("Window layout could not be read: {0}",ex.Message); }
        layouts ??= new();
    }

    public T Remember<T>(T window,string key,Window owner=null,IReadOnlyDictionary<string,ColumnDefinition> columns=null) where T : Window
    {
        if(layouts.TryGetValue(key,out var saved) && saved!=null) {
            if(Valid(saved.Width)) window.Width=Math.Max(window.MinWidth,saved.Width);
            if(Valid(saved.Height)) window.Height=Math.Max(window.MinHeight,saved.Height);
            if(columns!=null && saved.Columns!=null)
                foreach(var (name,column) in columns)
                    if(saved.Columns.TryGetValue(name,out var width) && Valid(width))
                        column.Width=new GridLength(Math.Clamp(width,column.MinWidth,column.MaxWidth));
        }
        var screen=owner==null ? window.Screens.Primary : window.Screens.ScreenFromWindow(owner) ?? window.Screens.Primary;
        if(screen!=null) {
            var width=screen.WorkingArea.Width/screen.Scaling;
            var height=Math.Max(100,screen.WorkingArea.Height/screen.Scaling-40);
            window.MinWidth=Math.Min(window.MinWidth,width); window.MinHeight=Math.Min(window.MinHeight,height);
            window.Width=Math.Min(window.Width,width); window.Height=Math.Min(window.Height,height);
        }
        var normalWidth=window.Width; var normalHeight=window.Height;
        var maximized=saved?.Maximized==true;
        if(maximized) window.WindowState=WindowState.Maximized;
        window.SizeChanged+=(_,_)=> {
            if(window.WindowState!=WindowState.Normal) return;
            if(Valid(window.Width)) normalWidth=window.Width;
            if(Valid(window.Height)) normalHeight=window.Height;
        };
        window.PropertyChanged+=(_,e)=> {
            if(e.Property==Window.WindowStateProperty && window.WindowState!=WindowState.Minimized)
                maximized=window.WindowState==WindowState.Maximized;
        };
        window.Closed+=(_,_)=> {
            var layout=new Layout { Width=normalWidth,Height=normalHeight,Maximized=maximized };
            if(columns!=null)
                foreach(var (name,column) in columns) {
                    var width=column.ActualWidth>0 ? column.ActualWidth : column.Width.Value;
                    if(Valid(width)) layout.Columns[name]=width;
                }
            layouts[key]=layout;
            try { LocalStore.Write(path,layouts); }
            catch(Exception ex) { Trace.TraceWarning("Window layout could not be saved: {0}",ex.Message); }
        };
        return window;
    }

    private static bool Valid(double value) => double.IsFinite(value) && value>0;
}
