using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WorshipPptx.Tests;

static class ActionButtonTests
{
    private static void Pump(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static Button Button(MainWindow window,string name) => window.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)==name);
    private static void Click(Window window,Control control,double x=.5,double y=.5)
    {
        var point=control.TranslatePoint(new Point(control.Bounds.Width*x,control.Bounds.Height*y),window).Value;
        window.MouseMove(point); window.MouseDown(point,MouseButton.Left); window.MouseUp(point,MouseButton.Left); Pump(window);
    }
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Footer buttons receive clicks across their faces after font changes",()=> {
            var store=new LocalStore(Path.Combine(root,"action-hit-test")); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump(window);
            try {
                var buttons=new[]{"預覽整個崇拜","製作 PowerPoint","上傳 Google Drive"}.Select(n=>Button(window,n)).ToArray();
                var clicks=0;
                foreach(var button in buttons) button.AddHandler(Avalonia.Controls.Button.ClickEvent,(_,e)=>clicks++,RoutingStrategies.Bubble,true);
                foreach(var font in FontCatalog.Bundled) {
                    window.FindControl<ComboBox>("FontPicker").SelectedItem=font; Pump(window);
                    foreach(var button in buttons) foreach(var position in new[]{(.1,.1),(.5,.5),(.9,.9)}) {
                        var before=clicks; Click(window,button,position.Item1,position.Item2);
                        check(clicks==before+1,$"Lost click on {button.Content} with {font.Name} at {position}");
                        foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window);
                    }
                }
            } finally {window.Close();}
        });
        test("Each footer action receives the click that dismisses a font, size or color popup",()=> {
            var store=new LocalStore(Path.Combine(root,"action-font-dropdown")); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump(window);
            try {
                foreach(var popupName in new[]{"FontPicker","FontSizePicker","TextColorPicker"})
                    foreach(var name in new[]{"預覽整個崇拜","製作 PowerPoint","上傳 Google Drive"}) {
                        var control=window.FindControl<Control>(popupName);
                        Click(window,control);
                        check(control is ComboBox combo ? combo.IsDropDownOpen : ((Button)control).Flyout.IsOpen,"Popup did not open: "+popupName);
                        var button=Button(window,name); var clicks=0;
                        EventHandler<RoutedEventArgs> count=(_,_)=>clicks++;
                        button.Click+=count;
                        Click(window,button); button.Click-=count;
                        check(clicks==1,$"{popupName} swallowed the {name} click");
                        check(control is ComboBox picker ? !picker.IsDropDownOpen : !((Button)control).Flyout.IsOpen,"Popup did not dismiss: "+popupName);
                        // Headless storage returns a cancelled save picker without a native window.
                        if(name!="製作 PowerPoint") check(window.OwnedWindows.Any(),"The requested action did not open a window: "+name);
                        foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window);
                    }
            } finally {foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window); window.Close();}
        });
        test("Returning from the background picker and dragging the preview leave footer clicks usable",()=> {
            var store=new LocalStore(Path.Combine(root,"action-background")); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump(window);
            try {
                Click(window,window.FindControl<Button>("EditBackground"));
                var picker=window.OwnedWindows.OfType<BackgroundDialog>().Single(); Pump(picker);
                Click(picker,picker.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string=="取消" && b.IsEffectivelyVisible && b.IsEffectivelyEnabled)); Pump(window);
                check(!window.OwnedWindows.Any(),"Background picker stayed open");
                var preview=(CompositionPreview)window.FindControl<Viewbox>("SlideHost").Child;
                preview.ZoomBy(1.4); Pump(window);
                var from=preview.TranslatePoint(new Point(300,180),window).Value;
                var to=preview.TranslatePoint(new Point(360,200),window).Value;
                window.MouseDown(from,MouseButton.Left); window.MouseMove(to,RawInputModifiers.LeftMouseButton); window.MouseUp(to,MouseButton.Left); Pump(window);
                check(!preview.IsDragging,"Preview retained pointer capture");
                foreach(var name in new[]{"預覽整個崇拜","製作 PowerPoint","上傳 Google Drive"}) {
                    var button=Button(window,name); var clicks=0; EventHandler<RoutedEventArgs> count=(_,_)=>clicks++;
                    button.Click+=count; Click(window,button); button.Click-=count;
                    check(clicks==1,"Lost click after background editing: "+name);
                    if(name!="製作 PowerPoint") check(window.OwnedWindows.Any(),"Action did not open after background editing: "+name);
                    foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window);
                }
            } finally {foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window); window.Close();}
        });
        test("Actions acknowledge the first click, reject duplicate requests and recover after validation errors",()=> {
            var store=new LocalStore(Path.Combine(root,"action-busy")); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump(window);
            try {
                var actions=window.FindControl<StackPanel>("ActionButtons");
                var button=Button(window,"預覽整個崇拜");
                button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                check(!actions.IsEnabled && window.FindControl<TextBlock>("StatusText").Text=="正在準備預覽…","No immediate acknowledgement before slide preparation");
                window.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=Key.F5}); Pump(window);
                check(window.OwnedWindows.Count()==1,"Repeated request opened duplicate windows");
                window.OwnedWindows.Single().Close(); Pump(window);
                check(actions.IsEnabled && !window.FindControl<TextBlock>("StatusText").Text.Contains("準備"),"Buttons or status were left busy");
                var title=window.FindControl<TextBox>("SongTitle"); var original=title.Text; title.Text=""; Pump(window);
                Click(window,button);
                check(window.OwnedWindows.Single() is not PreviewWindow,"Invalid content did not show its error");
                window.OwnedWindows.Single().Close(); Pump(window); check(actions.IsEnabled,"An error left the buttons disabled");
                title.Text=original; Pump(window); Click(window,button);
                check(window.OwnedWindows.Single() is PreviewWindow,"Corrected content could not be previewed with one click");
            } finally {foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window); window.Close();}
        });
        test("Preview and upload open with one click after returning from their dialogs",()=> {
            var store=new LocalStore(Path.Combine(root,"action-dialogs")); store.Save(Demo.Create());
            var window=new MainWindow(store); window.Show(); Pump(window);
            try {
                foreach(var name in new[]{"預覽整個崇拜","上傳 Google Drive"}) for(var i=0;i<4;i++) {
                    var timer=Stopwatch.StartNew(); Click(window,Button(window,name));
                    var dialog=window.OwnedWindows.SingleOrDefault();
                    check(dialog!=null,$"Click {i+1} did not open {name}");
                    Console.WriteLine($"  {name} click {i+1}: {timer.ElapsedMilliseconds} ms, {dialog.GetType().Name}");
                    dialog.Close(); Pump(window);
                }
            } finally {foreach(var dialog in window.OwnedWindows.ToArray()) dialog.Close(); Pump(window); window.Close();}
        });
    }
}
