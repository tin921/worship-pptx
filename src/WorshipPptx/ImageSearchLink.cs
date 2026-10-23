using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WorshipPptx;

public static class ImageSearchLink
{
    // Only these editable visual keywords are sent to Google when the user opens the link.
    public static Uri Create(string keywords,bool suggested=false)
    {
        var groups=Regex.Split((keywords ?? "").Trim(),@"[、，,;；\r\n]+")
            .Where(g=>!string.IsNullOrWhiteSpace(g)).ToArray();
        if(groups.Length==0) throw new ArgumentException("Enter a subject for image search.",nameof(keywords));
        // The generated light/mood details describe our library, but requiring them
        // on Google hides useful backgrounds. Start with its English subject only.
        // Edited searches may add up to three distinct phrases, without exact quotes.
        var terms=(suggested ? groups.Take(1) : groups).SelectMany(EnglishTerms)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(suggested ? 1 : 3).ToArray();
        if(terms.Length==0) throw new ArgumentException("Enter a subject for image search.",nameof(keywords));
        var words="Christian worship slide background "+string.Join(" ",terms);
        // 720p is a video-related keyword, not a minimum-size constraint. Request
        // Google's Large/Wide filters instead; the downloaded dimensions still vary.
        return new("https://www.google.com/search?udm=2&q="+Uri.EscapeDataString(words)+"&imgsz=l&imgar=w&tbs=isz:l,iar:w");
    }

    private static IEnumerable<string> EnglishTerms(string group)
    {
        var alternatives=Regex.Split(group,@"[/／]+|\bOR\b",RegexOptions.IgnoreCase)
            .Select(s=>Regex.Replace(Regex.Replace(s,@"[^\p{L}\p{N}\s]"," "),@"\s+"," ").Trim())
            .Where(s=>s.Length>0).ToArray();
        var english=alternatives.Select(s=>ImageKeywords.EnglishFor(s).FirstOrDefault())
            .Where(s=>s!=null).Concat(alternatives.Where(s=>Regex.IsMatch(s,@"^[A-Za-z0-9\s]+$")))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        // Never invent a translation for unknown custom text or substitute a stale
        // matched theme. All 80 built-in subjects have curated English equivalents.
        return english.Length>0 ? english : alternatives;
    }
}
