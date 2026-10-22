using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorshipPptx;

public static class ImageSearchLink
{
    // Only these editable visual keywords are sent to Google when the user opens the link.
    public static Uri Create(string keywords)
    {
        var terms=Regex.Split((keywords ?? "").Trim(),@"[、，,;；\r\n]+|\s+OR\s+",RegexOptions.IgnoreCase)
            .Select(s=>Regex.Replace(s.Replace('"',' '),@"\s+"," ").Trim()).Where(s=>s.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var words="("+string.Join(" OR ",terms.Select(s=>"\""+s+"\""))+")";
        var background=Regex.IsMatch(words,@"[\u3400-\u9fff]") ? "背景" : "background";
        // Google has no exact 720p-minimum option. Its 2 MP minimum exceeds 720p's pixel count.
        return new("https://www.google.com/search?tbm=isch&hl=zh-HK&q="+Uri.EscapeDataString(words+" "+background)+"&imgsz=2mp&imgar=w&tbs=isz:lt,islt:2mp,iar:w");
    }
}
