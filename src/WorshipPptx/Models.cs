using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace WorshipPptx
{
    [DataContract] public class ServicePlan
    {
        [DataMember] public string Id { get; set; } = Guid.NewGuid().ToString("N");
        [DataMember] public string Title { get; set; } = "主日崇拜";
        [DataMember] public string FontName { get; set; } = FontCatalog.DefaultName;
        [DataMember] public DateTime Date { get; set; } = DateTime.Today;
        [DataMember] public DateTime Updated { get; set; } = DateTime.Now;
        [DataMember] public List<ServiceItem> Items { get; set; } = new List<ServiceItem>();
        public string DisplayDate => Date.ToString("yyyy年M月d日");
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
    }
    [DataContract] public class UserSettings
    {
        [DataMember] public string ChurchLicense { get; set; } = "";
    }
    public sealed class SlideData
    {
        public string Title { get; set; }
        public string FontName { get; set; } = FontCatalog.DefaultName;
        public string Section { get; set; }
        public List<string> Lines { get; set; } = new List<string>();
        public string Footer { get; set; }
        public string Background { get; set; }
        public string Text => string.Join("\n", Lines);
    }
    public static class Demo
    {
        public static ServicePlan Create()
        {
            var date = DateTime.Today.AddDays(((int)DayOfWeek.Sunday - (int)DateTime.Today.DayOfWeek + 7) % 7);
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
