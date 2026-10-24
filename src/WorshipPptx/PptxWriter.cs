using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
namespace WorshipPptx
{
    // Editable DrawingML text over embedded images. No installed Office or network service is required.
    public static class PptxWriter
    {
        private static readonly XNamespace P="http://schemas.openxmlformats.org/presentationml/2006/main", A="http://schemas.openxmlformats.org/drawingml/2006/main", R="http://schemas.openxmlformats.org/officeDocument/2006/relationships", Rel="http://schemas.openxmlformats.org/package/2006/relationships", Ct="http://schemas.openxmlformats.org/package/2006/content-types";
        private const string OfficeRel="http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        private static long Emu(double px) => (long)Math.Round(px*9525);
        private static XElement E(XName name, params object[] content) => new XElement(name,content);
        private static XAttribute At(string name,object value) => new XAttribute(name,value);
        private static XElement Relation(string id,string kind,string target) => E(Rel+"Relationship",At("Id",id),At("Type",OfficeRel+kind),At("Target",target));
        private static XElement Relationships(params object[] entries) => E(Rel+"Relationships",entries);
        private static XElement Group() => E(P+"nvGrpSpPr", E(P+"cNvPr",At("id",1),At("name","")),E(P+"cNvGrpSpPr"),E(P+"nvPr"));
        private static XElement GroupProps() => E(P+"grpSpPr", E(A+"xfrm",E(A+"off",At("x",0),At("y",0)),E(A+"ext",At("cx",0),At("cy",0)),E(A+"chOff",At("x",0),At("y",0)),E(A+"chExt",At("cx",0),At("cy",0))));
        private static XElement Transform(double x,double y,double w,double h) => E(A+"xfrm",E(A+"off",At("x",Emu(x)),At("y",Emu(y))),E(A+"ext",At("cx",Emu(w)),At("cy",Emu(h))));
        private static XElement Geometry() => E(A+"prstGeom",At("prst","rect"),E(A+"avLst"));
        private static XElement Solid(string rgb) => E(A+"solidFill",E(A+"srgbClr",At("val",rgb)));
        private static XElement Shape(uint id,string name,double x,double y,double w,double h,params object[] props) => E(P+"sp", E(P+"nvSpPr",E(P+"cNvPr",At("id",id),At("name",name)),E(P+"cNvSpPr"),E(P+"nvPr")),E(P+"spPr",Transform(x,y,w,h),Geometry(),props));
        private static XElement Text(uint id,string text,double y,double size,string color,double height,string fontName,string alignment="Center",bool bold=false,double x=SlideLayout.TextX,double width=SlideLayout.TextWidth)
        {
            var shape = Shape(id,"Text "+id,x,y,width,height,E(A+"noFill"),E(A+"ln",E(A+"noFill")));
            shape.Element(P+"nvSpPr").Element(P+"cNvSpPr").Add(At("txBox",1));
            shape.Add(E(P+"txBody",E(A+"bodyPr",At("wrap","none"),At("lIns",0),At("rIns",0),At("tIns",0),At("bIns",0),At("anchor","t"),E(A+"noAutofit")),E(A+"lstStyle"),E(A+"p",E(A+"pPr",At("algn",alignment=="Left" ? "l" : alignment=="Right" ? "r" : "ctr"),E(A+"lnSpc",E(A+"spcPct",At("val",100000))),E(A+"spcBef",E(A+"spcPts",At("val",0))),E(A+"spcAft",E(A+"spcPts",At("val",0)))),E(A+"r",E(A+"rPr",At("lang",ChineseText.CultureName),At("sz",(int)Math.Round(size*75)),At("b",bold ? 1 : 0),At("dirty",0),Solid(color),E(A+"latin",At("typeface",fontName)),E(A+"ea",At("typeface",fontName)),E(A+"cs",At("typeface",fontName))),E(A+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text ?? "")),E(A+"endParaRPr",At("lang",ChineseText.CultureName)))));
            return shape;
        }
        public static void Write(string path,IList<SlideData> slides,string title)
        {
            if (slides.Count == 0) throw new InvalidOperationException(ChineseText.Ui("沒有可匯出的投影片。"));
            var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using (var zip=ZipFile.Open(temp,ZipArchiveMode.Create))
                {
                    var images=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
                    string ImageKey(SlideData slide) => (slide.Background ?? "")+"|"+(slide.BackgroundTransform ?? new()).Key;
                    foreach(var slide in slides) { var key=ImageKey(slide); if(!images.ContainsKey(key)) { var n=images.Count+1; images.Add(key,n); Bytes(zip,"ppt/media/image"+n+".png",SlideVisual.BackgroundPng(slide.Background,slide.BackgroundTransform)); } }
                    var types=E(Ct+"Types",E(Ct+"Default",At("Extension","rels"),At("ContentType","application/vnd.openxmlformats-package.relationships+xml")),E(Ct+"Default",At("Extension","xml"),At("ContentType","application/xml")),E(Ct+"Default",At("Extension","png"),At("ContentType","image/png")));
                    Action<string,string> type=(part,kind)=>types.Add(E(Ct+"Override",At("PartName","/"+part),At("ContentType",kind)));
                    const string content="application/vnd.openxmlformats-officedocument.presentationml.";
                    type("ppt/presentation.xml",content+"presentation.main+xml"); type("ppt/slideMasters/slideMaster1.xml",content+"slideMaster+xml"); type("ppt/slideLayouts/slideLayout1.xml",content+"slideLayout+xml"); type("ppt/theme/theme1.xml","application/vnd.openxmlformats-officedocument.theme+xml");
                    type("docProps/core.xml","application/vnd.openxmlformats-package.core-properties+xml"); type("docProps/app.xml","application/vnd.openxmlformats-officedocument.extended-properties+xml");
                    var ids=new List<XElement>(); var relations=new List<XElement> { Relation("rIdMaster","slideMaster","slideMasters/slideMaster1.xml") };
                    for(var i=0;i<slides.Count;i++)
                    {
                        var n=i+1; var slide=slides[i]; var image=images[ImageKey(slide)];
                        type("ppt/slides/slide"+n+".xml",content+"slide+xml"); ids.Add(E(P+"sldId",At("id",256+i),new XAttribute(R+"id","rId"+n))); relations.Add(Relation("rId"+n,"slide","slides/slide"+n+".xml"));
                        var tree=E(P+"spTree",Group(),GroupProps(),E(P+"pic",E(P+"nvPicPr",E(P+"cNvPr",At("id",2),At("name","Background")),E(P+"cNvPicPr",E(A+"picLocks",At("noChangeAspect",1))),E(P+"nvPr")),E(P+"blipFill",E(A+"blip",new XAttribute(R+"embed","rIdImage")),E(A+"stretch",E(A+"fillRect"))),E(P+"spPr",Transform(0,0,1280,720),Geometry())));
                        var color=TextColors.Get(slide.TextColor).Hex.TrimStart('#');
                        var spacing=SlideLayout.LineSpacing(slide.FontSize,slide.FontName,slide.Bold);
                        for(var line=0;line<slide.Lines.Count;line++) tree.Add(Text((uint)(4+line),slide.Lines[line],SlideLayout.TextTop(slide)+line*spacing,SlideLayout.FontPixels(slide.FontSize),color,spacing,slide.FontName,slide.Alignment,slide.Bold));
                        var credits=SlideLayout.FooterLines(slide);
                        for(var line=0;line<credits.Count;line++) tree.Add(Text((uint)(20+line),credits[line],SlideLayout.FooterTop(slide)+line*SlideLayout.FooterLineSpacing(slide),SlideLayout.FooterSize(slide),color,SlideLayout.FooterLineSpacing(slide),slide.FontName,slide.Alignment,slide.Bold));
                        if(!string.IsNullOrEmpty(SlideVisual.TagLabel(slide))) {
                            var tagWidth=SlideVisual.TagWidth(slide); var tagX=1280-32-tagWidth;
                            var tag=Text(50000,SlideVisual.TagLabel(slide),656,20,"38483E",32,slide.FontName,"Center",false,tagX,tagWidth);
                            tag.Element(P+"nvSpPr").Element(P+"cNvPr").SetAttributeValue("name","Section tag");
                            tag.Element(P+"spPr").Element(A+"noFill").ReplaceWith(Solid("ECEEEC"));
                            var body=tag.Element(P+"txBody").Element(A+"bodyPr"); body.SetAttributeValue("lIns",Emu(12)); body.SetAttributeValue("rIns",Emu(12)); body.SetAttributeValue("anchor","ctr");
                            tree.Add(tag);
                        }
                        Xml(zip,"ppt/slides/slide"+n+".xml",E(P+"sld",new XAttribute(XNamespace.Xmlns+"a",A),new XAttribute(XNamespace.Xmlns+"r",R),E(P+"cSld",At("name",slide.Title+" — "+slide.Section),tree),E(P+"clrMapOvr",E(A+"masterClrMapping"))));
                        var slideRelations=Relationships(Relation("rIdLayout","slideLayout","../slideLayouts/slideLayout1.xml"),Relation("rIdImage","image","../media/image"+image+".png"));
                        Xml(zip,"ppt/slides/_rels/slide"+n+".xml.rels",slideRelations);
                    }
                    var embeddedFonts=E(P+"embeddedFontLst");
                    var fontIndex=0;
                    foreach(var family in slides.Select(s=>FontCatalog.Get(s.FontName)).Where(f=>f.IsBundled).Select(f=>f.Name).Distinct())
                    {
                        var id="rIdFont"+(++fontIndex);
                        Bytes(zip,"ppt/fonts/font"+fontIndex+".fntdata",FontCatalog.EmbeddedBytes(family));
                        relations.Add(Relation(id,"font","fonts/font"+fontIndex+".fntdata"));
                        embeddedFonts.Add(E(P+"embeddedFont",E(P+"font",At("typeface",family)),E(P+"regular",new XAttribute(R+"id",id))));
                    }
                    if(fontIndex>0) types.Add(E(Ct+"Default",At("Extension","fntdata"),At("ContentType","application/x-fontdata")));
                    Xml(zip,"[Content_Types].xml",types);
                    XNamespace cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties", dc="http://purl.org/dc/elements/1.1/";
                    var fontAttribution=slides.Any(s=>s.FontName=="TW-MOE-Li")
                        ? E(dc+"description","教育部隸書 TW-MOE-Li 3.00 — 中華民國教育部. Original font embedded unchanged under CC BY-ND 3.0 Taiwan: https://creativecommons.org/licenses/by-nd/3.0/tw/ . Source: https://language.moe.gov.tw/material/info?m=9fe3fb11-c3d5-41f2-b029-6d18a2c2fd0d") : null;
                    Xml(zip,"docProps/core.xml",E(cp+"coreProperties",new XAttribute(XNamespace.Xmlns+"dc",dc),E(dc+"title",title),E(dc+"creator","Worship PPTX"),fontAttribution));
                    XNamespace ep="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
                    Xml(zip,"docProps/app.xml",E(ep+"Properties",E(ep+"Application","Worship PPTX"),E(ep+"PresentationFormat","On-screen Show (16:9)"),E(ep+"Slides",slides.Count)));
                    Xml(zip,"_rels/.rels",Relationships(Relation("rId1","officeDocument","ppt/presentation.xml"),E(Rel+"Relationship",At("Id","rId2"),At("Type","http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"),At("Target","docProps/core.xml")),Relation("rId3","extended-properties","docProps/app.xml")));
                    Xml(zip,"ppt/presentation.xml",E(P+"presentation",At("embedTrueTypeFonts",fontIndex>0 ? 1 : 0),At("saveSubsetFonts",0),new XAttribute(XNamespace.Xmlns+"a",A),new XAttribute(XNamespace.Xmlns+"r",R),E(P+"sldMasterIdLst",E(P+"sldMasterId",At("id",2147483648),new XAttribute(R+"id","rIdMaster"))),E(P+"sldIdLst",ids),E(P+"sldSz",At("cx",Emu(1280)),At("cy",Emu(720)),At("type","screen16x9")),E(P+"notesSz",At("cx",6858000),At("cy",9144000)),fontIndex>0 ? embeddedFonts : null,E(P+"defaultTextStyle")));
                    Xml(zip,"ppt/_rels/presentation.xml.rels",Relationships(relations));
                    var colorMap=E(P+"clrMap",At("bg1","lt1"),At("tx1","dk1"),At("bg2","lt2"),At("tx2","dk2"),At("accent1","accent1"),At("accent2","accent2"),At("accent3","accent3"),At("accent4","accent4"),At("accent5","accent5"),At("accent6","accent6"),At("hlink","hlink"),At("folHlink","folHlink"));
                    Xml(zip,"ppt/slideMasters/slideMaster1.xml",E(P+"sldMaster",new XAttribute(XNamespace.Xmlns+"a",A),new XAttribute(XNamespace.Xmlns+"r",R),E(P+"cSld",E(P+"spTree",Group(),GroupProps())),colorMap,E(P+"sldLayoutIdLst",E(P+"sldLayoutId",At("id",2147483649),new XAttribute(R+"id","rIdLayout"))),E(P+"txStyles",E(P+"titleStyle"),E(P+"bodyStyle"),E(P+"otherStyle"))));
                    Xml(zip,"ppt/slideMasters/_rels/slideMaster1.xml.rels",Relationships(Relation("rIdLayout","slideLayout","../slideLayouts/slideLayout1.xml"),Relation("rIdTheme","theme","../theme/theme1.xml")));
                    Xml(zip,"ppt/slideLayouts/slideLayout1.xml",E(P+"sldLayout",new XAttribute(XNamespace.Xmlns+"a",A),At("type","blank"),At("preserve",1),E(P+"cSld",At("name","Blank"),E(P+"spTree",Group(),GroupProps())),E(P+"clrMapOvr",E(A+"masterClrMapping"))));
                    Xml(zip,"ppt/slideLayouts/_rels/slideLayout1.xml.rels",Relationships(Relation("rIdMaster","slideMaster","../slideMasters/slideMaster1.xml")));
                    Xml(zip,"ppt/theme/theme1.xml",Theme(slides[0].FontName));
                }
                if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
            }
            finally { if(File.Exists(temp)) File.Delete(temp); }
        }
        private static XElement Theme(string fontName)
        {
            var colors=E(A+"clrScheme",At("name","Worship"));
            var names=new[]{"dk1","lt1","dk2","lt2","accent1","accent2","accent3","accent4","accent5","accent6","hlink","folHlink"};
            var values=new[]{"000000","FFFFFF","263A32","F6F7F2","426B54","7C9D83","D7C7A2","587E84","997C69","B4C7A2","426B54","738077"};
            for(var i=0;i<names.Length;i++) colors.Add(E(A+names[i],E(A+"srgbClr",At("val",values[i]))));
            Func<string,XElement> font=name=>E(A+name,E(A+"latin",At("typeface",fontName)),E(A+"ea",At("typeface",fontName)),E(A+"cs",At("typeface",fontName)));
            return E(A+"theme",At("name","Worship"),E(A+"themeElements",colors,E(A+"fontScheme",At("name","Worship"),font("majorFont"),font("minorFont")),E(A+"fmtScheme",At("name","Worship"),E(A+"fillStyleLst",Solid("FFFFFF"),Solid("FFFFFF"),Solid("FFFFFF")),E(A+"lnStyleLst",Enumerable.Range(1,3).Select(i=>E(A+"ln",At("w",9525*i),Solid("FFFFFF"),E(A+"prstDash",At("val","solid"))))),E(A+"effectStyleLst",Enumerable.Range(1,3).Select(i=>E(A+"effectStyle",E(A+"effectLst")))),E(A+"bgFillStyleLst",Solid("FFFFFF"),Solid("FFFFFF"),Solid("FFFFFF")))));
        }
        private static void Bytes(ZipArchive zip,string path,byte[] bytes) { using(var s=zip.CreateEntry(path,CompressionLevel.Optimal).Open()) s.Write(bytes,0,bytes.Length); }
        private static void Xml(ZipArchive zip,string path,XElement root) { using(var s=zip.CreateEntry(path,CompressionLevel.Optimal).Open()) { var doc=new XDocument(new XDeclaration("1.0","utf-8","yes"),root); doc.Save(s); } }
    }
}
