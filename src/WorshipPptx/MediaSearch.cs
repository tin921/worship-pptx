using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace WorshipPptx;

public sealed class MediaScene
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Query { get; set; }
    public bool Core { get; set; }
    public string[] Cues { get; set; }
    public float[] Vector { get; set; }
}

public sealed record MediaMatch(string Theme,string Keywords,IReadOnlyList<MediaAsset> Assets);
public sealed record MediaSearchProgress(int? Percent,string Message);

// Matching starts when Edit Background is opened or the user explicitly requests a new match.
public static class MediaSearch
{
    private static readonly Lazy<LocalEncoder> engine=new(()=>new LocalEncoder());
    private static readonly SemaphoreSlim gate=new(1,1);
    private sealed record CachedMatch(MediaScene Scene,IReadOnlyList<MediaAsset> Assets);
    private static readonly Dictionary<string,CachedMatch> matches=new();
    private static readonly Queue<string> matchOrder=new();
    public static int InferenceCount { get; private set; }
    public static bool IsModelLoaded => engine.IsValueCreated;
    public static async Task<IReadOnlyList<MediaAsset>> SearchAsync(string title,string lyrics,CancellationToken cancellation=default) => (await MatchAsync(title,lyrics,cancellation).ConfigureAwait(false)).Assets;
    public static Task<MediaMatch> MatchAsync(string title,string lyrics,CancellationToken cancellation=default,IProgress<MediaSearchProgress> progress=null) => Match(title,lyrics,false,cancellation,progress);
    public static Task<MediaMatch> MatchTermsAsync(string terms,CancellationToken cancellation=default,IProgress<MediaSearchProgress> progress=null) => Match("",terms,true,cancellation,progress);
    // Ignore tags and spacing that only change presentation layout. Retain English
    // word boundaries so "now here" never collides with the different word "nowhere".
    public static string ContentHash(string title,string lyrics,bool custom=false)
    {
        string Clean(string text) {
            text=ChineseText.Convert(Regex.Replace(text ?? "",@"\[[^\]\r\n]*\]",""),ChineseText.Simplified);
            text=Regex.Replace(text.Trim(),@"\s+"," ");
            return Regex.Replace(text,@"(?<=[\p{IsCJKUnifiedIdeographs}\p{P}])\s+|\s+(?=[\p{IsCJKUnifiedIdeographs}\p{P}])","");
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]{custom ? "terms" : "song",Clean(title),Clean(lyrics)}))));
    }
    private static Task<MediaMatch> Match(string title,string lyrics,bool custom,CancellationToken cancellation,IProgress<MediaSearchProgress> progress) => Task.Run(async ()=>
    {
        progress?.Report(new(null,ChineseText.Ui("正在準備分析…")));
        await gate.WaitAsync(cancellation);
        try
        {
            cancellation.ThrowIfCancellationRequested();
            if(string.IsNullOrWhiteSpace(title+lyrics)) throw new ArgumentException(ChineseText.Ui("請先輸入詩歌名稱、歌詞或搜尋字詞。"));
            if((title?.Length ?? 0)+(lyrics?.Length ?? 0)>20000) throw new ArgumentException(ChineseText.Ui("搜尋內容請保持在 20,000 字以內。"));
            var key=ContentHash(title,lyrics,custom);
            if(matches.TryGetValue(key,out var cached)) {
                progress?.Report(new(100,ChineseText.Ui("已使用記憶中的主題配對")));
                return new MediaMatch(cached.Scene.Title,ImageKeywords.ForDisplay(cached.Scene),cached.Assets);
            }
            progress?.Report(new(null,IsModelLoaded ? ChineseText.Ui("正在準備歌詞…") : ChineseText.Ui("正在載入本機分析工具，首次開啟需稍候…")));
            var encoder=engine.Value;
            cancellation.ThrowIfCancellationRequested();
            var cleanTitle=encoder.Normalize(title ?? "");
            var lines=Regex.Replace(lyrics ?? "",@"\[[^\]\r\n]*\]", "").Split('\n').Select(s=>encoder.Normalize(s).Trim()).Where(s=>s.Length>0).Distinct();
            var cleanLyrics=string.Join(" ",lines);
            var lyricTokens=encoder.ChunkTokens(cleanLyrics.Length>0 ? cleanLyrics : cleanTitle);
            var hasTitle=cleanTitle.Length>0 && cleanLyrics.Length>0;
            var total=lyricTokens.Count+(hasTitle ? 1 : 0)+2;
            var completed=0;
            void Report(string message) => progress?.Report(new((int)Math.Round(100.0*completed/total),message));
            Report(ChineseText.Ui("正在擷取主題及關鍵字…"));
            var chunks=new List<float[]>();
            foreach(var tokens in lyricTokens) {
                cancellation.ThrowIfCancellationRequested(); chunks.Add(encoder.Encode(tokens)); InferenceCount++; completed++;
                Report(ChineseText.Ui("正在擷取主題及關鍵字…"));
            }
            var document=LocalEncoder.Unit(Enumerable.Range(0,384).Select(i=>chunks.Average(c=>c[i])).ToArray());
            if(hasTitle)
            {
                cancellation.ThrowIfCancellationRequested();
                var titleVector=encoder.Encode(encoder.ChunkTokens(cleanTitle)[0]); InferenceCount++; completed++;
                Report(ChineseText.Ui("正在配對背景主題…"));
                document=LocalEncoder.Unit(document.Select((v,i)=>.85f*v+.15f*titleVector[i]).ToArray());
            }
            var scenes=JsonSerializer.Deserialize<List<MediaScene>>(File.ReadAllText(Path.Combine(MediaLibrary.Root,"scenes.json")),MediaLibrary.JsonOptions);
            var text=cleanTitle+" "+cleanLyrics;
            var scored=scenes.Select(s=>new { Scene=s, Hits=s.Cues.Count(c=>text.Contains(c,StringComparison.Ordinal)), TitleHit=s.Cues.Any(c=>cleanTitle.Contains(c,StringComparison.Ordinal)) })
                .Select(s=>new { s.Scene, Preferred=custom || s.Scene.Core || s.Hits>0, Score=.7f*Dot(s.Scene.Vector,document)+.3f*chunks.Max(c=>Dot(s.Scene.Vector,c))+Math.Min(s.Hits,2)*.18f+(s.TitleHit ? .35f : 0) }).ToList();
            var chosen=new List<MediaScene>();
            while(chosen.Count<4 && scored.Any(s=>s.Preferred))
            {
                var next=scored.Where(s=>s.Preferred).MaxBy(s=>.85*s.Score-.15*chosen.Select(c=>Dot(c.Vector,s.Scene.Vector)).DefaultIfEmpty(0).Max());
                chosen.Add(next.Scene); scored.Remove(next);
            }
            cancellation.ThrowIfCancellationRequested();
            var first=chosen.First();
            completed++; Report(ChineseText.Ui("正在整理圖片關鍵字…"));
            // Keep the strongest diverse matches first, then browse the remaining
            // ranked themes by scrolling, without a separate "show all" action.
            var ranked=chosen.Concat(scored.OrderByDescending(s=>s.Score).Select(s=>s.Scene));
            var result=new MediaMatch(first.Title,ImageKeywords.ForDisplay(first),ranked.SelectMany(s=>MediaLibrary.All.Where(a=>a.Theme==s.Id)).ToList());
            cancellation.ThrowIfCancellationRequested();
            if(matches.Count>=128) matches.Remove(matchOrder.Dequeue());
            matches[key]=new(first,result.Assets); matchOrder.Enqueue(key);
            completed++; Report(ChineseText.Ui("主題及關鍵字已就緒"));
            return result;
        }
        finally { gate.Release(); }
    },cancellation);
    private static float Dot(float[] a,float[] b) => a.Zip(b,(x,y)=>x*y).Sum();
}

public sealed class LocalEncoder : IDisposable
{
    private readonly SentencePieceTokenizer tokenizer;
    private readonly InferenceSession session;
    private readonly Dictionary<string,string> normalization;
    public static int AvailableWorkerCount => Math.Max(1,Environment.ProcessorCount-2);
    public LocalEncoder(int? workerCount=null)
    {
        var root=Path.Combine(AppContext.BaseDirectory,"Models");
        using var input=File.OpenRead(Path.Combine(root,"sentencepiece.bpe.model"));
        tokenizer=SentencePieceTokenizer.Create(input,false,false);
        normalization=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(root,"normalization.json")));
        // Short 128-token inputs stop benefiting beyond eight workers on the
        // measured desktop CPU. Keep at least two logical processors available.
        using var options=new SessionOptions { IntraOpNumThreads=Math.Clamp(workerCount ?? Math.Min(8,AvailableWorkerCount),1,Math.Max(1,Environment.ProcessorCount)),InterOpNumThreads=1 };
        session=new InferenceSession(Path.Combine(root,"model.onnx"),options);
    }
    public string Normalize(string text)
    {
        var result=new StringBuilder();
        foreach(var rune in text.EnumerateRunes()) { var s=rune.ToString(); result.Append(normalization.TryGetValue(s,out var converted)?converted:s); }
        return Regex.Replace(result.ToString(),@"\s+"," ").Trim();
    }
    public long[] Tokenize(string text) => new long[]{0}.Concat(tokenizer.EncodeToIds(text,false,false).Select(i=>i==0 ? 3L : i+1L)).Append(2).ToArray();
    public List<float[]> EncodeChunks(string text,CancellationToken cancellation=default)
    {
        var result=new List<float[]>();
        foreach(var chunk in ChunkTokens(text)) { cancellation.ThrowIfCancellationRequested(); result.Add(Encode(chunk)); }
        return result;
    }
    public List<long[]> ChunkTokens(string text)
    {
        var tokens=Tokenize(text).Skip(1).SkipLast(1).ToArray();
        var result=new List<long[]>();
        for(var i=0;i<Math.Max(1,tokens.Length);i+=126)
        {
            result.Add(new long[]{0}.Concat(tokens.Skip(i).Take(126)).Append(2).ToArray());
        }
        return result;
    }
    public float[] Encode(long[] tokens)
    {
        var shape=new[]{1,tokens.Length};
        var inputs=new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor("input_ids",new DenseTensor<long>(tokens,shape)),NamedOnnxValue.CreateFromTensor("attention_mask",new DenseTensor<long>(Enumerable.Repeat(1L,tokens.Length).ToArray(),shape)) };
        if(session.InputMetadata.ContainsKey("token_type_ids")) inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids",new DenseTensor<long>(new long[tokens.Length],shape)));
        using var output=session.Run(inputs);
        var hidden=output.First().AsTensor<float>();
        var pooled=new float[384];
        for(var t=0;t<tokens.Length;t++) for(var d=0;d<384;d++) pooled[d]+=hidden[0,t,d]/tokens.Length;
        return Unit(pooled);
    }
    public static float[] Unit(float[] v) { var length=Math.Max(1e-12,Math.Sqrt(v.Sum(x=>(double)x*x))); return v.Select(x=>(float)(x/length)).ToArray(); }
    public void Dispose() => session.Dispose();
}
