using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Avalonia.Media;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace WorshipPptx.Tests;

static class FontTests
{
    internal static bool SimplifiedOnly(FontChoice font) => font.Name is "Ma Shan Zheng" or "ZCOOL KuaiLe";
    internal static string Sample(FontChoice font) => SimplifiedOnly(font) ? "祢的恩典与慈爱永远长存" : "祢的恩典與慈愛永遠長存";
    public static void Run(Action<string,Action> test,Action<bool,string> check,Action<byte[]> validateEmbedded,string root)
    {
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
                if(!SimplifiedOnly(font) && font.Name!="jf-openhuninn-2.1") sample+="你的恩典与慈爱永远长存";
                foreach(var ch in sample) check(glyph.CharacterToGlyphMap.TryGetGlyph(ch,out _),"Missing glyph "+ch);
                if(SimplifiedOnly(font)) check(font.DisplayName.Contains(ChineseText.Ui("簡體")),"Limited coverage is not marked in the picker");
            });
            if(OperatingSystem.IsWindows()) test(font.Name+" EOT loads in Windows embedded-font engine",()=>validateEmbedded(FontCatalog.EmbeddedBytes(font.Name)));
        }
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
            for(var i=0;i<fonts.Length;i++) {
                using var input=zip.GetEntry("ppt/fonts/font"+(i+1)+".fntdata").Open(); using var bytes=new MemoryStream(); input.CopyTo(bytes);
                check(bytes.ToArray().SequenceEqual(FontCatalog.EmbeddedBytes(fonts[i].Name)),"Embedded font differs from selected font");
            }
        });
    }
}
