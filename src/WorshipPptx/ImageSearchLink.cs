using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorshipPptx;

public static class ImageSearchLink
{
    // Only these editable visual keywords are sent to Google when the user opens the link.
    public static Uri Create(string keywords)
    {
        var groups=Regex.Split((keywords ?? "").Trim(),@"[、，,;；\r\n]+")
            .Select(Alternatives).Where(g=>g.Length>0).ToArray();
        if(groups.Length==0) throw new ArgumentException("Enter a subject for image search.",nameof(keywords));
        // Keep the subject mandatory. A light/mood modifier must never match by itself.
        var subject=groups[0];
        var details=groups.Skip(1).SelectMany(g=>g).Except(subject,StringComparer.OrdinalIgnoreCase).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var words="(Christian OR Bible OR 基督教 OR 聖經 OR 圣经) "+Any(subject)
            +(details.Length>0 ? " "+Any(details) : "")+" (background OR 背景)";
        // No language restriction: concepts can match either Chinese script or English.
        // Google controls these filters; 2 MP is not an exact 1280-by-720 guarantee.
        return new("https://www.google.com/search?tbm=isch&q="+Uri.EscapeDataString(words)+"&imgsz=2mp&imgar=w&tbs=isz:lt,islt:2mp,iar:w");
    }

    private static string Any(IEnumerable<string> terms) => "("+string.Join(" OR ",terms.Select(s=>"\""+s+"\""))+")";
    private static string[] Alternatives(string group) => Regex.Split(group,@"[/／]+|\s+OR\s+",RegexOptions.IgnoreCase)
        .Select(s=>Regex.Replace(s.Replace('"',' ').Replace('“',' ').Replace('”',' '),@"\s+"," ").Trim())
        .Where(s=>s.Length>0).SelectMany(s=>new[]{s,ChineseText.Convert(s,ChineseText.Traditional),ChineseText.Convert(s,ChineseText.Simplified)})
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
}
