using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace WorshipPptx.Tests;

static class ServiceHistoryTests
{
    private static void Pump() => Dispatcher.UIThread.RunJobs();
    private static Button Remove(MainWindow window) => window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="RemoveServiceButton" && b.IsEffectivelyVisible);
    private static Window Ask(MainWindow window)
    {
        Remove(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        return window.OwnedWindows.Single();
    }
    private static void Reply(Window dialog,string label)
    {
        dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
    }
    private static ServicePlan Add(LocalStore store,string title,int days)
    {
        var plan=Demo.Create(); plan.Title=title; plan.Date=DateTime.Today.AddDays(days); store.Save(plan); return plan;
    }
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Removing a program requires confirmation and only the selected row has its control",()=> {
            var store=new LocalStore(Path.Combine(root,"remove-cancel"));
            var earlier=Add(store,"上週崇拜",-7); var current=Add(store,"本週崇拜",1);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var history=window.FindControl<ListBox>("HistoryList");
                check(Remove(window).DataContext==history.SelectedItem,"Remove control is on an unselected program");
                var dialog=Ask(window);
                check(dialog.Title=="移除崇拜" && dialog.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text.Contains(current.Title) && t.Text.Contains(current.Date.ToString("yyyy-MM-dd"))),"Confirmation does not identify the program");
                check(store.LoadHistory().Count==2,"Program removed before confirmation");
                Reply(dialog,"取消");
                check(store.LoadHistory().Count==2 && ((ServicePlan)history.SelectedItem).Id==current.Id,"Cancel deleted or changed the program");
                history.SelectedItem=history.Items.Cast<ServicePlan>().Single(p=>p.Id==earlier.Id); Pump();
                check(((ServicePlan)Remove(window).DataContext).Id==earlier.Id,"Remove control did not follow selection");
            } finally { window.Close(); }
        });
        test("Deletion selects the nearest upcoming date, falls back to latest past, and stays deleted after closing",()=> {
            var store=new LocalStore(Path.Combine(root,"remove-next"));
            var older=Add(store,"較早崇拜",-14); var past=Add(store,"最近崇拜",-7);
            var near=Add(store,"下週崇拜",7); var far=Add(store,"下月崇拜",28);
            var current=Add(store,"目前崇拜",1); store.Save(current);
            var path=Path.Combine(store.History,current.Id+".json"); File.WriteAllText(path+".tmp","unfinished draft");
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                var history=window.FindControl<ListBox>("HistoryList");
                Reply(Ask(window),"移除");
                check(((ServicePlan)history.SelectedItem).Id==near.Id,"Selected edit recency or farthest date instead of nearest upcoming");
                check(!File.Exists(path) && !File.Exists(path+".bak") && !File.Exists(path+".tmp"),"Deleted service or recovery files remain");
                Reply(Ask(window),"移除"); check(((ServicePlan)history.SelectedItem).Id==far.Id,"Next upcoming program was not selected");
                Reply(Ask(window),"移除"); check(((ServicePlan)history.SelectedItem).Id==past.Id,"Latest past program was not selected");
            } finally { window.Close(); }
            check(store.LoadHistory().Select(p=>p.Id).Order().SequenceEqual(new[]{older.Id,past.Id}.Order()),"Autosave resurrected a deleted program");
        });
        test("Deleting the final program leaves a fresh editable blank program",()=> {
            var store=new LocalStore(Path.Combine(root,"remove-last")); var old=Add(store,"最後崇拜",0);
            var window=new MainWindow(store); window.Show(); Pump();
            try {
                Reply(Ask(window),"移除");
                var history=window.FindControl<ListBox>("HistoryList"); var next=(ServicePlan)history.SelectedItem;
                check(history.ItemCount==1 && next.Id!=old.Id && next.Items.Count==1 && next.Items[0].Lyrics=="","Last deletion did not leave a fresh blank editor");
                window.FindControl<TextBox>("Lyrics").Text="新的經文"; Pump();
                var feedback=window.FindControl<HyperlinkButton>("FeedbackButton");
                check(feedback.NavigateUri.AbsoluteUri=="https://docs.google.com/document/d/1-OT-NvNMkuqltQ3w_aVcpKyyfVkQGBNFl4gzb5dDkvU/edit","Feedback destination is incorrect");
            } finally { window.Close(); }
            check(store.LoadHistory().Single().Items[0].Lyrics=="新的經文","Fresh program cannot be edited or saved");
        });
    }
    public static void ReviewProfile(string root)
    {
        var store=new LocalStore(Path.Combine(root,"review-profile"));
        Add(store,"下週崇拜",8); Add(store,"上週崇拜",-6);
        var current=Add(store,"主日崇拜",1); current.Items[1].Background="media:ocean-photo"; store.Save(current);
        Console.WriteLine("Review profile: "+store.Root);
    }
}
