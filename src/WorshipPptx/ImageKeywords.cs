using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WorshipPptx;

// Choose display/search language from the original song, before embedding normalization.
public static class ImageKeywords
{
    public sealed class VisualTerms { public string[] Zh { get; set; } public string[] En { get; set; } }
    private enum Language { TraditionalChinese,SimplifiedChinese,English }
    private static readonly Lazy<Dictionary<string,string>> characters=new(()=>
        JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Models","normalization.json"))));
    private static readonly Lazy<HashSet<string>> simplifiedOnly=new(()=>characters.Value.Values.Except(characters.Value.Keys).ToHashSet());
    private static readonly Lazy<Dictionary<string,VisualTerms>> themes=new(()=>JsonSerializer.Deserialize<Dictionary<string,VisualTerms>>(File.ReadAllText(Path.Combine(MediaLibrary.Root,"search-terms.json")),MediaLibrary.JsonOptions));

    public static string ForSong(MediaScene scene,string title,string lyrics)
    {
        // Lyrics determine a bilingual song's language; a title is used when lyrics are empty.
        var language=Detect(lyrics) ?? Detect(title) ?? Language.TraditionalChinese;
        var label=Regex.Split(scene.Title ?? "",@"[、，,]+").First().Trim();
        VisualTerms terms=null;
        if(scene.Id!=null) themes.Value.TryGetValue(scene.Id,out terms);
        var chinese=string.Join("、",new[]{label}.Concat(terms?.Zh ?? new[]{"柔和光線","寧靜氛圍","簡潔構圖"}));
        if(language==Language.English && !string.IsNullOrWhiteSpace(scene.Query)) return string.Join(", ",new[]{scene.Query}.Concat(terms?.En ?? new[]{"soft light","peaceful mood","simple composition"}));
        if(language!=Language.SimplifiedChinese) return chinese;
        var result=new StringBuilder();
        foreach(var rune in chinese.EnumerateRunes()) {
            var character=rune.ToString();
            result.Append(characters.Value.TryGetValue(character,out var simplified) ? simplified : character);
        }
        return result.ToString();
    }

    private static Language? Detect(string input)
    {
        var text=Regex.Replace(input ?? "",@"\[[^\]\r\n]*\]","");
        text=string.Join(" ",text.Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0).Distinct());
        var han=text.EnumerateRunes().Where(r=>r.Value is >=0x3400 and <=0x9FFF or >=0xF900 and <=0xFAFF or >=0x20000 and <=0x323AF).Select(r=>r.ToString()).ToList();
        var english=Regex.Matches(text,@"[A-Za-z]+(?:['’-][A-Za-z]+)*").Count;
        if(han.Count==0) return english>0 ? Language.English : null;
        if(english>han.Count) return Language.English;
        // Ignore worship pronouns: 祢/祂 are not evidence of Traditional orthography.
        var traditional=han.Count(c=>c!="祢" && c!="祂" && characters.Value.ContainsKey(c));
        var simplified=han.Count(simplifiedOnly.Value.Contains);
        return simplified>traditional ? Language.SimplifiedChinese : Language.TraditionalChinese;
    }
}
