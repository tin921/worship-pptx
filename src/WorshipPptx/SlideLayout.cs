using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
namespace WorshipPptx
{
    public static class SlideLayout
    {
        public const double Width = 1280, Height = 720, TextX = 72, TextY = 68, TextWidth = 1136, FontSize = 48, LineHeight = 66;
        public const int LinesPerSlide = 3;
        public const string Font = FontCatalog.DefaultName;
        private static readonly Regex Tag = new Regex(@"^\[([a-z][a-z0-9]*)\]$", RegexOptions.IgnoreCase);
        public static List<SlideData> Build(ServicePlan plan, UserSettings settings)
        {
            if (plan.Items == null || plan.Items.Count == 0) throw new FormatException("請先加入詩歌或經文。 ");
            return plan.Items.SelectMany(i => BuildItem(i, settings, plan.FontName)).ToList();
        }
        public static List<SlideData> BuildItem(ServiceItem item, UserSettings settings, string fontName = Font)
        {
            if (string.IsNullOrWhiteSpace(item.Title)) throw new FormatException("請填寫詩歌或經文名稱。 ");
            if (string.IsNullOrWhiteSpace(item.Lyrics)) throw new FormatException(item.Title + "：請輸入歌詞或經文。");
            var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var names = new List<string>();
            var current = "text";
            sections[current] = new List<string>();
            var tagged = false;
            foreach (var raw in item.Lyrics.Replace("\r", "").Split('\n'))
            {
                var line = raw.Trim();
                var match = Tag.Match(line);
                if (item.Kind == "song" && match.Success)
                {
                    tagged = true; current = match.Groups[1].Value.ToLowerInvariant();
                    if (sections.ContainsKey(current)) throw new FormatException(item.Title + "：段落 [" + current + "] 重複；重唱請在演唱次序填寫。");
                    sections[current] = new List<string>(); names.Add(current);
                }
                else
                {
                    if (item.Kind == "song" && (line.StartsWith("[") || line.EndsWith("]"))) throw new FormatException(item.Title + "：段落標記格式不正確，請使用 [v1]、[c] 等。");
                    sections[current].Add(line);
                }
            }
            if (tagged && sections["text"].Any(x => x.Length > 0)) throw new FormatException(item.Title + "：第一行歌詞前請加上 [v1] 等段落標記。");
            var order = tagged ? (string.IsNullOrWhiteSpace(item.Order) ? names : Regex.Split(item.Order.Trim().ToLowerInvariant(), @"[\s,，;；]+" ).ToList()) : new List<string> { "text" };
            if (!tagged && item.Kind == "song" && !string.IsNullOrWhiteSpace(item.Order)) throw new FormatException(item.Title + "：已填演唱次序，請在歌詞加入相應段落標記。");
            var slides = new List<SlideData>();
            foreach (var key in order)
            {
                if (!sections.ContainsKey(key)) throw new FormatException(item.Title + "：找不到 [" + key + "]，請檢查演唱次序。");
                if (!sections[key].Any(x => x.Length > 0)) throw new FormatException(item.Title + "：段落 [" + key + "] 沒有歌詞。");
                var pending = new List<string>();
                Action flush = () => { if (pending.Count == 0) return; slides.Add(new SlideData { Title = item.Title, FontName = FontCatalog.Get(fontName).Name, Section = key == "text" ? "經文" : key.ToUpperInvariant(), Lines = new List<string>(pending), Footer = Footer(item, settings), Background = item.Background }); pending.Clear(); };
                foreach (var line in sections[key])
                {
                    if (line.Length == 0) { flush(); continue; }
                    foreach (var wrapped in Wrap(line, fontName)) { pending.Add(wrapped); if (pending.Count == LinesPerSlide) flush(); }
                }
                flush();
            }
            return slides;
        }
        public static string Footer(ServiceItem item, UserSettings settings)
        {
            var parts = new List<string> { item.Copyright ?? "" };
            if (!string.IsNullOrWhiteSpace(item.SongId)) parts.Add("CCLI Song #" + item.SongId);
            if (item.Kind == "song" && !string.IsNullOrWhiteSpace(settings.ChurchLicense)) parts.Add("CCLI Licence #" + settings.ChurchLicense);
            return string.Join("  ·  ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        public static List<string> Wrap(string input, string fontName = Font)
        {
            var result = new List<string>();
            var line = new StringBuilder();
            // Text elements preserve surrogate pairs and combining characters.
            var enumerator = StringInfo.GetTextElementEnumerator(input);
            while (enumerator.MoveNext())
            {
                var ch = enumerator.GetTextElement();
                if (line.Length > 0 && Measure(line.ToString() + ch, FontSize, fontName) > TextWidth - 24)
                {
                    var text = line.ToString(); var space = text.LastIndexOf(' ');
                    if (space > text.Length / 2) { result.Add(text.Substring(0, space)); line.Clear(); line.Append(text.Substring(space + 1)); }
                    else { result.Add(text); line.Clear(); }
                }
                line.Append(ch);
            }
            if (line.Length > 0) result.Add(line.ToString().Trim());
            return result;
        }
        public static double Measure(string text, double size, string fontName = Font) => new FormattedText(text, CultureInfo.GetCultureInfo("zh-HK"), FlowDirection.LeftToRight, new Typeface(FontCatalog.Get(fontName).Family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), size, Brushes.White, 1.0).WidthIncludingTrailingWhitespace;
        public static double FooterSize(string text, string fontName = Font) => Math.Min(14, 14 * TextWidth / Math.Max(TextWidth, Measure(text ?? "", 14, fontName)));
    }
}
