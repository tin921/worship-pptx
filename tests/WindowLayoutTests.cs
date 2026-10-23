using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Threading;

namespace WorshipPptx.Tests;

static class WindowLayoutTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Window sizes and both editor dividers survive a restart",()=> {
            var store=new LocalStore(Path.Combine(root,"layout-"+Guid.NewGuid().ToString("N")));
            var first=new MainWindow(store); first.Show(); Dispatcher.UIThread.RunJobs();
            first.Width=1500; first.Height=860;
            var columns=first.FindControl<Grid>("WorkArea").ColumnDefinitions;
            columns[0].Width=new GridLength(320); columns[4].Width=new GridLength(150);
            Dispatcher.UIThread.RunJobs(); first.Close();
            var next=new MainWindow(new LocalStore(store.Root)); next.Show(); Dispatcher.UIThread.RunJobs();
            try {
                var restored=next.FindControl<Grid>("WorkArea").ColumnDefinitions;
                check(next.Width==1500 && next.Height==860,"Main window size was not restored");
                check(Math.Abs(restored[0].ActualWidth-320)<1 && Math.Abs(restored[4].ActualWidth-150)<1,"A divider position was not restored");
            } finally { next.Close(); }
        });
        test("Dialog sizes stay separate and maximization preserves the normal size",()=> {
            var store=new LocalStore(Path.Combine(root,"dialogs-"+Guid.NewGuid().ToString("N")));
            var layouts=new WindowLayouts(store.Root);
            var preview=layouts.Remember(new Window { Width=800,Height=600 },"presentation");
            preview.Show(); preview.Width=900; preview.Height=650; Dispatcher.UIThread.RunJobs();
            preview.WindowState=WindowState.Maximized; Dispatcher.UIThread.RunJobs(); preview.Close();
            var search=layouts.Remember(new Window { Width=700,Height=500 },"background"); search.Show(); search.Close();
            layouts=new WindowLayouts(store.Root);
            var restored=layouts.Remember(new Window { Width=400,Height=300 },"presentation");
            check(restored.Width==900 && restored.Height==650 && restored.WindowState==WindowState.Maximized,"Maximized window lost its normal bounds/state");
            var other=layouts.Remember(new Window { Width=400,Height=300 },"background");
            check(other.Width==700 && other.Height==500 && other.WindowState==WindowState.Normal,"Dialog layouts overwrote one another");
            restored.Close(); other.Close();
        });
        test("Damaged or invalid saved window layouts fall back to usable defaults",()=> {
            var store=new LocalStore(Path.Combine(root,"invalid-layout-"+Guid.NewGuid().ToString("N")));
            var path=Path.Combine(store.Root,"window-layout.json"); File.WriteAllText(path,"broken json");
            var window=new WindowLayouts(store.Root).Remember(new Window { Width=800,Height=600 },"main");
            check(window.Width==800 && window.Height==600,"Damaged preferences changed the defaults"); window.Close();
            LocalStore.Write(path,new Dictionary<string,WindowLayouts.Layout> { ["main"]=new() { Width=-50,Height=0 } });
            window=new WindowLayouts(store.Root).Remember(new Window { Width=800,Height=600 },"main");
            check(window.Width==800 && window.Height==600,"Invalid dimensions were restored"); window.Close();
        });
    }
}
