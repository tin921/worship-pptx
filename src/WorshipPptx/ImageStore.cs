using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using SkiaSharp;
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
            using var stream=new MemoryStream(bytes);
            using var codec=SKCodec.Create(stream);
            if(codec==null) throw new IOException("未能讀取圖片。請使用 PNG、JPEG、WebP、BMP 或 GIF；網址必須直接連結圖片。");
            var info=codec.Info;
            if((long)info.Width*info.Height>40000000 || info.Width>20000 || info.Height>20000) throw new IOException("圖片尺寸太大，請縮小至 4,000 萬像素以下。");
            using var decoded=SKBitmap.Decode(codec) ?? throw new IOException("圖片資料不完整。");
            using var image=SKImage.FromBitmap(decoded);
            using var png=image.Encode(SKEncodedImageFormat.Png,100);
            return SaveBytes(png.ToArray());
        }
        private string SaveBytes(byte[] bytes)
        {
            var name=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()+".png";
            var path=Path.Combine(folder,name);
            if(!File.Exists(path)) File.WriteAllBytes(path,bytes);
            return path;
        }
        public string SaveBitmap(Bitmap bitmap)
        {
            if((long)bitmap.PixelSize.Width*bitmap.PixelSize.Height>40000000) throw new IOException("圖片尺寸太大。");
            using var stream=new MemoryStream(); bitmap.Save(stream,PngBitmapEncoderOptions.Default); return ImportBytes(stream.ToArray());
        }
        public async Task<string> ImportUrlAsync(string url, CancellationToken cancellation = default(CancellationToken))
        {
            Uri uri;
            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !string.IsNullOrEmpty(uri.UserInfo)) throw new IOException("請輸入 http:// 或 https:// 開頭的圖片網址。");
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("WorshipPptx/1.0");
                using (var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellation).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > MaxBytes) throw new IOException("圖片不能超過 25 MB。");
                    using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new MemoryStream())
                    {
                        timeout.CancelAfter(TimeSpan.FromSeconds(30));
                        var buffer = new byte[81920]; int read;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) > 0)
                        { if (output.Length + read > MaxBytes) throw new IOException("圖片不能超過 25 MB。"); await output.WriteAsync(buffer, 0, read, timeout.Token).ConfigureAwait(false); }
                        return ImportBytes(output.ToArray());
                    }
                }
            }
        }
        public static Bitmap Load(string path) => new Bitmap(path);
    }
}
