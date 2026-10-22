using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ComponentModel;
using System.Linq;
namespace WorshipPptx
{
    [DataContract] public class ServicePlan : INotifyPropertyChanged
    {
        [DataMember] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        private string title="主日崇拜";
        [DataMember] public string Title { get=>title; set { if(title==value) return; title=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Title))); } }
        [DataMember] public string FontName { get; set; } = FontCatalog.DefaultName;
        [DataMember] public double FontSize { get; set; } = 36;
        [DataMember] public string Alignment { get; set; } = "Center";
        [DataMember] public string TextColor { get; set; } = "#FFFFFF";
        private DateTime date=Demo.ComingSunday(DateTime.Today);
        [DataMember] public DateTime Date { get=>date; set { if(date==value) return; date=value; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(DisplayDate))); PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(IsPast))); } }
        [DataMember] public DateTime Updated { get; set; } = DateTime.Now;
        [DataMember] public List<ServiceItem> Items { get; set; } = new List<ServiceItem>();
        public string DisplayDate => Date.ToString("yyyy年M月d日");
        public bool IsPast => Date.Date<DateTime.Today;
        public string TabNames => string.Join("\n",Items?.Select(i=>string.IsNullOrWhiteSpace(i.Title) ? "未命名" : i.Title) ?? Enumerable.Empty<string>());
        private string lastTabNames;
        public void NotifyTabsChanged() { var names=TabNames; if(names==lastTabNames) return; lastTabNames=names; PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(TabNames))); }
        public event PropertyChangedEventHandler PropertyChanged;
        [OnDeserializing] private void SetDefaults(StreamingContext context) { FontName=FontCatalog.DefaultName; FontSize=36; Alignment="Center"; TextColor="#FFFFFF"; }
        [OnDeserialized] private void MigrateFormatting(StreamingContext context)
        {
            // Preserve the old service-wide appearance once, then each tab owns its own settings.
            if(Items!=null) foreach(var item in Items) item.Formatting ??= new SlideFormatting { FontName=FontName,FontSize=FontSize,Alignment=Alignment,TextColor=TextColor };
        }
        public bool MoveItem(ServiceItem item,int insertionIndex)
        {
            var from=Items.IndexOf(item);
            if(from<0 || insertionIndex<0 || insertionIndex>Items.Count) return false;
            if(insertionIndex>from) insertionIndex--;
            if(from==insertionIndex) return false;
            Items.RemoveAt(from); Items.Insert(insertionIndex,item); NotifyTabsChanged(); return true;
        }
    }
    [DataContract] public class ServiceItem
    {
        [DataMember] public string Kind { get; set; } = "song";
        [DataMember] public string Title { get; set; } = "新詩歌";
        [DataMember] public string Lyrics { get; set; } = "[v1]\n\n[c]\n";
        [DataMember] public string Order { get; set; } = "v1 c";
        [DataMember] public string Copyright { get; set; } = "";
        [DataMember] public string SongId { get; set; } = "";
        [DataMember] public string Background { get; set; } = "";
        [DataMember] public SlideFormatting Formatting { get; set; } = new();
        [DataMember(EmitDefaultValue=false)] public PreviewLayout Preview { get; set; }
    }
    [DataContract] public sealed class SlideFormatting
    {
        [DataMember] public string FontName { get; set; } = FontCatalog.DefaultName;
        [DataMember] public double FontSize { get; set; } = 36;
        [DataMember] public string Alignment { get; set; } = "Center";
        [DataMember] public string TextColor { get; set; } = "#FFFFFF";
        [DataMember] public bool Bold { get; set; }
        [DataMember] public bool FullSlide { get; set; }
        [DataMember] public string TextPosition { get; set; } = "Top";
        [OnDeserializing] private void SetDefaults(StreamingContext context) { FontName=FontCatalog.DefaultName; FontSize=36; Alignment="Center"; TextColor="#FFFFFF"; TextPosition="Top"; }
    }
    [DataContract] public class UserSettings
    {
        [DataMember] public string ChurchLicense { get; set; } = "";
    }
    public sealed class SlideData
    {
        public bool IsTitle { get; set; }
        public string Title { get; set; }
        public string FontName { get; set; } = FontCatalog.DefaultName;
        public double FontSize { get; set; } = 36;
        public string Alignment { get; set; } = "Center";
        public string Section { get; set; }
        public List<string> Lines { get; set; } = new List<string>();
        public string Footer { get; set; }
        public string Background { get; set; }
        public string TextColor { get; set; } = "#FFFFFF";
        public bool Bold { get; set; }
        public bool FullSlide { get; set; }
        public bool BottomHalf { get; set; }
        public PreviewLayout Preview { get; set; }
        public string TextPosition { get; set; } = "Top";
        public string Text => string.Join("\n", Lines);
    }
    public static class Demo
    {
        public static DateTime ComingSunday(DateTime today) => today.Date.AddDays((7-(int)today.DayOfWeek)%7);
        public static ServicePlan Create()
        {
            var date = ComingSunday(DateTime.Today);
            return new ServicePlan { Date = date, Items = new List<ServiceItem> {
                new ServiceItem { Kind="scripture", Title="宣召經文", Lyrics="詩篇 100:1–2（和合本）\n\n普天下當向耶和華歡呼！\n你們當樂意事奉耶和華，\n當來向他歌唱！", Order="", Copyright="和合本（1919，公版）" },
                new ServiceItem { Title="同心頌讚", Lyrics="[v1]\n今天聚集在主跟前\n同心獻上感恩頌讚\n\n[c]\n願我一生為祢而活\n讓祢的愛充滿我心\n\n[v2]\n在困倦裡祢賜盼望\n前路有祢不再孤單", Order="v1 c v2 c c", Copyright="原創示例歌詞 · 可自由使用" },
                new ServiceItem { Title="恩典伴我行", Lyrics="[v1]\n清晨祢恩典再次臨到\n每一步有祢的看顧\n\n[c]\n恩典伴我走過高山低谷\n祢的信實永遠不改變", Order="v1 c c", Copyright="原創示例歌詞 · 可自由使用" },
                new ServiceItem { Kind="scripture", Title="讀經", Lyrics="詩篇 23:1–3（和合本）\n\n耶和華是我的牧者，\n我必不致缺乏。\n\n他使我躺臥在青草地上，\n領我在可安歇的水邊。\n\n他使我的靈魂甦醒，\n為自己的名引導我走義路。", Order="", Copyright="和合本（1919，公版）" },
                new ServiceItem { Title="一生跟隨", Lyrics="[v1]\n願將今天交在祢手\n在平凡裡學習愛人\n\n[c]\n一生跟隨祢的腳步\n帶着盼望走向明天", Order="v1 c c", Copyright="原創示例歌詞 · 可自由使用" }
            }};
        }
    }
}
