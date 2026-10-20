using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
namespace WorshipPptx
{
    public sealed class BundledFont
    {
        public string Name { get; }
        public string DisplayName { get; }
        public string FilePath { get; }
        public FontFamily Family { get; }
        public BundledFont(string name,string label,string filename)
        {
            Name=name; DisplayName=label;
            FilePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fonts",filename);
            Family=new FontFamily(new Uri(Path.GetDirectoryName(FilePath)+Path.DirectorySeparatorChar),"./#"+name);
        }
    }
    public static class FontCatalog
    {
        public const string DefaultName="Noto Sans CJK HK";
        public static readonly IReadOnlyList<BundledFont> All=new[] {
            new BundledFont("Noto Sans CJK HK","香港黑體 · Noto Sans CJK HK","NotoSansCJKhk-Regular.ttf"),
            new BundledFont("Noto Serif CJK HK","香港宋體 · Noto Serif CJK HK","NotoSerifCJKhk-Regular.ttf")
        };
        public static BundledFont Get(string name) => All.FirstOrDefault(f=>f.Name==name) ?? All[0];

        // EOT 2.1 wrapping, as used by PowerPoint's application/x-fontdata parts.
        // The full static TrueType face is preserved: the resulting deck remains editable.
        // Only our bundled OFL fonts enter here; arbitrary installed fonts are never redistributed.
        public static byte[] EmbeddedBytes(string name)
        {
            var font=Get(name); var ttf=File.ReadAllBytes(font.FilePath);
            Func<int,ushort> u16=p=>(ushort)((ttf[p]<<8)|ttf[p+1]);
            Func<int,uint> u32=p=>((uint)ttf[p]<<24)|((uint)ttf[p+1]<<16)|((uint)ttf[p+2]<<8)|ttf[p+3];
            var tables=new Dictionary<string,int>();
            for(var i=0;i<u16(4);i++) { var p=12+i*16; tables[Encoding.ASCII.GetString(ttf,p,4)]=(int)u32(p+8); }
            var os=tables["OS/2"]; var head=tables["head"];
            Func<int,string,string> fontName=(id,fallback)=>
            {
                var table=tables["name"]; var count=u16(table+2); var strings=table+u16(table+4);
                for(var i=0;i<count;i++)
                {
                    var entry=table+6+i*12;
                    if(u16(entry)==3 && u16(entry+4)==0x409 && u16(entry+6)==id)
                        return Encoding.BigEndianUnicode.GetString(ttf,strings+u16(entry+10),u16(entry+8));
                }
                return fallback;
            };
            if(u16(os+8)!=0) throw new InvalidOperationException("Bundled font must permit unrestricted embedding.");
            if(!tables.ContainsKey("glyf") || tables.ContainsKey("fvar")) throw new InvalidOperationException("A static TrueType font is required.");
            using(var stream=new MemoryStream())
            using(var writer=new BinaryWriter(stream,Encoding.Unicode,true))
            {
                writer.Write((uint)0); writer.Write((uint)ttf.Length); writer.Write((uint)0x00020001); writer.Write((uint)0);
                writer.Write(ttf,os+32,10); writer.Write((byte)1); writer.Write((byte)(u16(os+62)&1)); writer.Write((uint)u16(os+4));
                writer.Write(u16(os+8)); writer.Write((ushort)0x504c);
                for(var i=0;i<4;i++) writer.Write(u32(os+42+i*4));
                writer.Write(u16(os)>=1 ? u32(os+78) : 0u); writer.Write(u16(os)>=1 ? u32(os+82) : 0u);
                writer.Write(u32(head+8)); for(var i=0;i<4;i++) writer.Write(0u);
                foreach(var text in new[]{fontName(1,font.Name),fontName(2,"Regular"),fontName(5,"Version 1.000"),fontName(4,font.Name+" Regular")})
                { writer.Write((ushort)0); var bytes=Encoding.Unicode.GetBytes(text); writer.Write((ushort)bytes.Length); writer.Write(bytes); }
                writer.Write((ushort)0); writer.Write((ushort)0); // Padding5 and empty RootString.
                writer.Write(ttf); writer.Flush(); var size=(uint)stream.Length; stream.Position=0; writer.Write(size); writer.Flush(); return stream.ToArray();
            }
        }
    }
}
