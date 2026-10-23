using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WorshipPptx
{
    public sealed class MediaAsset
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Theme { get; set; }
        public string Style { get; set; }
        public string Lighting { get; set; }
        public bool Core { get; set; }
        public string Still { get; set; }
        public string Key => "media:"+Id;
        public string StyleLabel => Style switch { "photo"=>ChineseText.Ui("攝影"), "illustration"=>ChineseText.Ui("插畫"), "abstract"=>ChineseText.Ui("抽象"), "watercolour"=>ChineseText.Ui("柔光水彩"), "sculpted"=>ChineseText.Ui("立體象徵"), "generic"=>Lighting=="daylight" ? ChineseText.Ui("通用 · 日光") : ChineseText.Ui("通用 · 夜色"), _=>ChineseText.Ui("原有素材") };
    }
    public static class MediaLibrary
    {
        public static readonly JsonSerializerOptions JsonOptions=new() { PropertyNameCaseInsensitive=true };
        public static string Root => Path.Combine(AppContext.BaseDirectory,"Media");
        private static readonly Lazy<IReadOnlyList<MediaAsset>> assets=new(()=>
        {
            var path=Path.Combine(Root,"catalog.json");
            return File.Exists(path) ? JsonSerializer.Deserialize<List<MediaAsset>>(File.ReadAllText(path),JsonOptions).AsReadOnly() : Array.Empty<MediaAsset>();
        });
        public static IReadOnlyList<MediaAsset> All => assets.Value;
        public static MediaAsset Find(string key) => key?.StartsWith("media:",StringComparison.Ordinal)==true ? All.FirstOrDefault(x=>x.Id==key.Substring(6)) : null;
        public static string FilePath(string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)) return "";
            var root=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
            var path=Path.GetFullPath(Path.Combine(Root,relative.Replace('/',Path.DirectorySeparatorChar)));
            if(!path.StartsWith(root,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)) throw new IOException(ChineseText.Ui("素材路徑不正確。"));
            return path;
        }
        public static string StillPath(string key)
        {
            if(key?.StartsWith("media:",StringComparison.Ordinal)!=true) return key ?? "";
            var asset=Find(key) ?? throw new IOException(ChineseText.Ui("找不到已選的內置素材，請重新選擇背景。"));
            return FilePath(asset.Still);
        }
        public static bool Daylight(string key) => Find(key)?.Lighting=="daylight";
    }
}
