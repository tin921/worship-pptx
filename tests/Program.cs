using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
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
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false }).SetupWithoutStarting();
            root=Path.GetFullPath(args.Length>0 ? args[0] : Path.Combine(Path.GetTempPath(),"WorshipPptx-tests-"+Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root);
            if(args.Contains("--ui-review")) {UiReview.Run(root); return 0;}
            if(args.Contains("--actions-only")) {ActionButtonTests.Run(Test,Check,root); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed.");return failed==0?0:1;}
            if(args.Contains("--fonts-only")) {FontTests.Run(Test,Check,ValidateEmbeddedFont,root); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed.");return failed==0?0:1;}
            if(args.Contains("--drive-only")) {DriveUploadTests.Run(Test,Check,root); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed.");return failed==0?0:1;}
            if(args.Contains("--editor-only")) { EditorInteractionTests.Run(Test,Check,root); WindowLayoutTests.Run(Test,Check,root); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed."); return failed==0 ? 0 : 1; }
            if(args.Contains("--gallery-only")) { GalleryTests.Run(Test,Check,new ImageStore(Path.Combine(root,"gallery-images"))); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed."); return failed==0 ? 0 : 1; }
            if(args.Contains("--language-only")) { LanguageTests.Run(Test,Check,root); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed."); return failed==0 ? 0 : 1; }
            if(args.Contains("--composition-only")) { CompositionTests.Run(Test,Check,root); Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed."); return failed==0 ? 0 : 1; }
            var settings=new UserSettings { ChurchLicense="12345" };
            ActionButtonTests.Run(Test,Check,root);
            var demo=Demo.Create();
            Test("New services use the coming Sunday across week and year boundaries",()=> {
                Check(Demo.ComingSunday(new DateTime(2026,10,21))==new DateTime(2026,10,25),"Midweek date is not Sunday");
                Check(Demo.ComingSunday(new DateTime(2026,10,25))==new DateTime(2026,10,25),"Sunday should stay today");
                Check(Demo.ComingSunday(new DateTime(2026,12,31))==new DateTime(2027,1,3),"Year boundary failed");
                Check(demo.Date==Demo.ComingSunday(DateTime.Today) && new ServicePlan().Date==demo.Date,"New service default differs");
            });
            Test("Five items in service order, first song at index 1",()=>Check(demo.Items.Count==5 && demo.Items[0].Kind=="scripture" && demo.Items[1].Kind=="song" && demo.Items[3].Kind=="scripture","Wrong service order"));
            demo.Items[1].SongId="6789";
            Test("Every song starts with its title, credits and separate CCLI numbers",()=> {
                var all=SlideLayout.Build(demo,settings);
                Check(all.Count(s=>s.IsTitle)==3 && !all[0].IsTitle,"Wrong song/scripture title pages");
                foreach(var song in demo.Items.Where(i=>i.Kind=="song")) {
                    var result=SlideLayout.BuildItem(song,settings); var title=result[0];
                    Check(title.IsTitle && title.Text==song.Title && !result[1].IsTitle,"Title must precede the lyrics");
                    Check(title.Footer.Contains(song.Copyright) && title.Footer.Contains("CCLI Licence #12345"),"Missing title credits");
                    Check(result.Skip(1).All(s=>s.Footer==""),"Credits repeated on a lyric slide");
                    if(song.SongId.Length>0) Check(title.Footer.Contains("CCLI Song #"+song.SongId),"Missing song number");
                    Check(title.Background==song.Background,"Title lost background settings");
                }
                var longTitle=new ServiceItem { Title=string.Concat(Enumerable.Repeat("恩典慈愛",10)),Lyrics="[v1]\n內容",Order="v1" };
                var fitted=SlideLayout.BuildItem(longTitle,settings,FontCatalog.DefaultName,72)[0];
                Check(string.Concat(fitted.Lines)==longTitle.Title && fitted.FontSize<72,"Long title was clipped");
                Check(SlideLayout.TextTop(fitted)+fitted.Lines.Count*SlideLayout.LineSpacing(fitted.FontSize,fitted.FontName)<SlideLayout.FooterTop(fitted),"Title overlaps credits");
            });
            Test("Explicit repeat order",()=> { var result=SlideLayout.BuildItem(demo.Items[1],settings).Where(s=>!s.IsTitle).ToList(); Check(result.Count==5,"Expected five lyric slides"); Check(string.Join(" ",result.Select(s=>s.Section))=="V1 C V2 C C","Wrong repeat order"); Check(result[1].Text==result[4].Text,"Repeated chorus changed"); });
            Test("Unknown section rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n內容",Order="v2" },settings)));
            Test("Duplicate tags rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n甲\n[v1]\n乙",Order="v1" },settings)));
            Test("Missing and empty tagged lyrics rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n",Order="v1" },settings)));
            Test("Lyrics before first tag rejected",()=>Reject(()=>SlideLayout.BuildItem(new ServiceItem { Lyrics="前言\n[v1]\n甲",Order="v1" },settings)));
            Test("A complete lyric block fits one slide and preserves internal blank lines",()=> { var result=SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n甲\n乙\n丙\n丁\n\n戊\n\n",Order="v1" },settings).Where(s=>!s.IsTitle).ToList(); Check(result.Count==1 && result[0].Text=="甲\n乙\n丙\n丁\n\n戊" && result[0].FontSize<36,"Lyric block was split or spacing lost"); });
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
            Test("Stored image survives original deletion",()=> { File.Delete(source); Check(ImageStore.Load(imported).PixelSize.Width==1280,"Image not copied"); });
            Test("Invalid image rejected",()=>Reject(()=>images.ImportBytes(Encoding.UTF8.GetBytes("not an image"))));
            Test("Oversized image data rejected",()=>Reject(()=>images.ImportBytes(new byte[ImageStore.MaxBytes+1])));
            Test("Unsupported URL scheme rejected",()=>Reject(()=>images.ImportUrlAsync("file:///C:/private.txt").GetAwaiter().GetResult()));
            Test("HTTP image download and validation",()=> { var result=WithHttp(bytes,"image/png",url=>images.ImportUrlAsync(url).GetAwaiter().GetResult()); Check(File.Exists(result),"URL image missing"); });
            Test("HTML page rejected as image",()=>Reject(()=>WithHttp(Encoding.UTF8.GetBytes("<html>web page</html>"),"text/html",url=>images.ImportUrlAsync(url).GetAwaiter().GetResult())));
            Test("File-drop input",()=> { var data=Transfer(DataTransferItem.CreateFile(new Window().StorageProvider.TryGetFileFromPathAsync(imported).GetAwaiter().GetResult())); Check(BackgroundDialog.ImportData(images,data).Result==imported,"Drop failed"); });
            Test("Bitmap clipboard input",()=> { var data=Transfer(DataTransferItem.Create(DataFormat.Bitmap,ImageStore.Load(imported))); Check(File.Exists(BackgroundDialog.ImportData(images,data).Result),"Paste failed"); });
            Test("URL clipboard input",()=> { var path=WithHttp(bytes,"image/png",url=>BackgroundDialog.ImportData(images,Transfer(DataTransferItem.CreateText(url))).GetAwaiter().GetResult()); Check(File.Exists(path),"URL paste failed"); });
            FontTests.Run(Test,Check,ValidateEmbeddedFont,root);
            demo.Items[1].Background=imported;
            var slides=SlideLayout.Build(demo,settings); var output=Path.Combine(root,"sample-worship.pptx");
            Test("PPTX package schema validates",()=> { PptxWriter.Write(output,slides,demo.Title); using(var doc=PresentationDocument.Open(output,false)) { var errors=new OpenXmlValidator().Validate(doc).ToList(); Check(errors.Count==0,string.Join("\n",errors.Take(10).Select(e=>e.Description+" "+e.Path?.XPath))); Check(doc.PresentationPart.SlideParts.Count()==slides.Count,"Wrong slide count"); } });
            Test("All native text boxes confined to upper half",()=> { XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",a="http://schemas.openxmlformats.org/drawingml/2006/main"; using(var zip=ZipFile.OpenRead(output)) foreach(var entry in zip.Entries.Where(x=>x.FullName.StartsWith("ppt/slides/slide") && x.FullName.EndsWith(".xml"))) using(var input=entry.Open()) { var xml=XDocument.Load(input); foreach(var shape in xml.Descendants(p+"sp").Where(x=>x.Element(p+"txBody")!=null && (string)x.Element(p+"nvSpPr").Element(p+"cNvPr").Attribute("name")!="Section tag")) { var transform=shape.Element(p+"spPr").Element(a+"xfrm"); var bottom=(long)transform.Element(a+"off").Attribute("y")+(long)transform.Element(a+"ext").Attribute("cy"); Check(bottom<=720*9525/2,"Text below half on "+entry.FullName); } } });
            Test("All image references embedded, no external links",()=> { using(var zip=ZipFile.OpenRead(output)) { Check(zip.Entries.Any(x=>x.FullName.StartsWith("ppt/media/")),"No image"); foreach(var entry in zip.Entries.Where(x=>x.FullName.EndsWith(".rels"))) using(var input=new StreamReader(entry.Open())) Check(!input.ReadToEnd().Contains("TargetMode=\"External\""),"External dependency"); } });
            Test("PPTX overwrite leaves valid package",()=> { PptxWriter.Write(output,slides,demo.Title); using(var doc=PresentationDocument.Open(output,false)) Check(!new OpenXmlValidator().Validate(doc).Any(),"Overwrite corrupt"); });
            Test("PPTX embeds selected full font and uses it in native text",()=> { using(var doc=PresentationDocument.Open(output,false)) { var embedded=doc.PresentationPart.Presentation.EmbeddedFontList; Check(embedded.ChildElements.Count==1,"Font list missing"); Check(embedded.InnerXml.Contains(FontCatalog.DefaultName),"Wrong font"); } using(var zip=ZipFile.OpenRead(output)) using(var input=zip.GetEntry("ppt/fonts/font1.fntdata").Open()) using(var memory=new MemoryStream()) { input.CopyTo(memory); Check(memory.ToArray().SequenceEqual(FontCatalog.EmbeddedBytes(FontCatalog.DefaultName)),"Font payload changed"); } });
            Test("Serif selection persists and exports with its own font",()=> { foreach(var item in demo.Items) item.Formatting.FontName="Noto Serif CJK HK"; store.Save(demo); Check(store.LoadHistory().Single().Items.All(i=>i.Formatting.FontName=="Noto Serif CJK HK"),"Font choice lost"); var serifSlides=SlideLayout.Build(demo,settings); Check(serifSlides.All(s=>s.FontName=="Noto Serif CJK HK"),"Wrong slide font"); var path=Path.Combine(root,"sample-serif.pptx"); PptxWriter.Write(path,serifSlides,demo.Title); using(var doc=PresentationDocument.Open(path,false)) { Check(!new OpenXmlValidator().Validate(doc).Any(),"Invalid serif deck"); Check(doc.PresentationPart.Presentation.EmbeddedFontList.InnerXml.Contains("Noto Serif CJK HK"),"Serif not embedded"); } });
            Test("Older saved services default to 36 pt and centered text",()=> {
                var path=Path.Combine(root,"legacy-service.json"); File.WriteAllText(path,"{\"Title\":\"舊崇拜\",\"Items\":[]}",new UTF8Encoding(false));
                var old=LocalStore.Read<ServicePlan>(path); Check(old.FontSize==36 && old.Alignment=="Center" && old.FontName==FontCatalog.DefaultName && old.TextColor=="#FFFFFF","Legacy defaults changed");
            });
            Test("Reordering preserves items and persists export order with typography",()=> {
                var plan=Demo.Create(); var moved=plan.Items[1]; var original=plan.Items.ToArray();
                Check(plan.MoveItem(moved,plan.Items.Count) && plan.Items.Last()==moved,"Move to end failed");
                Check(plan.MoveItem(moved,0) && plan.Items.First()==moved,"Move to start failed");
                Check(!plan.MoveItem(moved,1),"Adjacent drop should not change order");
                Check(plan.Items.Count==original.Length && original.All(plan.Items.Contains),"Item lost or duplicated");
                foreach(var item in plan.Items) { item.Formatting.AutoFontSize=false; item.Formatting.FontSize=48; item.Formatting.AutoTitleFontSize=false; item.Formatting.TitleFontSize=48; item.Formatting.Alignment="Right"; }
                var path=Path.Combine(root,"reordered-service.json"); LocalStore.Write(path,plan); var read=LocalStore.Read<ServicePlan>(path);
                Check(read.Items.Select(i=>i.Title).SequenceEqual(plan.Items.Select(i=>i.Title)),"Order did not persist");
                var exported=SlideLayout.Build(read,settings);
                Check(exported.Select(s=>s.Title).Distinct().SequenceEqual(read.Items.Select(i=>i.Title)),"Export order differs from tabs");
                Check(exported.All(s=>s.FontSize==48 && s.Alignment=="Right"),"Typography did not persist");
            });
            Test("Large Chinese lyric blocks shrink to one slide within the text area",()=> {
                var text=string.Concat(Enumerable.Repeat("祢的恩典與慈愛永遠長存",12));
                foreach(var font in FontCatalog.All) foreach(var size in new[]{18.0,36,72}) {
                    var result=SlideLayout.BuildItem(new ServiceItem { Lyrics="[v1]\n"+text,Order="v1" },settings,font.Name,size,"Left").Where(s=>!s.IsTitle).ToList();
                    Check(result.Count==1 && result[0].FontSize<=size,"Lyric block split or exceeds selected maximum");
                    Check(string.Concat(result.SelectMany(s=>s.Lines))==text,"Text lost when resizing: "+font.Name);
                    foreach(var slide in result) {
                    Check(slide.Lines.All(l=>SlideLayout.Measure(l,SlideLayout.FontPixels(slide.FontSize),font.Name)<=SlideLayout.TextWidth),"Horizontal overflow: "+font.Name);
                        Check(SlideLayout.TextY+slide.Lines.Count*SlideLayout.LineSpacing(slide.FontSize,font.Name)<=SlideLayout.FooterTop(slide),"Lyrics overlap footer: "+font.Name);
                    }
                }
            });
            Test("Invalid font sizes rejected",()=> { foreach(var size in new[]{0.0,17,73,double.NaN,double.PositiveInfinity}) Reject(()=>SlideLayout.BuildItem(demo.Items[1],settings,FontCatalog.DefaultName,size)); });
            Test("Preview and PPTX agree on font size, alignment and top-half bounds",()=> {
                var formatted=new System.Collections.Generic.List<SlideData>();
                foreach(var alignment in new[]{"Left","Center","Right"}) foreach(var size in new[]{18.0,48,72}) formatted.AddRange(SlideLayout.BuildItem(demo.Items[1],settings,FontCatalog.DefaultName,size,alignment));
                var path=Path.Combine(root,"sample-formatting.pptx"); PptxWriter.Write(path,formatted,"文字格式");
                using(var doc=PresentationDocument.Open(path,false)) Check(!new OpenXmlValidator().Validate(doc).Any(),"Invalid formatted deck");
                XNamespace p="http://schemas.openxmlformats.org/presentationml/2006/main",a="http://schemas.openxmlformats.org/drawingml/2006/main";
                using(var zip=ZipFile.OpenRead(path)) for(var i=0;i<formatted.Count;i++) {
                    var slide=formatted[i]; var preview=SlideVisual.Create(slide);
                    var label=preview.Children.OfType<Avalonia.Controls.TextBlock>().First();
                    Check(label.FontSize==SlideLayout.FontPixels(slide.FontSize) && label.TextAlignment.ToString()==slide.Alignment,"Preview format mismatch");
                    using(var input=zip.GetEntry("ppt/slides/slide"+(i+1)+".xml").Open()) foreach(var shape in XDocument.Load(input).Descendants(p+"sp").Where(s=>s.Element(p+"txBody")!=null && (string)s.Element(p+"nvSpPr").Element(p+"cNvPr").Attribute("name")!="Section tag")) {
                        var transform=shape.Element(p+"spPr").Element(a+"xfrm");
                        Check((long)transform.Element(a+"off").Attribute("y")+(long)transform.Element(a+"ext").Attribute("cy")<=360*9525,"Export below upper half");
                        if((int)shape.Element(p+"nvSpPr").Element(p+"cNvPr").Attribute("id")>=20) {
                            Check((int)shape.Descendants(a+"rPr").First().Attribute("sz")==Math.Round(slide.FontSize*60),"Credits are not 60 percent of main text"); continue;
                        }
                        Check((int)shape.Descendants(a+"rPr").First().Attribute("sz")==slide.FontSize*100,"Wrong point size");
                        Check((string)shape.Descendants(a+"pPr").First().Attribute("algn")== (slide.Alignment=="Left" ? "l" : slide.Alignment=="Right" ? "r" : "ctr"),"Wrong paragraph alignment");
                    }
                }
            });
            Test("Installed Chinese fonts are detected without silent font substitution",()=> {
                foreach(var font in FontCatalog.All.Where(f=>!f.IsBundled)) { GlyphTypeface glyph; Check(FontManager.Current.TryGetGlyphTypeface(new Typeface(font.Family,FontStyle.Normal,FontWeight.Normal,FontStretch.Normal),out glyph),"Unavailable font listed: "+font.Name); Check(FontManager.Current.SystemFonts.Any(f=>f.Name==font.Name),"Uninstalled font listed"); }
                Console.WriteLine("  Installed Chinese fonts: "+string.Join(", ",FontCatalog.All.Where(f=>!f.IsBundled).Select(f=>f.Name)));
            });
            var systemFont=FontCatalog.All.FirstOrDefault(f=>!f.IsBundled);
            if(systemFont!=null) Test("System-font deck references the selected font without bundling system files",()=> {
                var result=SlideLayout.BuildItem(demo.Items[1],settings,systemFont.Name,40,"Right"); var path=Path.Combine(root,"sample-windows-font.pptx"); PptxWriter.Write(path,result,"System font");
                using(var doc=PresentationDocument.Open(path,false)) { Check(!new OpenXmlValidator().Validate(doc).Any(),"Invalid System-font deck"); Check(doc.PresentationPart.Presentation.EmbeddedFontList==null,"System font unexpectedly embedded"); }
                using(var zip=ZipFile.OpenRead(path)) { Check(!zip.Entries.Any(e=>e.FullName.StartsWith("ppt/fonts/")),"System font copied into package"); using(var input=zip.GetEntry("ppt/slides/slide1.xml").Open()) { XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main"; Check(XDocument.Load(input).Descendants(a+"ea").All(n=>(string)n.Attribute("typeface")==systemFont.Name),"Selected System font lost"); } }
            });
            Test("Preview layout retains 720p dimensions without taking screenshots",()=> {
                var visual=SlideVisual.Create(slides[1]); visual.Measure(new Size(1280,720)); visual.Arrange(new Rect(0,0,1280,720));
                Check(visual.Bounds.Width==1280 && visual.Bounds.Height==720,"Wrong preview size");
                foreach(var label in visual.Children.OfType<TextBlock>()) Check(Canvas.GetTop(label)+label.DesiredSize.Height<=360,"Preview text below upper half");
            });
            Test("Avalonia editor loads saved plans, first song and resizable columns",()=> {
                var window=new MainWindow(store); window.Show(); Dispatcher.UIThread.RunJobs();
                try {
                    var tabs=window.FindControl<TabControl>("SongTabs");
                    Check(tabs.Items.Count==6 && tabs.SelectedIndex==1,"Default active tab changed");
                    Check(tabs.Items.Cast<TabItem>().Last().Classes.Contains("add-tab"),"Add tab is not last");
                    Check(window.FindControl<TextBox>("Lyrics").Text==demo.Items[1].Lyrics,"Saved lyrics changed");
                    var grid=window.FindControl<Grid>("WorkArea");
                    Check(grid.ColumnDefinitions.Count==5 && grid.Children.OfType<GridSplitter>().Count()==2,"Splitters missing");
                    Check(grid.ColumnDefinitions[0].ActualWidth>=260 && grid.ColumnDefinitions[2].ActualWidth>=340,"Panel content clipped");
                    Check(window.FindControl<ComboBox>("FontPicker").ItemCount==FontCatalog.All.Count,"Font choices missing");
                    var titleField=window.ActiveServiceField<TextBox>("ServiceTitle"); var dateField=window.ActiveServiceField<CalendarDatePicker>("ServiceDate");
                    var titleOrigin=titleField.TranslatePoint(default,window).Value; var dateOrigin=dateField.TranslatePoint(default,window).Value; var tabsOrigin=tabs.TranslatePoint(default,window).Value;
                    Check(titleOrigin.X<210 && dateOrigin.X<210 && dateOrigin.Y>titleOrigin.Y,"Service fields are not stacked in the sidebar");
                    Check(Math.Abs(titleField.Bounds.Width-dateField.Bounds.Width)<.1 && titleField.Bounds.Width>=150,"Service fields do not use full available width");
                    Check(tabsOrigin.X<250,"Tabs did not move left after relocating service fields");
                    var newButton=window.FindControl<Button>("NewServiceButton"); var historyList=window.FindControl<ListBox>("HistoryList");
                    Check(Math.Abs(newButton.Bounds.Width-historyList.Bounds.Width)<.1,"New service button does not match the navigation width");
                    Check(dateField.SelectedDate==demo.Date && dateField.CustomDateFormatString=="yyyy-MM-dd","Date display changed");
                    Check(window.FindControl<TextBox>("Lyrics").Bounds.Height>300,"Lyrics did not gain vertical space");
                    var colour=window.FindControl<Button>("TextColorPicker");
                    var swatches=window.FindControl<UniformGrid>("ColorSwatches");
                    Check(swatches.Children.Count==64 && swatches.Columns==8 && swatches.Rows==8,"Colour grid missing");
                    var yellow=swatches.Children.OfType<Button>().Single(b=>((TextColorChoice)b.Tag).Hex=="#FFE46B");
                    yellow.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
                    Check(store.LoadHistory().Single().Items[1].Formatting.TextColor=="#FFE46B","Tab colour did not save");
                    var slideCanvas=(Canvas)window.FindControl<Viewbox>("SlideHost").Child;
                    Check(slideCanvas.Children.OfType<TextBlock>().All(t=>t.Foreground is ISolidColorBrush brush && brush.Color==Color.Parse("#FFE46B")),"Colour did not reach preview");
                    var fontPicker=window.FindControl<ComboBox>("FontPicker");
                    var selectedFont=(FontChoice)fontPicker.SelectedItem;
                    Check(fontPicker.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==selectedFont.DisplayName && t.FontFamily.Equals(selectedFont.Family)),"Selected font name does not use its typeface");
                    var left=window.FindControl<ToggleButton>("AlignLeft"); var center=window.FindControl<ToggleButton>("AlignCenter"); var right=window.FindControl<ToggleButton>("AlignRight");
                    Check(center.IsChecked==true && left.IsChecked==false && right.IsChecked==false,"Saved center alignment not selected");
                    right.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(right.IsChecked==true && center.IsChecked==false && left.IsChecked==false,"Alignment is not exclusive");
                    right.IsChecked=false; right.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(right.IsChecked==true,"Clicking the selected alignment deselects all buttons");
                    Check(store.LoadHistory().Single().Items[1].Formatting.Alignment=="Right","Alignment did not save");
                    center.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var thumbs=window.FindControl<ListBox>("Thumbnails"); thumbs.SelectedIndex=2;
                    Check(window.FindControl<TextBlock>("PreviewInfo").Text.StartsWith("3 /"),"Thumbnail does not update preview immediately");
                    thumbs.Focus(); thumbs.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent, Key=Key.Down });
                    Check(thumbs.SelectedIndex==3 && window.FindControl<TextBlock>("PreviewInfo").Text.StartsWith("4 /"),"Arrow key does not update preview");
                    window.FindControl<TextBox>("SongOrder").Text="v1 c";
                    titleField.Text="感恩崇拜";
                } finally { window.Close(); }
                Check(store.LoadHistory().Single().Items[1].Order=="v1 c","Closing did not save latest edit");
                Check(store.LoadHistory().Single().Title=="感恩崇拜","Service title stopped being editable");
            });
            Test("Full-service preview tracks each tab separately and navigates with all four arrow keys",()=> {
                var plan=new ServicePlan { Items=new() {
                    new ServiceItem { Title="相同名稱",Lyrics="[v1]\n第一首\n[c]\n副歌",Order="v1 c" },
                    new ServiceItem { Title="相同名稱",Lyrics="[v1]\n第二首",Order="v1" },
                    new ServiceItem { Title="讀經",Lyrics="平安",Order="" },
                    new ServiceItem { Title="公告",Lyrics="[1]\n第一項\n[2]\n第二項",Order="" }
                }};
                var pages=SlideLayout.Build(plan,settings);
                Check(pages.Select(s=>s.ItemSlideNumber).SequenceEqual(new[]{1,2,3,1,2,1,1,2}),"Tab slide numbers did not reset");
                Check(pages.Select(s=>s.ItemSlideCount).SequenceEqual(new[]{3,3,3,2,2,1,2,2}),"Tab slide totals are wrong");
                var preview=new PreviewWindow(pages); preview.Show(); Dispatcher.UIThread.RunJobs();
                try {
                    var caption=preview.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="TotalSlideCount");
                    var itemCount=preview.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="ItemSlideCount");
                    void Press(Key key,int page,int itemPage,int itemTotal) {
                        preview.KeyPress(key,RawInputModifiers.None,PhysicalKey.None,null);
                        preview.KeyRelease(key,RawInputModifiers.None,PhysicalKey.None,null); Dispatcher.UIThread.RunJobs();
                        Check(caption.Text==page+" / "+pages.Count && itemCount.Text==itemPage+" / "+itemTotal,"Wrong slide/counter after "+key);
                    }
                    Check(caption.Text.EndsWith("1 / "+pages.Count) && itemCount.Text.EndsWith("1 / 3"),"Wrong first preview page");
                    preview.GetVisualDescendants().OfType<Button>().First().Focus();
                    Press(Key.Right,2,2,3); Press(Key.Down,3,3,3); Press(Key.Down,4,1,2);
                    Press(Key.Up,3,3,3); Press(Key.Left,2,2,3); Press(Key.Left,1,1,3);
                    Press(Key.Up,1,1,3); Press(Key.Left,1,1,3); Press(Key.Down,2,2,3);
                    Press(Key.Right,3,3,3); Press(Key.Down,4,1,2); Press(Key.Down,5,2,2);
                    Press(Key.Down,6,1,1); Press(Key.Down,7,1,2); Press(Key.Down,8,2,2);
                    Press(Key.Down,8,2,2); Press(Key.Right,8,2,2); Press(Key.Up,7,1,2);
                } finally { preview.Close(); }
            });
            Test("Avalonia image and settings dialogs initialize",()=> {
                var imageDialog=new BackgroundDialog(images,imported);
                Check(imageDialog.FindControl<Image>("ImagePreview").Source!=null,"Image dialog blank");
                var settingsDialog=new SettingsDialog(settings);
                Check(settingsDialog.FindControl<TextBox>("LicenseText").Text==settings.ChurchLicense,"Settings did not load");
            });
            LanguageTests.Run(Test,Check,root);
            FormattingTests.Run(Test,Check,root);
            EditorInteractionTests.Run(Test,Check,root);
            WindowLayoutTests.Run(Test,Check,root);
            CompositionTests.Run(Test,Check,root);
            DriveUploadTests.Run(Test,Check,root);
            ProseTests.Run(Test,Check,root);
            MediaTests.Run(Test,Check,root,store,images);
            Console.WriteLine("RESULT "+passed+" passed, "+failed+" failed. Outputs: "+root); return failed==0 ? 0 : 1;
        }
        static DataTransfer Transfer(DataTransferItem item) { var data=new DataTransfer(); data.Add(item); return data; }
        static string WithHttp(byte[] bytes,string type,Func<string,string> call)
        {
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port;
            var server=Task.Run(()=> { using(var client=listener.AcceptTcpClient()) using(var stream=client.GetStream()) { var request=new byte[4096]; _ = stream.Read(request,0,request.Length); var header=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: "+type+"\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n"); stream.Write(header,0,header.Length); stream.Write(bytes,0,bytes.Length); } });
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
