using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Avalonia.Media;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace WorshipPptx.Tests;

static class FontTests
{
    internal static bool SimplifiedOnly(FontChoice font) => font.Name is "Ma Shan Zheng" or "ZCOOL KuaiLe";
    internal static string Sample(FontChoice font) => SimplifiedOnly(font) ? "祢的恩典与慈爱永远长存" :
        font.Name is "TW-MOE-Li" or "JasonHandwriting4" ? "你的恩典與慈愛永遠長存" : "祢的恩典與慈愛永遠長存";
    public static void Run(Action<string,Action> test,Action<bool,string> check,Action<byte[]> validateEmbedded,string root)
    {
        using var coverage=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"font-coverage.json")));
        var samples=coverage.RootElement.GetProperty("fonts").EnumerateArray().ToDictionary(f=>f.GetProperty("family").GetString());
        test("All researched font additions are available in the picker",()=> {
            foreach(var name in samples.Keys) check(FontCatalog.Bundled.Any(f=>f.Name==name),"Missing bundled font: "+name);
        });
        test("Near-duplicate system fonts are hidden and old selections resolve to their base families",()=> {
            foreach(var pair in new[]{("Microsoft JhengHei UI","Microsoft JhengHei"),("Microsoft YaHei UI","Microsoft YaHei"),("NSimSun","SimSun")}) {
                check(FontCatalog.All.All(f=>f.Name!=pair.Item1),"Duplicate is still in the picker: "+pair.Item1);
                check(ReferenceEquals(FontCatalog.Get(pair.Item1),FontCatalog.Get(pair.Item2)),"Saved duplicate selection silently changed to an unrelated font");
            }
        });
        foreach(var font in FontCatalog.Bundled) {
            test(font.Name+" loads its actual bundled face and covers its advertised Chinese script",()=> {
                var face=new Typeface(font.Family,FontStyle.Normal,FontWeight.Normal,FontStretch.Normal);
                check(FontManager.Current.TryGetGlyphTypeface(face,out var glyph),"No physical font");
                check(glyph.FamilyName==font.Name && font.Family.Key!=null,"Font did not load from bundle");
                var sample=Sample(font)+(SimplifiedOnly(font) ? "赞美归于主荣耀圣名" : "讚美歸於主榮耀聖名");
                // The new Traditional faces do not claim full Simplified coverage.
                if(!SimplifiedOnly(font) && font.Name!="jf-openhuninn-2.1" && !samples.ContainsKey(font.Name)) sample+="你的恩典与慈爱永远长存";
                foreach(var ch in sample) check(glyph.CharacterToGlyphMap.TryGetGlyph(ch,out _),"Missing glyph "+ch);
                if(SimplifiedOnly(font)) check(font.DisplayName.Contains(ChineseText.Ui("簡體")),"Limited coverage is not marked in the picker");
                if(samples.TryGetValue(font.Name,out var fixture)) {
                    var missing=fixture.GetProperty("missing").GetString();
                    foreach(var ch in coverage.RootElement.GetProperty("corpus").GetString().Where(c=>c>0x3000))
                        check(glyph.CharacterToGlyphMap.TryGetGlyph(ch,out _)==!missing.Contains(ch),"Unexpected coverage for "+ch);
                    if(missing.Length>0 || font.Name=="QIJIC")
                        check(font.DisplayName.Contains(ChineseText.Ui("部分")),"Limited coverage is not marked in the picker");
                }
            });
            if(OperatingSystem.IsWindows()) test(font.Name+" EOT loads in Windows embedded-font engine",()=>validateEmbedded(FontCatalog.EmbeddedBytes(font.Name)));
        }
        test("Bundled original font files retain upstream bytes and editable permissions",()=> {
            using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"Fonts","manifest.json")));
            foreach(var record in manifest.RootElement.EnumerateArray()) {
                var path=Path.Combine(AppContext.BaseDirectory,"Fonts",record.GetProperty("file").GetString());
                var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
                check(hash==record.GetProperty("sha256").GetString(),"Font checksum differs from manifest");
                if(record.TryGetProperty("source_sha256",out var original) && !record.TryGetProperty("convert_cff",out _))
                    check(hash==original.GetString(),"Original TrueType font was modified");
            }
        });
        test("Distinctive Chinese fonts export as editable embedded text",()=> {
            var fonts=FontCatalog.Bundled.Skip(2).ToArray();
            var pages=new System.Collections.Generic.List<SlideData>();
            var language=ChineseText.Current.Language;
            try {
                foreach(var font in fonts) {
                    ChineseText.SetLanguage(SimplifiedOnly(font) ? ChineseText.Simplified : ChineseText.Traditional);
                    pages.AddRange(SlideLayout.BuildItem(new ServiceItem { Title="字體預覽",Lyrics="[v1]\n"+Sample(font),Order="v1" },new UserSettings(),font.Name));
                }
            } finally {ChineseText.SetLanguage(language);}
            var path=Path.Combine(root,"additional-chinese-fonts.pptx"); PptxWriter.Write(path,pages,"中文字體");
            using var document=PresentationDocument.Open(path,false);
            check(!new OpenXmlValidator().Validate(document).Any(),"Invalid deck with additional fonts");
            var embedded=document.PresentationPart.Presentation.EmbeddedFontList;
            check(embedded.ChildElements.Count==fonts.Length && fonts.All(f=>embedded.InnerXml.Contains(f.Name)),"Some selected fonts were not embedded");
            using var zip=ZipFile.OpenRead(path);
            using(var core=new StreamReader(zip.GetEntry("docProps/core.xml").Open())) {
                var properties=core.ReadToEnd();
                check(properties.Contains("中華民國教育部") && properties.Contains("https://creativecommons.org/licenses/by-nd/3.0/tw/"),"Ministry font attribution is missing");
            }
            for(var i=0;i<fonts.Length;i++) {
                using var input=zip.GetEntry("ppt/fonts/font"+(i+1)+".fntdata").Open(); using var bytes=new MemoryStream(); input.CopyTo(bytes);
                check(bytes.ToArray().SequenceEqual(FontCatalog.EmbeddedBytes(fonts[i].Name)),"Embedded font differs from selected font");
            }
        });
    }
}
