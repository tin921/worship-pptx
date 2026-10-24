using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WorshipPptx;
using SkiaSharp;

class TutorialRenderer
{
 static string root=Path.GetFullPath("docs/tutorial/review-0.93.35");
 static string shots=Path.Combine(root,"rendered");
 static MainWindow main;
 static LocalStore store;
 static ServicePlan plan;
 static readonly List<object> geometry=new();
 static void Pump(int ms=50){var until=DateTime.UtcNow.AddMilliseconds(ms);do{Dispatcher.UIThread.RunJobs();Thread.Sleep(5);}while(DateTime.UtcNow<until);}
 static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
 static T C<T>(string name) where T:Control=>main.FindControl<T>(name) ?? main.ActiveServiceField<T>(name);
 static ServiceItem Selected=>(ServiceItem)((TabItem)C<TabControl>("SongTabs").SelectedItem).Tag;
 static void Refresh(){typeof(MainWindow).GetField("loading",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,true);Call(main,"FillEditor");typeof(MainWindow).GetField("loading",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(main,false);Call(main,"RefreshPreview");Call(main,"SaveDraft");Pump(60);}
 static void Text(string name,string value){C<TextBox>(name).Text=value;Call(main,"RefreshPreview");Pump();}
 static void Select(int n){C<TabControl>("SongTabs").SelectedIndex=n;Pump();}
 static void Add(string title,string lyrics,string order,string background,string color,double size=48)
 {
  var tabs=C<TabControl>("SongTabs");tabs.SelectedIndex=tabs.Items.Count-1;Pump();
  Selected.Background=background;Selected.Formatting.FontSize=size;Selected.Formatting.TextColor=color;
  Text("SongTitle",title);Text("Lyrics",lyrics);Text("SongOrder",order);Text("CopyrightText","原創示例歌詞 · 可自由使用");
  Selected.Background=background;Selected.Formatting.FontSize=size;Selected.Formatting.TextColor=color;Refresh();
 }
 static void Click(string name){C<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
 static string ControlName(Control control)
 {
  var names=new Dictionary<string,string>{{"編輯背景","EditBackground"},{"預覽整個崇拜","PreviewService"},{"製作 PowerPoint","CreatePowerPoint"},{"設定","OpenSettings"},{"移除","RemoveItem"},{"＋ 新增崇拜","NewService"}};
  if(!string.IsNullOrEmpty(control.Name))return control.Name;
  if(control is Button confirm && confirm.Content as string=="移除" && TopLevel.GetTopLevel(confirm) is Window owner && owner.Title=="移除崇拜")return "ConfirmRemoval";
  if(control is Button reset && reset.GetValue(Avalonia.Automation.AutomationProperties.NameProperty)=="重設平移及縮放")return "ResetBackground";
  if(control is Button card && card.Tag is MediaAsset asset)return "Card_"+asset.Id;
  if(control is Button button && button.Content is string label && names.ContainsKey(label))return names[label];
  if(control is TabItem tab && tab.Tag==null)return "AddTab";
  return "";
 }
 static SKBitmap Pixels(Window window)
 {
  using var image=window.CaptureRenderedFrame() ?? throw new Exception("No app render");
  using var stream=new MemoryStream();image.Save(stream);stream.Position=0;return SKBitmap.Decode(stream);
 }
 static void Snap(string name,Window window=null)
 {
  window??=main;Pump(90);AvaloniaHeadlessPlatform.ForceRenderTimerTick();
  using var mainPixels=Pixels(main);using var foreground=window==main?null:Pixels(window);
  using var result=new SKBitmap(1600,900);using var canvas=new SKCanvas(result);
  canvas.Clear(new SKColor(216,222,216));
  int dx=(1600-mainPixels.Width)/2,dy=(900-mainPixels.Height)/2;
  canvas.DrawBitmap(mainPixels,dx,dy);
  if(foreground!=null){
   dx=(1600-foreground.Width)/2;dy=(900-foreground.Height)/2;
   using var dim=new SKPaint{Color=new SKColor(20,35,30,65)};canvas.DrawRect(0,0,1600,900,dim);
   using var edge=new SKPaint{Color=new SKColor(125,142,133)};canvas.DrawRect(dx-1,dy-1,foreground.Width+2,foreground.Height+2,edge);
   if(window is BackgroundImageWindow) { dx=dy=0; canvas.Clear(SKColors.Black); canvas.DrawBitmap(foreground,new SKRect(0,0,1600,900)); }
   else canvas.DrawBitmap(foreground,dx,dy);
  }
  using var rendered=SKImage.FromBitmap(result);using var data=rendered.Encode(SKEncodedImageFormat.Png,100);
  using(var file=File.Create(Path.Combine(shots,name+".png")))data.SaveTo(file);
  var rects=window.GetVisualDescendants().OfType<Control>().Where(c=>!string.IsNullOrEmpty(ControlName(c))&&c.IsEffectivelyVisible).Select(c=>{
   var p=c.TranslatePoint(new Point(),window)??new Point();return new{name=ControlName(c),x=p.X+dx,y=p.Y+dy,w=c.Bounds.Width,h=c.Bounds.Height};}).ToList();
  geometry.Add(new{name,width=1600,height=900,controls=rects});Console.WriteLine("FRAME "+name);
 }
 static void SetTheme(string background,string color,double size){Selected.Background=background;Selected.Formatting.TextColor=color;Selected.Formatting.FontSize=size;Refresh();}

 static void Press(Window w,Key key) { w.Focus(); w.RaiseEvent(new KeyEventArgs {RoutedEvent=InputElement.KeyDownEvent,Key=key});Pump(); }
 static void Styles(BackgroundDialog d,string only) { foreach(var b in d.FindControl<WrapPanel>("StyleFilters").Children.Cast<ToggleButton>()) b.IsChecked=only==null || (string)b.Tag==only; Pump(); }
 static void WaitMatch(BackgroundDialog d) { var end=DateTime.UtcNow.AddSeconds(100); while(d.FindControl<Border>("MatchingStatus").IsVisible && DateTime.UtcNow<end)Pump(80); if(d.FindControl<Border>("MatchingStatus").IsVisible)throw new Exception("Matching timeout"); }
 static void Sequence(string name,int frames,Action<int> act) { for(int i=0;i<frames;i++){act(i);Snap($"{name}-{i:000}");} }
 [STAThread] static void Main(string[] args)
 {
  Directory.CreateDirectory(shots);
  AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions{UseHeadlessDrawing=false}).SetupWithoutStarting();
  var profile=args.Contains("--upload") ? File.ReadAllText(Path.Combine(root,"profile-path.txt")) : Path.GetFullPath("E:/WorshipPptx-work/tutorial-profile-"+Guid.NewGuid().ToString("N")); File.WriteAllText(Path.Combine(root,"profile-path.txt"),profile);
  store=new LocalStore(profile);
  if(args.Contains("--upload")) { Upload(profile); return; }
  var older=new ServicePlan {Title="上週崇拜",Date=new DateTime(2026,10,18),Items=new(){new ServiceItem{Title="宣召",Lyrics="你們要讚美耶和華！",Order=""}}};store.Save(older);
  plan=new ServicePlan{Title="主日崇拜",Date=new DateTime(2026,10,25),Items=new(){new ServiceItem{Title="未命名",Kind="scripture",Lyrics="",Order=""}}};
  store.Save(plan);store.SaveSettings(new UserSettings {PptxSaveFolder=Path.Combine(root,"output")});
  main=new MainWindow(store){Width=1600,Height=900};main.Show();Pump(250);
  C<Grid>("WorkArea").ColumnDefinitions[0].Width=new GridLength(370);Pump();
  plan=(ServicePlan)typeof(MainWindow).GetField("plan",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
  Snap("01-empty");Text("ServiceTitle","主日敬拜");Snap("02-name");
  C<Grid>("WorkArea").ColumnDefinitions[0].Width=new GridLength(400);Pump();Snap("03-panel");
  Sequence("03-window",12,i=>{main.Width=1600-160*(i+1)/12.0;main.Height=900-80*(i+1)/12.0;Pump(35);});
  main.Width=1600;main.Height=900;Pump();
  C<Grid>("WorkArea").ColumnDefinitions[0].Width=new GridLength(370);Pump();
  // Use a disposable practice program to show real confirmation and date-based selection.
  var practice=new ServicePlan { Title="練習崇拜",Date=new DateTime(2026,11,1),Items=new(){new ServiceItem {Title="練習項目",Lyrics="",Order=""}} };
  store.Save(practice);Call(main,"RefreshHistory");Pump();
  C<ListBox>("HistoryList").SelectedItem=C<ListBox>("HistoryList").Items.Cast<ServicePlan>().Single(p=>p.Id==practice.Id);Pump();Snap("03-remove-selected");
  Click("RemoveServiceButton");var confirmation=main.OwnedWindows.Single();
  var confirmRemove=confirmation.GetVisualDescendants().OfType<Button>().Single(b=>b.Content as string=="移除");
  Snap("03-remove-confirm",confirmation);confirmRemove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(150);
  plan=(ServicePlan)typeof(MainWindow).GetField("plan",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(main);
  if(plan.Date.Date!=new DateTime(2026,10,25) || store.LoadHistory().Any(p=>p.Id==practice.Id))throw new Exception("Removal did not select the nearest upcoming program");
  Snap("03-removed-next");
  Text("SongTitle","宣召經文");Text("Lyrics","詩篇 100:1–2\n\n普天下當向耶和華歡呼！\n你們當樂意事奉耶和華，\n當來向他歌唱！");
  Selected.Background="media:sunrise-minimal";Selected.Formatting.TextColor="#FFFFFF";Refresh();Snap("04-invocation");
  Snap("04-pip-on");C<ToggleButton>("PipToggle").IsChecked=false;Click("PipToggle");Snap("04-pip-off");
  C<ToggleButton>("PipToggle").IsChecked=true;Click("PipToggle");Snap("04-pip-streaming");
  C<TabControl>("SongTabs").SelectedIndex=1;Pump();Snap("05-new-song");Text("SongTitle","同心頌讚");
  Text("Lyrics","[v1]\n今天聚集在主跟前\n同心獻上感恩頌讚\n\n[c]\n願我一生為祢而活\n讓祢的愛充滿我心\n\n[v2]\n在困倦裡祢賜盼望\n前路有祢不再孤單");
  Selected.Background="media:sunrise-illustration";Selected.Formatting.TextColor="#444444";Refresh();Snap("06-song-lyrics");
  Text("SongOrder","v1 c v2 c");Snap("07-order");Text("CopyrightText","原創示例歌詞 · 可自由使用");Snap("08-credits");
  C<ListBox>("Thumbnails").SelectedIndex=1;Pump();Snap("09-format");
  C<ComboBox>("FontPicker").IsDropDownOpen=true;Pump();Snap("10-font-list");C<ComboBox>("FontPicker").IsDropDownOpen=false;
  C<Button>("TextColorPicker").Flyout.ShowAt(C<Button>("TextColorPicker"));Pump();Snap("11-color");C<Button>("TextColorPicker").Flyout.Hide();
  C<ToggleButton>("BoldToggle").IsChecked=true;Click("BoldToggle");Click("AlignLeft");Snap("12-align");
  C<ToggleButton>("BoldToggle").IsChecked=false;Click("BoldToggle");Click("AlignCenter");Click("AutoFontSizeButton");Snap("13-auto");
  C<ComboBox>("FontSizePicker").SelectedItem=48.0;Pump();Snap("14-size");
  C<ListBox>("Thumbnails").SelectedIndex=0;Pump();C<ComboBox>("FontSizePicker").SelectedItem=64.0;Pump();Snap("15-title-size");
  C<ListBox>("Thumbnails").SelectedIndex=1;Pump();Snap("16-lyric-size");Click("AutoFontSizeButton");
  var dialog=new BackgroundDialog(new ImageStore(store.Assets),Selected.Background,Selected.Title,Selected.Lyrics){Width=1400,Height=820};dialog.Show(main);Pump(30);Snap("17-matching",dialog);WaitMatch(dialog);Snap("18-matched",dialog);
  foreach(var style in new[]{"photo","illustration","abstract","sculpted","minimal"}){Styles(dialog,style);Snap("19-"+style,dialog);}
  Styles(dialog,null);var scroll=dialog.FindControl<ScrollViewer>("GalleryScroll");
  for(int i=0;i<6;i++){var p=scroll.TranslatePoint(new Point(250,250),dialog).Value;dialog.MouseWheel(p,new Vector(0,-3),RawInputModifiers.None);Pump(120);Snap($"20-scroll-{i:000}",dialog);}
  scroll.Offset=default;Pump();Snap("21-gallery",dialog);
  var card=dialog.FindControl<WrapPanel>("Gallery").Children.Cast<Button>().First();
  var asset=(MediaAsset)card.Tag;_=SlideVisual.Background(asset.Key);Pump();
  var pt=card.TranslatePoint(new Point(card.Bounds.Width/2,card.Bounds.Height/3),dialog).Value;
  // Native double-click opening/closing is verified separately with Windows input.
  // Render the same handler directly, avoiding synthetic timestamp sensitivity under load.
  Call(dialog,"SelectBackground",asset.Key);Call(dialog,"ShowGalleryImage",asset);Pump();
  var full=dialog.OwnedWindows.OfType<BackgroundImageWindow>().Single();Snap("22-full-double",full);
  full.Close();Pump();Snap("23-back",dialog);  var expand=card.GetVisualDescendants().OfType<Button>().First(b=>b.Name?.StartsWith("Expand_")==true);expand.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();full=dialog.OwnedWindows.OfType<BackgroundImageWindow>().Single();Press(full,Key.Escape);Snap("25-google-link",dialog);
  File.WriteAllText(Path.Combine(root,"google-image-search.txt"),dialog.FindControl<HyperlinkButton>("GoogleImageLink").NavigateUri.ToString());
  Snap("26-import",dialog);Call(dialog,"SelectBackground","media:sunrise-illustration");Snap("27-apply",dialog);dialog.Close();
  Selected.Background="media:sunrise-illustration";Refresh();Snap("28-transform",main);
  var projection=(CompositionPreview)C<Viewbox>("SlideHost").Child;
  Sequence("29-zoom",30,i=>projection.ZoomBy(Math.Pow(1.6,1.0/30),new Point(640,360)));
  var dragA=projection.TranslatePoint(new Point(550,300),main).Value;main.MouseDown(dragA,MouseButton.Left);
  Sequence("30-pan",30,i=>main.MouseMove(new Point(dragA.X+90*(i+1)/30,dragA.Y+22*(i+1)/30)));main.MouseUp(new Point(dragA.X+90,dragA.Y+22),MouseButton.Left);Pump();
  Click("ZoomIn");Snap("31-plus");Click("ZoomOut");Snap("32-minus");
  C<ToggleButton>("FlipBackground").IsChecked=true;Click("FlipBackground");Snap("33-flip");
  Sequence("34-opacity",30,i=>C<Slider>("BackgroundOpacity").Value=100-(i+1));
  Call(main,"ResetBackground_Click",null,new RoutedEventArgs());Pump();Snap("35-reset");
  Add("恩典引路","[v1]\n祢的恩典伴我前行\n每個清晨都有新盼望\n\n[c]\n讓我腳步跟隨祢\n一生走在祢的光中\n\n[v2]\n縱然路途有風有雨\n祢的信實從不曾改變","v1 c v2 c","media:mountains-minimal","#444444",48);Snap("36-second-song");
  Add("光中前行","[v1]\n主祢的話照亮我心\n帶我走過寂靜的黑夜\n\n[c]\n我要歌唱祢的慈愛\n讓全地聽見祢的榮耀\n\n[v2]\n當我軟弱祢賜力量\n每步道路祢與我同行","v1 c v2 c","media:sunrise-minimal","#FFFFFF",48);Snap("37-third-song");
  Add("讀經","詩篇 23:1–3\n\n耶和華是我的牧者，我必不致缺乏。\n他使我躺臥在青草地上，\n領我在可安歇的水邊。\n他使我的靈魂甦醒，\n為自己的名引導我走義路。","","media:mountains-minimal","#444444",28);Text("CopyrightText","");Snap("38-reading");
  plan.MoveItem(plan.Items[3],2);Call(main,"LoadPlan");Pump();Snap("39-reorder");plan.MoveItem(plan.Items[2],4);Call(main,"LoadPlan");Select(1);Pump();
  C<TextBox>("Lyrics").Focus();Snap("39-field-focus");Press(main,Key.Escape);Snap("40-escape");Press(main,Key.Right);Snap("41-right");Press(main,Key.Left);Snap("42-left");Press(main,Key.Down);Snap("43-down");Press(main,Key.Up);Snap("44-up");Press(main,Key.Up);Snap("45-boundary");
  Press(main,Key.PageDown);Snap("46-pagedown");Press(main,Key.PageUp);Snap("47-pageup");
  // Return to the recorded service via its actual history selection.
  C<ListBox>("HistoryList").SelectedItem=C<ListBox>("HistoryList").Items.Cast<ServicePlan>().First(p=>p.Id==plan.Id);Pump();Select(1);Snap("48-before-preview");
  Press(main,Key.F5);var preview=main.OwnedWindows.OfType<PreviewWindow>().Single();Snap("49-preview",preview);Press(preview,Key.Down);Snap("50-preview-next",preview);Press(preview,Key.Right);Snap("51-preview-right",preview);Press(preview,Key.Left);Snap("52-preview-left",preview);Press(preview,Key.Escape);Snap("53-export");
  Snap("64-feedback-link");
  LocalStore.Write(Path.Combine(root,"tutorial-service.json"),plan);store.Save(plan);
  var slides=SlideLayout.Build(plan,new UserSettings());PptxWriter.Write(Path.Combine(root,"output/1025c-call+praise.pptx"),slides,plan.Title);
  File.WriteAllText(Path.Combine(root,"geometry.json"),JsonSerializer.Serialize(geometry,new JsonSerializerOptions{WriteIndented=true}));
  File.WriteAllText(Path.Combine(root,"capture-report.json"),JsonSerializer.Serialize(new{slideCount=slides.Count,source="Actual Avalonia controls rendered with Skia; input handlers drive demonstrations",profile,created=DateTimeOffset.Now}));
  Console.WriteLine("CAPTURE COMPLETE");
 }
 static void Upload(string profile)
 {
  // Replace this generated sequence completely when recording another upload.
  foreach(var frame in Directory.EnumerateFiles(shots,"57-upload-*.png"))File.Delete(frame);
  plan=LocalStore.Read<ServicePlan>(Path.Combine(root,"tutorial-service.json"));main=new MainWindow(store){Width=1600,Height=900};main.Show();Pump(150);
  var privateConfig=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WorshipPptx/google-drive.json");
  File.Copy(privateConfig,Path.Combine(profile,"google-drive.json"),true);
  var slides=SlideLayout.Build(plan,new UserSettings());
  var upload=new DriveUploadWindow(slides,plan.Title,"TutorialDemo-1025c-call+praise.pptx",profile);upload.Show(main);Snap("54-upload",upload);
  upload.FindControl<TextBox>("UploadFolder").Text=Path.Combine(root,"output");Snap("55-upload-path",upload);
  upload.FindControl<CheckBox>("ReplaceExisting").IsChecked=true;Snap("56-upload-ready",upload);
  var timer=System.Diagnostics.Stopwatch.StartNew();upload.FindControl<Button>("StartUpload").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  var records=new List<object>();int n=0;
  while(!(bool)typeof(DriveUploadWindow).GetField("completed",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(upload) && timer.Elapsed.TotalSeconds<120) {
   Pump(500);Snap($"57-upload-{n:000}",upload);records.Add(new {shot=$"57-upload-{n:000}",seconds=timer.Elapsed.TotalSeconds});n++;
   if(upload.FindControl<Button>("StartUpload").IsEnabled)throw new Exception("Upload failed: "+upload.FindControl<TextBox>("UploadLog").Text);
  }
  Pump(250);if(upload.FindControl<TextBlock>("StepLabel").Text!="上傳完成")throw new Exception("Upload did not complete");Snap("58-upload-complete",upload);
  File.WriteAllText(Path.Combine(root,"upload-timing.json"),JsonSerializer.Serialize(records));
  File.WriteAllText(Path.Combine(root,"upload-log.txt"),upload.FindControl<TextBox>("UploadLog").Text);
  File.WriteAllText(Path.Combine(root,"upload-geometry.json"),JsonSerializer.Serialize(geometry));Console.WriteLine("REAL UPLOAD COMPLETE");
 }
}
