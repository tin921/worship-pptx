using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace WorshipPptx;

public partial class DriveUploadWindow : Window
{
    private readonly IList<SlideData> slides;
    private readonly string title,root;
    private CancellationTokenSource cancellation;
    private bool running;
    private DriveUploadClient client;
    public DriveUploadWindow(IList<SlideData> slides,string title,string fileName,string root)
    {
        this.slides=slides; this.title=title; this.root=root;
        InitializeComponent(); InputFocus.Attach(this); UploadName.Text=fileName;
        StepLabel.Text=ChineseText.Ui("無須登入 Google；由教會上傳服務處理。");
        Log(ChineseText.Ui("準備上傳：")+fileName);
        if(!File.Exists(Path.Combine(root,"google-drive.json"))) {
            Outcome.Text=ChineseText.Ui("尚未設定上傳服務。請管理員依照 docs/google-drive-setup.md 完成一次性設定。");
            Log(ChineseText.Ui("設定檔位置：")+Path.Combine(root,"google-drive.json"));
        }
        Closing+=(_,e)=> { if(running) { e.Cancel=true; cancellation?.Cancel(); StepLabel.Text=ChineseText.Ui("正在取消，請稍候…"); } };
    }
    private void Log(string message)
    {
        var safe=client?.Redact(message) ?? message;
        var stamp=DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        foreach(var line in safe.Replace("\r","").Split('\n')) UploadLog.Text+=(UploadLog.Text?.Length>0 ? "\n" : "")+"["+stamp+"] "+line;
        UploadLog.CaretIndex=UploadLog.Text.Length;
    }
    private async void Upload_Click(object sender,RoutedEventArgs e)
    {
        if(running) return;
        running=true; StartUpload.IsEnabled=false; UploadName.IsEnabled=false; ReplaceExisting.IsEnabled=false;
        CloseUpload.Content=ChineseText.Ui("取消上傳"); FolderLink.IsVisible=false; Outcome.Text="";
        cancellation=new CancellationTokenSource(); var token=cancellation.Token;
        var temp=Path.Combine(Path.GetTempPath(),"worship-upload-"+Guid.NewGuid().ToString("N")+".pptx");
        try {
            var settings=DriveUploadSettings.Load(root);
            using var http=new HttpClient { Timeout=TimeSpan.FromSeconds(120) };
            client=new DriveUploadClient(http,settings);
            Log(ChineseText.Ui("步驟 1：檢查設定與檔案名稱。")); settings.Validate();
            var fileName=(UploadName.Text ?? "").Trim(); DriveFileName.Validate(fileName);
            StepLabel.Text=ChineseText.Ui("步驟 2：正在製作 PowerPoint…"); Log(StepLabel.Text);
            UploadProgressBar.IsIndeterminate=true;
            await Task.Run(()=>PptxWriter.Write(temp,slides,title)); token.ThrowIfCancellationRequested();
            Log(ChineseText.Ui("PowerPoint 已完成：")+new FileInfo(temp).Length.ToString("N0")+" bytes");
            var progress=new Progress<UploadProgress>(p=> {
                StepLabel.Text=p.Message; UploadProgressBar.IsIndeterminate=!p.Percent.HasValue;
                if(p.Percent.HasValue) UploadProgressBar.Value=p.Percent.Value;
                Log(p.Message);
            });
            var result=await client.UploadAsync(temp,fileName,ReplaceExisting.IsChecked==true,progress,token);
            // Drain posted progress before displaying the final outcome.
            await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background);
            UploadProgressBar.IsIndeterminate=false; UploadProgressBar.Value=100;
            StepLabel.Text=ChineseText.Ui("上傳完成");
            Outcome.Text=ChineseText.Ui("上傳成功。請開啟資料夾確認檔案。");
            FolderLink.NavigateUri=DriveUploadSettings.ValidateFolderUrl(result.FolderUrl); FolderLink.IsVisible=true;
            Log(ChineseText.Ui("資料夾：")+result.FolderUrl);
        } catch(OperationCanceledException) when(token.IsCancellationRequested) {
            StepLabel.Text=ChineseText.Ui("已停止上傳");
            Outcome.Text=ChineseText.Ui("已取消。若在最後一步取消，請到資料夾確認檔案是否已完成。"); Log(Outcome.Text);
        } catch(Exception ex) {
            StepLabel.Text=ChineseText.Ui("上傳失敗");
            Outcome.Text=ChineseText.Ui("請按「複製全部記錄」，將完整記錄傳給管理員。");
            Log(ex.ToString()); Log(Outcome.Text);
        } finally {
            UploadProgressBar.IsIndeterminate=false;
            try { if(File.Exists(temp)) File.Delete(temp); } catch(Exception ex) { Log("Temporary file cleanup: "+ex.Message); }
            cancellation.Dispose(); cancellation=null; running=false;
            StartUpload.IsEnabled=true; UploadName.IsEnabled=true; ReplaceExisting.IsEnabled=true; CloseUpload.Content=ChineseText.Ui("關閉");
        }
    }
    private async void Copy_Click(object sender,RoutedEventArgs e)
    {
        try { if(Clipboard!=null) await Clipboard.SetValueAsync(DataFormat.Text,UploadLog.Text ?? ""); }
        catch(Exception ex) { Log(ChineseText.Ui("未能複製；可在記錄框全選並複製：")+ex.Message); }
    }
    private void Close_Click(object sender,RoutedEventArgs e) { if(running) cancellation?.Cancel(); else Close(); }
}
