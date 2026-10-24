using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WorshipPptx;

public static class DriveFileName
{
    public static string Create(ServicePlan plan,string machineName,DateTime today,string fallbackLanguage)
    {
        var pc=Regex.Replace(machineName ?? "", "[^A-Za-z0-9]", "");
        if(pc.Length==0) pc="PC";
        if(pc.Length>64) pc=pc.Substring(0,64);
        return pc+"-"+PresentationFileName.Create(plan,today,fallbackLanguage);
    }
    public static void Validate(string name)
    {
        if(!Regex.IsMatch(name ?? "",@"\A[A-Za-z0-9]{1,64}-[0-9]{4}[mc]-(call\+)?praise\.pptx\z"))
            throw new ArgumentException(ChineseText.Ui("檔名格式：電腦名稱-MMDDm/c-call+praise.pptx（call+ 可省略）。"));
        var date=name.Split('-')[1].Substring(0,4);
        if(!DateTime.TryParseExact("2000"+date,"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _))
            throw new ArgumentException(ChineseText.Ui("檔名中的月份或日期無效。"));
    }
}

public sealed class DriveUploadSettings
{
    public string ServiceUrl { get; set; }="";
    public string UploadKey { get; set; }="";
    public string FolderUrl { get; set; }="";
    public static DriveUploadSettings Load(string root)
    {
        var path=Path.Combine(root,"google-drive.json");
        return File.Exists(path) ? JsonSerializer.Deserialize<DriveUploadSettings>(File.ReadAllText(path),MediaLibrary.JsonOptions) ?? new() : new();
    }
    public void Validate()
    {
        if(!Uri.TryCreate(ServiceUrl,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="script.google.com" || !Regex.IsMatch(uri.AbsolutePath,@"^/macros/s/[A-Za-z0-9_-]+/exec$") || uri.Query.Length>0 || uri.UserInfo.Length>0 || !uri.IsDefaultPort || UploadKey.Length<32 || UploadKey.Contains("PLACEHOLDER",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ChineseText.Ui("Google Drive 上傳服務尚未設定。請管理員完成 docs/google-drive-setup.md，並設定本機 google-drive.json；使用者無須登入 Google。"));
        ValidateFolderUrl(FolderUrl);
    }
    public static Uri ValidateFolderUrl(string url)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || uri.Scheme!="https" || uri.Host!="drive.google.com" || !Regex.IsMatch(uri.AbsolutePath,@"^/drive/(u/[0-9]+/)?folders/[A-Za-z0-9_-]+$") || !uri.IsDefaultPort || uri.UserInfo.Length>0)
            throw new InvalidOperationException(ChineseText.Ui("請設定有效的 Google Drive 資料夾連結。"));
        return uri;
    }
}

public sealed record UploadProgress(string Message,int? Percent=null);
public sealed record DriveUploadResult(string FileId,string FolderUrl,DateTimeOffset? ExpiresAt=null);

// The church-owned Apps Script service authorizes Drive access. No Google identity,
// refresh token or account password is requested or stored on the user's computer.
public sealed class DriveUploadClient
{
    public const int ChunkSize=2*1024*1024;
    public const long MaxFileSize=100L*1024*1024;
    private readonly HttpClient http;
    private readonly DriveUploadSettings settings;
    public DriveUploadClient(HttpClient http,DriveUploadSettings settings) { this.http=http; this.settings=settings; }
    public async Task<DriveUploadResult> UploadAsync(string path,string name,bool replace,IProgress<UploadProgress> progress,CancellationToken cancellation)
    {
        settings.Validate(); DriveFileName.Validate(name); cancellation.ThrowIfCancellationRequested();
        using var file=File.OpenRead(path);
        if(file.Length<=0 || file.Length>MaxFileSize) throw new IOException(ChineseText.Ui("PowerPoint 檔案必須小於或等於 100 MB。"));
        var hash=Convert.ToHexString(await MD5.HashDataAsync(file,cancellation)).ToLowerInvariant(); file.Position=0;
        var sessionId=Guid.NewGuid().ToString("N");
        progress?.Report(new(ChineseText.Ui("正在連接上傳服務及檢查同名檔案…")));
        try {
        var state=await Send(new { action="start",key=settings.UploadKey,sessionId,name,size=file.Length,md5=hash,replace },progress,cancellation);
        CheckFolder(state.FolderUrl);
        var buffer=new byte[ChunkSize];
        while(!state.Complete) {
            cancellation.ThrowIfCancellationRequested();
            if(state.Offset<0 || state.Offset>=file.Length || state.Offset%ChunkSize!=0) throw new IOException("Upload service returned an invalid byte offset.");
            file.Position=state.Offset;
            var length=(int)Math.Min(ChunkSize,file.Length-state.Offset);
            await file.ReadExactlyAsync(buffer.AsMemory(0,length),cancellation);
            var previous=state.Offset;
            state=await Send(new { action="chunk",key=settings.UploadKey,sessionId,offset=previous,data=Convert.ToBase64String(buffer,0,length) },progress,cancellation);
            if(!state.Complete && state.Offset<=previous) throw new IOException("Upload service did not acknowledge the chunk.");
            var percent=(int)Math.Min(100,100L*state.Offset/file.Length);
            progress?.Report(new(ChineseText.Ui("已上傳 ")+state.Offset.ToString("N0")+" / "+file.Length.ToString("N0")+" bytes",percent));
        }
        if(state.Offset!=file.Length || string.IsNullOrWhiteSpace(state.FileId)) throw new IOException("Upload completion could not be verified.");
        CheckFolder(state.FolderUrl);
        progress?.Report(new(ChineseText.Ui("上傳完成；檔案大小及校驗碼已確認。"),100));
        return new(state.FileId,state.FolderUrl,state.ExpiresAt);
        } catch {
            // Best effort release of the filename lease. Never delete completed files.
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(6));
            try { await Send(new { action="cancel",key=settings.UploadKey,sessionId },null,cleanup.Token); }
            catch { progress?.Report(new(ChineseText.Ui("未能確認取消狀態；請檢查資料夾。未完成的上傳鎖定會在 15 分鐘後解除。"))); }
            throw;
        }
    }
    private void CheckFolder(string url)
    {
        var folder=DriveUploadSettings.ValidateFolderUrl(url);
        if(folder.Segments.Last()!=DriveUploadSettings.ValidateFolderUrl(settings.FolderUrl).Segments.Last())
            throw new IOException("The upload service returned a different destination folder. Ask the administrator to check configuration.");
    }
    private sealed class Reply
    {
        public bool Ok { get; set; }
        public bool Complete { get; set; }
        public long Offset { get; set; }
        public string FileId { get; set; }
        public string FolderUrl { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
        public string[] Logs { get; set; }
        public string Error { get; set; }
        public string Stack { get; set; }
    }
    private async Task<Reply> Send(object body,IProgress<UploadProgress> progress,CancellationToken cancellation)
    {
        var json=JsonSerializer.Serialize(body);
        // start and chunk requests are idempotent by session ID / byte offset.
        for(var attempt=1;;attempt++) {
            try {
                using var request=new HttpRequestMessage(HttpMethod.Post,settings.ServiceUrl) { Content=new StringContent(json,Encoding.UTF8,"application/json") };
                using var response=await http.SendAsync(request,cancellation);
                if((int)response.StatusCode==429 || (int)response.StatusCode>=500) throw new HttpRequestException("Upload service temporarily unavailable: HTTP "+(int)response.StatusCode);
                response.EnsureSuccessStatusCode();
                var text=await response.Content.ReadAsStringAsync(cancellation);
                Reply result;
                try { result=JsonSerializer.Deserialize<Reply>(text,MediaLibrary.JsonOptions); }
                catch(JsonException ex) { throw new IOException("The service did not return JSON. Check that the deployed web app allows Anyone and executes as its owner.",ex); }
                if(result==null) throw new IOException("The service returned an empty response.");
                foreach(var line in result.Logs ?? Array.Empty<string>()) progress?.Report(new(Redact(line)));
                if(!result.Ok) throw new IOException(Redact(result.Error+"\n"+result.Stack));
                return result;
            } catch(Exception ex) when((ex is HttpRequestException || ex is TaskCanceledException) && !cancellation.IsCancellationRequested && attempt<3) {
                progress?.Report(new(ChineseText.Ui("連線中斷，正在重試… ")+attempt+"/2"));
                await Task.Delay(TimeSpan.FromSeconds(attempt),cancellation);
            }
        }
    }
    public string Redact(string message) => string.IsNullOrEmpty(settings.UploadKey) ? message : message.Replace(settings.UploadKey,"[redacted]",StringComparison.Ordinal);
}
