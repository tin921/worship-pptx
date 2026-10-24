using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace WorshipPptx.Tests;

static class LanguageTests
{
    public static void Run(Action<string,Action> test,Action<bool,string> check,string root)
    {
        test("Chinese conversion is phrase-aware and keeps English, tags, whitespace and numbers",()=> {
            var source="[v1]\n  頭髮與發光\n\t皇后與後來\n\n[c]\n願祢的愛充滿我心\nCCLI #12345 Amazing Grace";
            var simplified=ChineseText.Convert(source,ChineseText.Simplified);
            check(simplified=="[v1]\n  头发与发光\n\t皇后与后来\n\n[c]\n愿祢的爱充满我心\nCCLI #12345 Amazing Grace","Incorrect script conversion: "+simplified);
            check(ChineseText.Convert("头发与发光，皇后与后来",ChineseText.Traditional)=="頭髮與發光，皇后與後來","Context-sensitive Traditional conversion failed");
            check(ChineseText.Convert("祢的恩典與慈愛，祂的看顧",ChineseText.Traditional)=="祢的恩典與慈愛，祂的看顧","Worship pronouns were rewritten");
        });
        test("Switching and autosaving preserve original Traditional distinctions and old JSON format",()=> {
            ChineseText.SetLanguage(ChineseText.Traditional);
            var store=new LocalStore(Path.Combine(root,"language-source-"+Guid.NewGuid().ToString("N")));
            var item=new ServiceItem { Title="愛與盼望",Lyrics="[v1]\n皇后與後來\n頭髮與發光",Order="v1",Copyright="版權所有 © 2026",SongId="012345",Background="media:unchanged" };
            var plan=new ServicePlan { Title="感恩頌讚",Items={item} };
            store.Save(plan);
            try {
                ChineseText.SetLanguage(ChineseText.Simplified);
                check(plan.Title=="感恩颂赞" && item.Title=="爱与盼望" && item.Copyright=="版权所有 © 2026","Title/credits did not convert");
                // Mimic Capture: setting an unchanged displayed value must not replace its source.
                plan.Title=plan.Title; item.Title=item.Title; item.Lyrics=item.Lyrics; item.Copyright=item.Copyright;
                store.Save(plan);
                using(var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(store.History,plan.Id+".json")))) {
                    check(json.RootElement.GetProperty("Title").GetString()=="感恩頌讚","Converted service title overwrote source");
                    check(json.RootElement.GetProperty("Items")[0].GetProperty("Lyrics").GetString().Contains("頭髮與發光"),"Converted lyrics overwrote source");
                }
                var reopened=store.LoadHistory().Single();
                check(reopened.Items[0].Lyrics.Contains("头发与发光"),"Reload did not use current script");
                ChineseText.SetLanguage(ChineseText.Traditional);
                check(reopened.Items[0].Lyrics=="[v1]\n皇后與後來\n頭髮與發光","Original character distinctions lost");
                check(item.SongId=="012345" && item.Background=="media:unchanged" && item.Order=="v1","Non-text fields changed");
                var legacy=Path.Combine(root,"language-old-settings.json"); File.WriteAllText(legacy,"{\"ChurchLicense\":\"0099\"}");
                check(LocalStore.Read<UserSettings>(legacy).Language==ChineseText.Traditional,"Old settings must default to Traditional");
            } finally { ChineseText.SetLanguage(ChineseText.Traditional); }
        });
        test("Language switch updates interface, history, editor and previews immediately and persists",()=> {
            var store=new LocalStore(Path.Combine(root,"language-ui-"+Guid.NewGuid().ToString("N")));
            var saveFolder=Path.Combine(root,"chosen-pptx-folder"); store.SaveSettings(new UserSettings {PptxSaveFolder=saveFolder});
            var plan=Demo.Create(); store.Save(plan);
            var other=Demo.Create(); other.Title="聖誕禮拜"; store.Save(other); plan.Updated=DateTime.Now.AddSeconds(1); store.Save(plan);
            var window=new MainWindow(store); window.Show(); Dispatcher.UIThread.RunJobs();
            try {
                var tabs=window.FindControl<TabControl>("SongTabs"); tabs.SelectedIndex=1;
                var lyrics=window.FindControl<TextBox>("Lyrics"); lyrics.Text="[v1]\n頭髮與發光\n願祢的愛充滿我心";
                window.FindControl<TextBox>("SongOrder").Text="v1";
                window.FindControl<ComboBox>("FontSizePicker").SelectedItem=40.0;
                var simplified=window.FindControl<ToggleButton>("SimplifiedButton");
                simplified.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
                check(ChineseText.IsSimplified && simplified.IsChecked==true && window.FindControl<ToggleButton>("TraditionalButton").IsChecked==false,"Switch state incorrect");
                check(window.Title=="敬拜工坊 · Worship PPTX" && window.FindControl<TextBlock>("AppHeading").Text=="敬拜工坊","XAML bindings did not update");
                check(lyrics.Text=="[v1]\n头发与发光\n愿祢的爱充满我心" && tabs.SelectedIndex==1,"Draft lost or tab changed");
                check((string)((TabItem)tabs.Items[1]).Header=="同心颂赞","Tab title stayed Traditional");
                check(window.FindControl<ListBox>("HistoryList").Items.Cast<ServicePlan>().Any(p=>p.Title=="圣诞礼拜"),"History stayed Traditional");
                check(window.FindControl<ComboBox>("FontPicker").SelectedItem is FontChoice font && font.DisplayName.Contains("香港黑体"),"Font labels stayed Traditional");
                check((double)window.FindControl<ComboBox>("FontSizePicker").SelectedItem==40,"Switch changed formatting");
                check((string)ToolTip.GetTip(window.FindControl<ToggleButton>("FlipBackground"))=="水平翻转","Image control tooltip stayed Traditional");
                check(store.LoadSettings().Language==ChineseText.Simplified,"Language not saved");
                check(store.LoadSettings().PptxSaveFolder==saveFolder,"Language switch reset the chosen PowerPoint folder");
                simplified.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                check(simplified.IsChecked==true,"Selected language can be deselected");
                window.FindControl<ToggleButton>("TraditionalButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
                check(lyrics.Text.Contains("頭髮與發光") && window.Title=="敬拜工坊 · Worship PPTX","Switch back lost original or interface");
                simplified.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            } finally { window.Close(); }
            var reopened=new MainWindow(store); reopened.Show(); Dispatcher.UIThread.RunJobs();
            try { check(reopened.FindControl<TextBlock>("AppHeading").Text=="敬拜工坊" && reopened.FindControl<TextBox>("Lyrics").Text.Contains("头发与发光"),"Language did not survive restart"); }
            finally { reopened.Close(); ChineseText.SetLanguage(ChineseText.Traditional); }
        });
        test("Both Chinese scripts export valid editable song, Scripture and announcement slides",()=> {
            var plan=new ServicePlan { Title="主日崇拜",Items={
                new ServiceItem { Title="愛與盼望",Lyrics="[v1]\n願祢的愛充滿我心",Order="v1",Copyright="版權所有",SongId="123" },
                new ServiceItem { Kind="scripture",Title="宣召經文",Lyrics="詩篇：耶和華是我的牧者",Order="" },
                new ServiceItem { Title="教會公告",Lyrics="[1]\n歡迎參加禱告會",Order="" }
            }};
            try {
                foreach(var language in new[]{ChineseText.Simplified,ChineseText.Traditional}) {
                    ChineseText.SetLanguage(language);
                    var slides=SlideLayout.Build(plan,new UserSettings { ChurchLicense="456",Language=language });
                    var path=Path.Combine(root,"language-"+language+".pptx"); PptxWriter.Write(path,slides,plan.Title);
                    using(var ppt=PresentationDocument.Open(path,false)) check(!new OpenXmlValidator().Validate(ppt).Any(),"Export schema failed");
                    using var zip=ZipFile.OpenRead(path); XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";
                    var pages=zip.Entries.Where(e=>e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml")).Select(e=> { using var input=e.Open(); return XDocument.Load(input); }).ToList();
                    var text=string.Join("\n",pages.SelectMany(p=>p.Descendants(a+"t").Select(t=>t.Value)));
                    check(text.Contains(ChineseText.Content("願祢的愛充滿我心")) && text.Contains(ChineseText.Content("詩篇：耶和華是我的牧者")) && text.Contains(ChineseText.Content("歡迎參加禱告會")),"Export differs from preview language");
                    var joined=string.Concat(pages.SelectMany(p=>p.Descendants(a+"t").Select(t=>t.Value)));
                    check(joined.Contains("CCLI Song #123") && joined.Contains("CCLI Licence #456") && joined.Contains(ChineseText.Content("版權所有")),"Credits lost");
                    check(pages.SelectMany(p=>p.Descendants(a+"rPr")).All(p=>(string)p.Attribute("lang")==ChineseText.CultureName),"PPTX proofing language incorrect");
                }
            } finally { ChineseText.SetLanguage(ChineseText.Traditional); }
        });
    }
}
