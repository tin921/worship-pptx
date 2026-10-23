using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WorshipPptx;

public static class PresentationFileName
{
    public static string Create(ServicePlan plan,DateTime today,string fallbackLanguage)
    {
        var content=string.Join("\n",plan.Items.Select(i=>i.Title+"\n"+i.Lyrics));
        var simplified=0; var traditional=0;
        foreach(var rune in content.EnumerateRunes()) {
            var value=rune.ToString();
            if(ChineseText.Convert(value,ChineseText.Traditional)!=value) simplified++;
            if(ChineseText.Convert(value,ChineseText.Simplified)!=value) traditional++;
        }
        var language=simplified==traditional ? (fallbackLanguage==ChineseText.Simplified ? "m" : "c") : simplified>traditional ? "m" : "c";
        var first=plan.Items.FirstOrDefault();
        var invocation=first!=null && !string.IsNullOrWhiteSpace(first.Lyrics) && !SlideLayout.HasSectionTags(first.Lyrics) && !SlideLayout.HasAnnouncementTags(first.Lyrics);
        return Demo.ComingSunday(today).ToString("MMdd",CultureInfo.InvariantCulture)+language+"-"+(invocation ? "call+" : "")+"praise.pptx";
    }
}
