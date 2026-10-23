using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorshipPptx;

public static class ImageKeywords
{
    public sealed class VisualTerms { public string TopicEn { get; set; } public string[] Zh { get; set; } public string[] En { get; set; } }
    private static readonly Lazy<Dictionary<string,VisualTerms>> themes=new(()=>JsonSerializer.Deserialize<Dictionary<string,VisualTerms>>(File.ReadAllText(Path.Combine(MediaLibrary.Root,"search-terms.json")),MediaLibrary.JsonOptions));
    private static readonly Lazy<Dictionary<string,HashSet<string>>> english=new(BuildEnglishLookup);

    // The app displays only Chinese. English equivalents are added when building the link.
    public static string ForDisplay(MediaScene scene)
    {
        VisualTerms terms=null;
        if(scene.Id!=null) themes.Value.TryGetValue(scene.Id,out terms);
        return ChineseText.Content(string.Join("、",new[]{Subject(scene)}
            .Concat(terms?.Zh ?? new[]{"柔和光線","寧靜氛圍","簡潔構圖"})));
    }

    public static IEnumerable<string> EnglishFor(string term) =>
        english.Value.TryGetValue(ChineseText.Convert(term,ChineseText.Simplified),out var matches) ? matches : Array.Empty<string>();

    private static string Subject(MediaScene scene) => Regex.Split(scene.Title ?? "",@"[、，,]+").First().Trim();
    private static Dictionary<string,HashSet<string>> BuildEnglishLookup()
    {
        var lookup=new Dictionary<string,HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string zh,string en)
        {
            if(string.IsNullOrWhiteSpace(zh) || string.IsNullOrWhiteSpace(en)) return;
            // Use the shared simplified form, since some traditional variants merge.
            var key=ChineseText.Convert(zh,ChineseText.Simplified);
            if(!lookup.TryGetValue(key,out var values)) lookup[key]=values=new(StringComparer.OrdinalIgnoreCase);
            values.Add(en);
        }
        var scenes=JsonSerializer.Deserialize<List<MediaScene>>(File.ReadAllText(Path.Combine(MediaLibrary.Root,"scenes.json")),MediaLibrary.JsonOptions);
        foreach(var scene in scenes)
        {
            if(!themes.Value.TryGetValue(scene.Id,out var terms)) continue;
            Add(Subject(scene),terms.TopicEn); Add(scene.Title,terms.TopicEn);
            foreach(var pair in (terms.Zh ?? Array.Empty<string>()).Zip(terms.En ?? Array.Empty<string>())) Add(pair.First,pair.Second);
        }
        return lookup;
    }
}
