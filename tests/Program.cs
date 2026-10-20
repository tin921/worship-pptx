using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
namespace WorshipPptx.Tests
{
    static class Program
    {
        static int passed,failed;
        static string root;
        static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
        static void Test(string name,Action body) { try { body(); Console.WriteLine("PASS "+name); passed++; } catch(Exception ex) { Console.WriteLine("FAIL "+name+": "+ex); failed++; } }
        static void Reject(Action action) { try { action(); } catch(FormatException) { return; } catch(IOException) { return; } throw new Exception("Expected rejection"); }
        [STAThread] static int Main(string[] args)
        {
            root=Path.GetFullPath(args.Length>0 ? args[0] : Path.Combine(Path.GetTempPath(),"WorshipPptx-tests-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            var settings=new UserSettings { ChurchLicense="12345" };
            var demo=Demo.Create();
            Test("Five items in service order, first song at index 1",()=>Check(demo.Items.Count==5 && demo.Items[0].Kind=="scripture" && demo.Items[1].Kind=="song" && demo.Items[3].Kind=="scripture","Wrong service order"));
            Test("Explicit repeat order",()=> { var result=SlideLayout.BuildItem(demo.Items[1],settings); Check(result.Count==5,"Expected five slides"); Check(string.Join(" ",result.Select(s=>s.Section))=="V1 C V2 C C","Wrong repeat order"); Check(result[1].Text==result[4].Text,"Repeated chorus changed"); });
            Test("Unknown section rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n內容",Order="v2" },settings)));
            Test("Duplicate tags rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n甲\n[v1]\n乙",Order="v1" },settings)));
            Test("Missing and empty tagged lyrics rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n",Order="v1" },settings)));
            Test("Lyrics before first tag rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="前言\n[v1]\n甲",Order="v1" },settings)));
            Test("Blank lines and 3-line limit paginate",()=> { var result=SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n甲\n乙\n丙\n丁\n\n戊",Order="v1" },settings); Check(result.Count==3 && result[0].Lines.Count==3 && result[1].Text=="丁" && result[2].Text=="戊","Wrong page breaks"); });
            Test("Traditional and Simplified Chinese retained when wrapping",()=> { foreach(var text in new[]{string.Concat(Enumerable.Repeat("祢的恩典與慈愛永遠長存",10)),string.Concat(Enumerable.Repeat("你的恩典与慈爱永远长存",10))}) { var lines=SlideLayout.Wrap(text); Check(string.Concat(lines)==text,"Text lost"); Check(lines.All(l=>SlideLayout.Measure(l,SlideLayout.FontSize)<=SlideLayout.TextWidth),"Overflow"); } });
            Test("Unicode supplementary characters retained",()=> { var text=string.Concat(Enumerable.Repeat("恩典🌅",40)); Check(string.Concat(SlideLayout.Wrap(text))==text,"Unicode split"); });
            Test("Scripture ignores song order",()=>Check(SlideLayout.BuildItem(demo.Items[3],settings).Count==4,"Wrong scripture pagination"));
            Test("CCLI song and church licence kept separate",()=> { var item=new ServiceItem { Copyright="作者",SongId="6789" }; Check(SlideLayout.Footer(item,settings)=="作者  ·  CCLI Song #6789  ·  CCLI Licence #12345","Copyright footer wrong"); Check(!SlideLayout.Footer(demo.Items[0],settings).Contains("CCLI"),"CCLI on scripture"); });
            var store=new LocalStore(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")));
            Test("History persists Unicode and invalid drafts",()=> { store.Save(demo); demo.Items[1].Order="invalid"; store.Save(demo); var read=store.LoadHistory().Single(); Check(read.Items[1].Order=="invalid" && read.Items[1].Title=="同心頌讚","History did not preserve draft"); demo.Items[1].Order="v1 c v2 c c"; store.Save(demo); });
            Test("Atomic save backup is retained",()=>Check(File.Exists(Path.Combine(store.History,demo.Id+".json.bak")),"Backup missing"));
            Test("Settings round-trip",()=> { store.SaveSettings(settings); Check(store.LoadSettings().ChurchLicense=="12345","Settings lost"); });
            Test("Damaged history recovers backup without deleting original",()=> { var file=Path.Combine(store.History,demo.Id+".json"); File.WriteAllText(file,"broken"); Check(store.LoadHistory().Count==1 && File.ReadAllText(file)=="broken" && store.RecoveryWarning.Length>0,"Recovery failed"); store.Save(demo); });
            var images=new ImageStore(store.Assets); var bytes=SlideVisual.BackgroundPng(""); var source=Path.Combine(root,"input.png"); File.WriteAllBytes(source,bytes); string imported=null;
            Test("Local image import is content-addressed PNG",()=> { imported=images.ImportFile(source); Check(File.Exists(imported) && imported==images.ImportBytes(bytes),"Import not deduplicated"); });
            Test("Stored image survives original deletion",()=> { File.Delete(source); Check(ImageStore.Load(imported).PixelWidth==1280,"Image not copied"); });
            Test("Invalid image rejected",()=>Reject(()=>images.ImportBytes(Encoding.UTF8.GetBytes("not an image"))));
            Test("Oversized image data rejected",()=>Reject(()=>images.ImportBytes(new byte[ImageStore.MaxBytes+1])));
            Test("Unsupported URL scheme rejected",()=>Reject(()=>images.ImportUrlAsync("file:///C:/private.txt").GetAwaiter().GetResult()));
            Test("HTTP image download and validation",()=> { var result=WithHttp(bytes,"image/png",url=>images.ImportUrlAsync(url).GetAwaiter().GetResult()); Check(File.Exists(result),"URL image missing"); });
            Test("HTML page rejected as image",()=>Reject(()=>WithHttp(Encoding.UTF8.GetBytes("<html>web page</html>"),"text/html",url=>images.ImportUrlAsync(url).GetAwaiter().GetResult())));
            Test("File-drop input",()=> { var data=new DataObject(DataFormats.FileDrop,new[]{imported}); Check(BackgroundDialog.ImportData(images,data).Result==imported,"Drop failed"); });
            Test("Bitmap clipboard input",()=> { var data=new DataObject(DataFormats.Bitmap,ImageStore.Load(imported)); Check(File.Exists(BackgroundDialog.ImportData(images,data).Result),"Paste failed"); });
            Test("URL clipboard input",()=> { var path=WithHttp(bytes,"image/png",url=>BackgroundDialog.ImportData(images,new DataObject(DataFormats.UnicodeText,url)).GetAwaiter().GetResult()); Check(File.Exists(path),"URL paste failed"); });
            foreach(var font in FontCatalog.All)
            {
                Test(font.Name+" loads from bundled file and covers Chinese",()=> { var face=new Typeface(font.Family,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal); GlyphTypeface glyph; Check(face.TryGetGlyphTypeface(out glyph),"No physical font"); Check(glyph.FontUri.LocalPath.StartsWith(AppDomain.CurrentDomain.BaseDirectory,StringComparison.OrdinalIgnoreCase),"Font did not load from bundle"); foreach(var ch in "祢的恩典與慈愛永遠長存你的恩典与慈爱永远长存") Check(glyph.CharacterToGlyphMap.ContainsKey(ch),"Missing glyph "+ch); });
                Test(font.Name+" EOT loads in Windows embedded-font engine",()=>ValidateEmbeddedFont(FontCatalog.EmbeddedBytes(font.Name)));
            }
            demo.Items[1].Background=imported;
            var slides=SlideLayout.Build(demo,settings); var output=Path.Combine(root,"sample-worship.pptx");
            Test("PPTX package schema validates",()=> { PptxWriter.Write(output,slides,demo.Title); using(var doc=PresentationDocument.Open(output,false)) { var errors=new OpenXmlValidator().Validate(doc).ToList(); Check(errors.Count==0,string.Join("\n",errors.Take(10).Select(e=>e.Description+" "+e.Path?.XPath))); Check(doc.PresentationPart.SlideParts.Count()==slides.Count,"Wrong slide count"); } });
            Test("All native text boxes confined to upper half",()=> { XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",a="http://schemas.openxmlformats.org/drawingml/2006/main"; using(var zip=ZipFile.OpenRead(output)) foreach(var entry in zip.Entries.Where(x=>x.FullName.StartsWith("ppt/slides/slide") && x.FullName.EndsWith(".xml"))) using(var input=entry.Open()) { var xml=XDocument.Load(input); foreach(var shape in xml.Descendants(p+"sp").Where(x=>x.Element(p+"txBody")!=null)) { var transform=shape.Element(p+"spPr").Element(a+"xfrm"); var bottom=(long)transform.Element(a+"off").Attribute("y")+(long)transform.Element(a+"ext").Attribute("cy"); Check(bottom<=720*9525/2,"Text below half on "+entry.FullName); } } });
            Test("All image references embedded, no external links",()=> { using(var zip=ZipFile.OpenRead(output)) { Check(zip.Entries.Any(x=>x.FullName.StartsWith("ppt/media/")),"No image"); foreach(var entry in zip.Entries.Where(x=>x.FullName.EndsWith(".rels"))) using(var input=new StreamReader(entry.Open())) Check(!input.ReadToEnd().Contains("TargetMode=\"External\""),"External dependency"); } });
            Test("PPTX overwrite leaves valid package",()=> { PptxWriter.Write(output,slides,demo.Title); using(var doc=PresentationDocument.Open(output,false)) Check(!new OpenXmlValidator().Validate(doc).Any(),"Overwrite corrupt"); });
            Test("PPTX embeds selected full font and uses it in native text",()=> { using(var doc=PresentationDocument.Open(output,false)) { var embedded=doc.PresentationPart.Presentation.EmbeddedFontList; Check(embedded.ChildElements.Count==1,"Font list missing"); Check(embedded.InnerXml.Contains(FontCatalog.DefaultName),"Wrong font"); } using(var zip=ZipFile.OpenRead(output)) using(var input=zip.GetEntry("ppt/fonts/font1.fntdata").Open()) using(var memory=new MemoryStream()) { input.CopyTo(memory); Check(memory.ToArray().SequenceEqual(FontCatalog.EmbeddedBytes(FontCatalog.DefaultName)),"Font payload changed"); } });
            Test("Serif selection persists and exports with its own font",()=> { demo.FontName="Noto Serif CJK HK"; store.Save(demo); Check(store.LoadHistory().Single().FontName==demo.FontName,"Font choice lost"); var serifSlides=SlideLayout.Build(demo,settings); Check(serifSlides.All(s=>s.FontName==demo.FontName),"Wrong slide font"); var path=Path.Combine(root,"sample-serif.pptx"); PptxWriter.Write(path,serifSlides,demo.Title); using(var doc=PresentationDocument.Open(path,false)) { Check(!new OpenXmlValidator().Validate(doc).Any(),"Invalid serif deck"); Check(doc.PresentationPart.Presentation.EmbeddedFontList.InnerXml.Contains(demo.FontName),"Serif not embedded"); } });
            Test("Render slide preview at export dimensions",()=> { var visual=SlideVisual.Create(slides[1]); visual.Measure(new Size(1280,720)); visual.Arrange(new Rect(0,0,1280,720)); visual.UpdateLayout(); var bmp=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32); bmp.Render(visual); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp)); using(var file=File.Create(Path.Combine(root,"slide-preview.png"))) encoder.Save(file); });
            Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed. Outputs: "+root); return failed==0 ? 0 : 1;
        }
        static string WithHttp(byte[] bytes,string type,Func<string,string> call)
        {
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
            var server=Task.Run(()=> { using(var client=listener.AcceptTcpClient()) using(var stream=client.GetStream()) { var request=new byte[4096]; stream.Read(request,0,request.Length); var header=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: "+type+"\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n"); stream.Write(header,0,header.Length); stream.Write(bytes,0,bytes.Length); } });
            try { return call("http://127.0.0.1:"+port+"/image"); } finally { listener.Stop(); server.GetAwaiter().GetResult(); }
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint ReadFont(IntPtr state,IntPtr buffer,uint count);
        [DllImport("t2embed.dll",CallingConvention=CallingConvention.Winapi)] static extern int TTLoadEmbeddedFont(out IntPtr handle,uint flags,out uint privilege,uint requested,out uint status,ReadFont read,IntPtr state,IntPtr winName,IntPtr macName,IntPtr info);
        [DllImport("t2embed.dll",CallingConvention=CallingConvention.Winapi)] static extern int TTDeleteEmbeddedFont(IntPtr handle,uint flags,out uint status);
        static void ValidateEmbeddedFont(byte[] bytes)
        {
            var position=0;
            ReadFont read=(state,buffer,count)=> { var length=Math.Min((int)count,bytes.Length-position); Marshal.Copy(bytes,position,buffer,length); position+=length; return (uint)length; };
            IntPtr handle; uint privilege,status;
            // Private, editable, temporary load only; release it immediately after validation.
            var result=TTLoadEmbeddedFont(out handle,1,out privilege,8,out status,read,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
            try { Check(result==0,"Windows rejected EOT, error 0x"+result.ToString("X")); Check(handle!=IntPtr.Zero,"Missing native font handle"); }
            finally { if(handle!=IntPtr.Zero) TTDeleteEmbeddedFont(handle,0,out status); GC.KeepAlive(read); }
        }
    }
}
