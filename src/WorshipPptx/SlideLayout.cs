using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Media;
namespace WorshipPptx
{
    public static class SlideLayout
    {
        public const double Width = 1280, Height = 720, TextX = 72, TextY = 68, TextWidth = 1136, FontSize = 48, LineHeight = 66;
        public const int LinesPerSlide = 3;
        public const string Font = FontCatalog.DefaultName;
        private static readonly Regex Tag = new Regex(@"^\[([a-z][a-z0-9]*)\]$", RegexOptions.IgnoreCase);
        private static readonly Regex AnnouncementTag = new Regex(@"^\[([0-9]+)\]$");
        public static bool HasSectionTags(string text) => (text ?? "").Replace("\r","").Split('\n').Any(line=>Tag.IsMatch(line.Trim()));
        public static bool HasAnnouncementTags(string text) => (text ?? "").Replace("\r","").Split('\n').Any(line=>AnnouncementTag.IsMatch(line.Trim()));
        public static List<SlideData> Build(ServicePlan plan, UserSettings settings)
        {
            if (plan.Items == null || plan.Items.Count == 0) throw new FormatException(ChineseText.Ui("請先加入詩歌或經文。 "));
            return plan.Items.SelectMany(i => BuildItem(i, settings)).ToList();
        }
        public static List<SlideData> BuildItem(ServiceItem item, UserSettings settings)
        {
            var f=item.Formatting ?? new SlideFormatting();
            var layout=item.Preview ?? PreviewLayout.FromLegacy(f.FullSlide);
            layout.Normalize();
            var automatic=f.AutoFontSize && HasSectionTags(item.Lyrics);
            var result=BuildItem(item,settings,f.FontName,automatic ? 72 : f.FontSize,f.Alignment,f.TextColor,f.Bold,layout.TextArea=="Full",f.TextPosition,f.AutoFontSize);
            foreach(var slide in result) { slide.BottomHalf=layout.TextArea=="Bottom"; slide.Preview=layout; slide.BackgroundTransform=(item.BackgroundTransform ?? new()).Copy(); }
            return result;
        }
        public static List<SlideData> BuildItem(ServiceItem item, UserSettings settings, string fontName, double fontSize = 36, string alignment = "Center",string textColor="#FFFFFF",bool bold=false,bool fullSlide=false,string textPosition="Top",bool autoFit=true)
        {
            if(double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize<18 || fontSize>72) throw new FormatException(ChineseText.Ui("字號須為 18 至 72 pt。"));
            if(alignment!="Left" && alignment!="Center" && alignment!="Right") throw new FormatException(ChineseText.Ui("請選擇靠左、置中或靠右。"));
            if(textPosition!="Top" && textPosition!="Middle" && textPosition!="Bottom") throw new FormatException(ChineseText.Ui("請選擇文字靠上、置中或靠下。"));
            if (string.IsNullOrWhiteSpace(item.Title)) throw new FormatException(ChineseText.Ui("請填寫詩歌或經文名稱。 "));
            if (string.IsNullOrWhiteSpace(item.Lyrics)) throw new FormatException(item.Title + ChineseText.Ui("：請輸入歌詞或經文。"));
            var tagged=HasSectionTags(item.Lyrics);
            if(HasAnnouncementTags(item.Lyrics)) {
                if(tagged) throw new FormatException(item.Title+ChineseText.Ui("：公告請使用 [1]、[2]，不要與詩歌段落標記混用。"));
                return BuildAnnouncements(item,settings,fontName,fontSize,alignment,textColor,bold,fullSlide,textPosition);
            }
            var requestedSize=fontSize;
            var titleCredits=Footer(item,settings);
            var footer=tagged ? "" : titleCredits;
            while(TextCapacity(fontSize,fontName,footer,bold,fullSlide,false)<1 && fontSize>18) fontSize=Math.Max(18,fontSize-1);
            var maxLines=TextCapacity(fontSize,fontName,footer,bold,fullSlide,false);
            if(maxLines<1) throw new FormatException(item.Title+ChineseText.Ui("：版權資料太長，請縮短文字或減少字號。"));
            var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var names = new List<string>();
            var current = "text";
            sections[current] = new List<string>();
            foreach (var raw in item.Lyrics.Replace("\r", "").Split('\n'))
            {
                var match = Tag.Match(raw.Trim());
                if (match.Success)
                {
                    current = match.Groups[1].Value.ToLowerInvariant();
                    if (sections.ContainsKey(current)) throw new FormatException(item.Title + ChineseText.Ui("：段落 [") + current + ChineseText.Ui("] 重複；重唱請在演唱次序填寫。"));
                    sections[current] = new List<string>(); names.Add(current);
                }
                else
                {
                    if (tagged && (raw.TrimStart().StartsWith("[") || raw.TrimEnd().EndsWith("]"))) throw new FormatException(item.Title + ChineseText.Ui("：段落標記格式不正確，請使用 [v1]、[c] 等。"));
                    sections[current].Add(raw);
                }
            }
            if (tagged && sections["text"].Any(x => !string.IsNullOrWhiteSpace(x))) throw new FormatException(item.Title + ChineseText.Ui("：第一行歌詞前請加上 [v1] 等段落標記。"));
            var order = tagged ? (string.IsNullOrWhiteSpace(item.Order) ? names : Regex.Split(item.Order.Trim().ToLowerInvariant(), @"[\s,，;；]+" ).ToList()) : new List<string> { "text" };
            var slides = new List<SlideData>();
            foreach (var key in order)
            {
                if (!sections.ContainsKey(key)) throw new FormatException(item.Title + ChineseText.Ui("：找不到 [") + key + ChineseText.Ui("]，請檢查演唱次序。"));
                if (!sections[key].Any(x => !string.IsNullOrWhiteSpace(x))) throw new FormatException(item.Title + ChineseText.Ui("：段落 [") + key + ChineseText.Ui("] 沒有歌詞。"));
                if(tagged && autoFit)
                {
                    // A tag is one complete lyric block. Blank lines inside it are
                    // spacing; only the blank separator around the tag is omitted.
                    var source=sections[key].SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse().ToList();
                    var slide=new SlideData { Title=item.Title,FontName=FontCatalog.Get(fontName).Name,FontSize=requestedSize,Alignment=alignment,Section=key.ToUpperInvariant(),ShowSectionTag=true,Footer="",Background=item.Background,TextColor=TextColors.Get(textColor).Hex,Bold=bold,FullSlide=fullSlide,TextPosition=textPosition };
                    FitBlock(slide,source,requestedSize,item.Title+ChineseText.Ui("：段落 [")+key+ChineseText.Ui("] 內容太多，請拆分段落。"),preserveLineBreaks:true);
                    slides.Add(slide);
                    continue;
                }
                var pending = new List<string>();
                var firstSlideOfBlock=true;
                Action flush = () => { if (pending.Count == 0) return; slides.Add(new SlideData { Title = item.Title, FontName = FontCatalog.Get(fontName).Name, FontSize = fontSize, Alignment = alignment, Section = key == "text" ? ChineseText.Ui("經文／公告") : key.ToUpperInvariant(), ShowSectionTag=tagged && firstSlideOfBlock, Lines = new List<string>(pending), Footer = footer, Background = item.Background, TextColor=TextColors.Get(textColor).Hex,Bold=bold,FullSlide=fullSlide,TextPosition=textPosition }); pending.Clear(); firstSlideOfBlock=false; };
                var content=tagged ? sections[key].SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse() : sections[key];
                foreach (var line in content)
                {
                    if(line.Length==0) { pending.Add(""); if(pending.Count==maxLines) flush(); continue; }
                    foreach (var wrapped in Wrap(line, fontName, fontSize,bold)) { pending.Add(wrapped); if (pending.Count == maxLines) flush(); }
                }
                flush();
            }
            if(tagged)
            {
                var titleSize=requestedSize;
                var titleText=Regex.Replace(item.Title.Trim(),@"\s+"," ");
                var titleLines=Wrap(titleText,fontName,titleSize,bold);
                while(titleLines.Count>TextCapacity(titleSize,fontName,titleCredits,bold,fullSlide) && titleSize>18)
                {
                    titleSize=Math.Max(18,titleSize-1);
                    titleLines=Wrap(titleText,fontName,titleSize,bold);
                }
                if(titleLines.Count>TextCapacity(titleSize,fontName,titleCredits,bold,fullSlide)) throw new FormatException(ChineseText.Ui("詩歌名稱或版權資料太長，請縮短文字。"));
                slides.Insert(0,new SlideData { IsTitle=true, Title=item.Title, Section=ChineseText.Ui("歌名"), Lines=titleLines, FontName=FontCatalog.Get(fontName).Name, FontSize=titleSize, Alignment=alignment, Footer=titleCredits, Background=item.Background, TextColor=TextColors.Get(textColor).Hex,Bold=bold,FullSlide=fullSlide,TextPosition=textPosition });
                if(autoFit) {
                    // One size for the song, determined by the block that needs
                    // the most room. Include the title/credits to prevent overlap.
                    var commonSize=slides.Min(s=>s.FontSize);
                    foreach(var slide in slides) {
                        slide.FontSize=commonSize;
                        var source=slide.IsTitle ? new[]{titleText} : sections[slide.Section].SkipWhile(string.IsNullOrWhiteSpace).Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse();
                        slide.Lines=slide.IsTitle ? Wrap(titleText,slide.FontName,commonSize,bold) : source.Select(line=>line.Replace("\t","    ")).ToList();
                    }
                }
            }
            foreach(var slide in slides) slide.IsSong=tagged;
            return NumberItemSlides(slides);
        }
        private static List<SlideData> NumberItemSlides(List<SlideData> slides)
        {
            for(var i=0;i<slides.Count;i++) { slides[i].ItemSlideNumber=i+1; slides[i].ItemSlideCount=slides.Count; }
            return slides;
        }
        private static List<SlideData> BuildAnnouncements(ServiceItem item,UserSettings settings,string fontName,double maximumSize,string alignment,string textColor,bool bold,bool fullSlide,string textPosition)
        {
            var items=new List<(string Number,List<string> Lines)>();
            var numbers=new HashSet<string>();
            foreach(var raw in item.Lyrics.Replace("\r","").Split('\n')) {
                var match=AnnouncementTag.Match(raw.Trim());
                if(match.Success) {
                    var number=match.Groups[1].Value.TrimStart('0'); if(number.Length==0) number="0";
                    if(!numbers.Add(number)) throw new FormatException(item.Title+ChineseText.Ui("：公告 [")+number+ChineseText.Ui("] 重複。"));
                    items.Add((number,new List<string>()));
                }
                else if(items.Count>0) items[^1].Lines.Add(raw);
                else if(!string.IsNullOrWhiteSpace(raw)) throw new FormatException(item.Title+ChineseText.Ui("：請在第一項公告前加上 [1]。"));
            }
            var slides=new List<SlideData>();
            foreach(var entry in items) {
                if(!entry.Lines.Any(line=>!string.IsNullOrWhiteSpace(line))) throw new FormatException(item.Title+ChineseText.Ui("：公告 [")+entry.Number+ChineseText.Ui("] 沒有內容。"));
                var slide=new SlideData { Title=item.Title,Section=ChineseText.Ui("公告 ")+entry.Number,FontName=FontCatalog.Get(fontName).Name,FontSize=maximumSize,Alignment=alignment,TextColor=TextColors.Get(textColor).Hex,Bold=bold,FullSlide=fullSlide,TextPosition=textPosition,Footer=Footer(item,settings),Background=item.Background };
                FitBlock(slide,entry.Lines,maximumSize,item.Title+ChineseText.Ui("：公告 [")+entry.Number+ChineseText.Ui("] 內容太多，請縮短文字。"));
                slides.Add(slide);
            }
            return NumberItemSlides(slides);
        }
        private static void FitBlock(SlideData slide,List<string> source,double maximumSize,string error,bool preserveLineBreaks=false)
        {
            bool Fit(double size) {
                slide.FontSize=size;
                slide.Lines=preserveLineBreaks
                    ? source.Select(line=>line.Replace("\t","    ")).ToList()
                    : source.SelectMany(line=>line.Length==0 ? new List<string>{""} : Wrap(line,slide.FontName,size,slide.Bold)).ToList();
                // Lyrics must fit their authored lines as well as the block height.
                // Prose announcements may still wrap within their single slide.
                return (!preserveLineBreaks || slide.Lines.All(line=>Measure(line,FontPixels(size),slide.FontName,slide.Bold)<=TextWidth-24))
                    && slide.Lines.Count<=TextCapacity(size,slide.FontName,slide.Footer,slide.Bold,slide.FullSlide,false);
            }
            if(Fit(maximumSize)) return;
            // Measure the selected face, weight, wrapping and usable text area.
            // The user's font size is a maximum, independently for each block.
            var low=1; var high=(int)Math.Floor(maximumSize); var best=0;
            while(low<=high) {
                var middle=(low+high)/2;
                if(Fit(middle)) { best=middle; low=middle+1; } else high=middle-1;
            }
            if(best==0) throw new FormatException(error);
            Fit(best);
        }
        public static string Footer(ServiceItem item, UserSettings settings)
        {
            var parts = new List<string> { item.Copyright ?? "" };
            var song=HasSectionTags(item.Lyrics);
            if (song && !string.IsNullOrWhiteSpace(item.SongId)) parts.Add("CCLI Song #" + item.SongId);
            if (song && !string.IsNullOrWhiteSpace(settings.ChurchLicense)) parts.Add("CCLI Licence #" + settings.ChurchLicense);
            return string.Join("  ·  ", parts.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        public static List<string> Wrap(string input, string fontName = Font, double fontSize = 36,bool bold=false)
        {
            var result = new List<string>();
            var line = new StringBuilder();
            // Text elements preserve surrogate pairs and combining characters.
            // Keep source tabs in the editor/store; use four spaces consistently in preview and PPTX.
            var enumerator = StringInfo.GetTextElementEnumerator(input.Replace("\t","    "));
            while (enumerator.MoveNext())
            {
                var ch = enumerator.GetTextElement();
                if (line.Length > 0 && Measure(line.ToString() + ch, FontPixels(fontSize), fontName,bold) > TextWidth - 24)
                {
                    var text = line.ToString(); var space = text.LastIndexOf(' ');
                    if (space > text.Length / 2 && text.Take(space).Any(c=>!char.IsWhiteSpace(c))) { result.Add(text.Substring(0, space+1)); line.Clear(); line.Append(text.Substring(space + 1)); }
                    else { result.Add(text); line.Clear(); }
                }
                line.Append(ch);
            }
            if (line.Length > 0) result.Add(line.ToString());
            return result;
        }
        public static double Measure(string text, double size, string fontName = Font,bool bold=false) => new FormattedText(text, CultureInfo.GetCultureInfo("zh-HK"), FlowDirection.LeftToRight, new Typeface(FontCatalog.Get(fontName).Family, FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal, FontStretch.Normal), size, Brushes.White).WidthIncludingTrailingWhitespace;
        public static double FontPixels(double points) => points*4/3;
        public static double LineSpacing(double points,string fontName,bool bold=false) => Math.Max(FontPixels(points)*LineHeight/FontSize,new FormattedText("國Ag",CultureInfo.GetCultureInfo("zh-HK"),FlowDirection.LeftToRight,new Typeface(FontCatalog.Get(fontName).Family,FontStyle.Normal,bold ? FontWeight.Bold : FontWeight.Normal,FontStretch.Normal),FontPixels(points),Brushes.White).Height);
        private static int TextCapacity(double size,string font,string footer,bool bold,bool fullSlide,bool song=true)
        {
            var slide=new SlideData { FontSize=size,FontName=font,Footer=footer,Bold=bold,FullSlide=fullSlide };
            var fits=(int)Math.Floor((FooterTop(slide)-CreditGap(slide)-TextY)/LineSpacing(size,font,bold));
            return song ? Math.Min(fullSlide ? 8 : LinesPerSlide,fits) : fits;
        }
        private static double CreditGap(SlideData slide) => string.IsNullOrWhiteSpace(slide.Footer) ? 0 : 18;
        public static double TextTop(SlideData slide)
        {
            var top=TextY+(slide.BottomHalf ? 360 : 0);
            var space=Math.Max(0,FooterTop(slide)-CreditGap(slide)-top-slide.Lines.Count*LineSpacing(slide.FontSize,slide.FontName,slide.Bold));
            return top+space*(slide.TextPosition=="Bottom" ? 1 : slide.TextPosition=="Middle" ? .5 : 0);
        }
        public static List<string> FooterLines(SlideData slide) => (slide.Footer ?? "").Replace("\r","").Split('\n').Where(s=>!string.IsNullOrWhiteSpace(s)).SelectMany(s=>Wrap(s.Trim(),slide.FontName,slide.FontSize*.6,slide.Bold)).ToList();
        public static double FooterSize(SlideData slide) => FontPixels(slide.FontSize)*.6;
        public static double FooterLineSpacing(SlideData slide) => LineSpacing(slide.FontSize*.6,slide.FontName,slide.Bold);
        public static double FooterHeight(SlideData slide) => FooterLines(slide).Count*FooterLineSpacing(slide);
        public static double FooterTop(SlideData slide) => (slide.FullSlide ? 652 : slide.BottomHalf ? 704 : 344)-FooterHeight(slide);
    }
}
