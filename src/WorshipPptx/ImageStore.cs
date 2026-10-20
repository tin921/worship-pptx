using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
namespace WorshipPptx
{
    public sealed class ImageStore
    {
        public const int MaxBytes = 25 * 1024 * 1024;
        private readonly string folder;
        public ImageStore(string folder) { this.folder = folder; Directory.CreateDirectory(folder); }
        public string ImportFile(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists) throw new IOException("找不到圖片檔案。");
            if (info.Length > MaxBytes) throw new IOException("圖片不能超過 25 MB。");
            return ImportBytes(File.ReadAllBytes(path));
        }
        public string ImportBytes(byte[] bytes)
        {
            if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new IOException("請使用 25 MB 以下的圖片。");
            try
            {
                using (var stream = new MemoryStream(bytes))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
                    var frame = decoder.Frames[0];
                    if ((long)frame.PixelWidth * frame.PixelHeight > 40000000 || frame.PixelWidth > 20000 || frame.PixelHeight > 20000) throw new IOException("圖片尺寸太大，請縮小至 4,000 萬像素以下。");
                    return SaveBitmap(frame);
                }
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is System.IO.FileFormatException || ex is ArgumentException)
            { throw new IOException("未能讀取圖片。請使用 PNG、JPEG、BMP、GIF 或 TIFF；網頁網址必須直接連結圖片。", ex); }
        }
        public string SaveBitmap(BitmapSource bitmap)
        {
            if ((long)bitmap.PixelWidth * bitmap.PixelHeight > 40000000) throw new IOException("圖片尺寸太大。");
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = new MemoryStream())
            {
                encoder.Save(stream); var bytes = stream.ToArray();
                using (var hash = SHA256.Create())
                {
                    var name = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() + ".png";
                    var path = Path.Combine(folder, name);
                    if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
                    return path;
                }
            }
        }
        public async Task<string> ImportUrlAsync(string url, CancellationToken cancellation = default(CancellationToken))
        {
            Uri uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("請輸入 http:// 或 https:// 開頭的圖片網址。");
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WorshipPptx/1.0");
                using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > MaxBytes) throw new IOException("圖片不能超過 25 MB。");
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    using (var input = await response.Content.ReadAsStreamAsync())
                    using (var output = new MemoryStream())
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(30));
                        var buffer = new byte[81920]; int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token)) > 0)
                        { if (output.Length + read > MaxBytes) throw new IOException("圖片不能超過 25 MB。"); await output.WriteAsync(buffer, 0, read, timeout.Token); }
                        return ImportBytes(output.ToArray());
                    }
                }
            }
        }
        public static BitmapSource Load(string path)
        {
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path, UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
    }
}
