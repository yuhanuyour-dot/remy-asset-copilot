using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace AssetCopilot;

public sealed class SizeEstimate
{
    public string Category {get;set;}="";
    public string Source {get;set;}="";
    public string Evidence {get;set;}="";
    public string Model {get;set;}="";
    public string InputHash {get;set;}="";
    public double Score {get;set;}
    public int Axis {get;set;}=-1;
    public double Meters {get;set;}
}
public static class SizeInferenceRules
{
    // Explicit dimensions are an anchor, not a licence to stretch the mesh on all three axes.
    public static SizeEstimate? Text(string text)
    {
        text=(text??"").Trim();if(text.Length==0)return null;
        var ids=Sizing.Matches(text);
        if(ids.Length>1)throw new ArgumentException("描述中有多个目标，请写明这次要生成哪一个物体，例如“只生成黑色椅子”。");
        var dimensions=Regex.Matches(text,@"(?<axis>宽(?:度)?|高(?:度)?|深(?:度)?|\bwidth\b|\bheight\b|\bdepth\b)\s*(?:为|是|约|大约|=|:|：)?\s*(?<number>\d+(?:\.\d+)?)\s*(?<unit>毫米|厘米|公分|英寸|英尺|米|mm\b|cm\b|inches?\b|inch\b|feet\b|ft\b|m\b)",RegexOptions.IgnoreCase);
        if(dimensions.Count>1)throw new ArgumentException("为保持模型比例，请在描述中指定一个尺寸基准（宽、高或深），其他方向会等比缩放。");
        if(dimensions.Count==1)
        {
            var d=dimensions[0];var u=d.Groups["unit"].Value.ToLowerInvariant();double unit=u switch{"毫米" or "mm"=>.001,"厘米" or "公分" or "cm"=>.01,"英寸" or "inch" or "inches"=>.0254,"英尺" or "feet" or "ft"=>.3048,_=>1};
            double meters=double.Parse(d.Groups["number"].Value,System.Globalization.CultureInfo.InvariantCulture)*unit;
            if(meters<.001||meters>1000)throw new ArgumentException("描述中的尺寸超出可用范围，请检查数值与单位。");
            string a=d.Groups["axis"].Value.ToLowerInvariant();int axis=a.StartsWith("高")||a=="height"?2:a.StartsWith("深")||a=="depth"?1:0;
            return new(){Category=ids.FirstOrDefault()??"",Source="文字尺寸",Axis=axis,Meters=meters,Evidence=d.Value,Model="explicit-dimension-v1"};
        }
        if(ids.Length==1)return new(){Category=ids[0],Source="描述识别",Model="object-vocabulary-v2"};
        return null;
    }
    public static SizeEstimate Visual(IReadOnlyList<(string Id,double Score)> ranked,string hash)
    {
        var sorted=ranked.OrderByDescending(x=>x.Score).ToArray();
        if(sorted.Length<2||sorted.Any(x=>!double.IsFinite(x.Score)||x.Score<0||x.Score>1))throw new InvalidOperationException("图像识别结果无效，请重新添加图片。");
        var first=sorted[0];double margin=first.Score-sorted[1].Score;
        if(first.Id.StartsWith("unknown")||!Sizing.Presets.Any(p=>p.Id==first.Id)||first.Score<.30||margin<.05)
            throw new ArgumentException("暂时无法可靠判断图中的目标。请在描述框补充物体名称，或改用手动尺寸。");
        return new(){Category=first.Id,Source="图片识别",Model="CLIP ViT-B/32 · 本地",InputHash=hash,Score=first.Score};
    }
}

// A per-window service. Caches image-content hashes, never filenames, keys or cloud responses.
public sealed class LocalSizeInference : IDisposable
{
    public static string Runtime => AppPaths.Vision;
    readonly Dictionary<string,Task<SizeEstimate>> cache=new();
    readonly CancellationTokenSource lifetime=new();
    readonly SemaphoreSlim gate=new(1,1);
    static readonly (string Id,string Label)[] RejectLabels=[
        ("unknown-room","a complete furnished room interior"),("unknown-building","a building exterior"),
        ("unknown-toy","a miniature toy furniture model"),("unknown-art","an abstract sculpture"),
        ("unknown-person","a person"),("unknown-animal","an animal"),("unknown-food","food"),
        ("unknown-landscape","a landscape"),("unknown-ui","a screenshot of a software interface"),
        ("unknown-logo","a logo or text on a blank background")];
    public static IReadOnlyList<(string Id,string Label)> Labels=>Sizing.Presets.Select(p=>(p.Id,"a "+p.Aliases.First(a=>a.All(c=>c<128)))).Concat(RejectLabels).ToArray();
    public async Task<SizeEstimate> InferAsync(string text,string? image,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var words=SizeInferenceRules.Text(text);if(words!=null)return words;
        if(string.IsNullOrWhiteSpace(image)||!File.Exists(image))throw new ArgumentException("添加图片或描述目标物体后，会自动估算尺寸；本地 GLB 可在描述框补充物体名称。");
        var file=new FileInfo(image);if(file.Length==0||file.Length>20*1024*1024)throw new ArgumentException("尺寸识别图片需为不超过 20 MB 的 JPG / PNG。");
        // Async hashing also validates the actual source contents, independent of the attachment name.
        byte[] bytes=await File.ReadAllBytesAsync(image,ct);string hash=Convert.ToHexString(SHA256.HashData(bytes));
        Task<SizeEstimate> task;
        lock(cache)
        {
            if(cache.TryGetValue(hash,out var previous)&&!previous.IsFaulted&&!previous.IsCanceled)task=previous;
            else{if(cache.Count>=8){foreach(var key in cache.Where(x=>x.Value.IsCompleted).Select(x=>x.Key).ToArray())cache.Remove(key);}task=RunImage(bytes,Path.GetExtension(image),hash,lifetime.Token);cache[hash]=task;}
        }
        return await task.WaitAsync(ct);
    }
    async Task<SizeEstimate> RunImage(byte[] bytes,string extension,string hash,CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        string snapshot=Path.Combine(AssetStore.Work,"size-input-"+Guid.NewGuid().ToString("N")+(extension.Equals(".png",StringComparison.OrdinalIgnoreCase)?".png":".jpg"));
        try
        {
            string node=AppPaths.Node,worker=Path.Combine(AppContext.BaseDirectory,"tools","size-inference.mjs");
            // In Rhino AppContext.BaseDirectory is Rhino/System, not the plugin directory.
            worker=Path.Combine(Path.GetDirectoryName(typeof(LocalSizeInference).Assembly.Location)!,"tools","size-inference.mjs");
            if(!File.Exists(node)||!File.Exists(worker)||!File.Exists(Path.Combine(Runtime,"model","onnx","model_quantized.onnx")))throw new InvalidOperationException("本地图片识别组件缺失，请运行完整版安装程序并勾选图片尺寸识别；也可先用文字描述估算。");
            Directory.CreateDirectory(AssetStore.Work);await File.WriteAllBytesAsync(snapshot,bytes,ct);
            var start=new ProcessStartInfo(node){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Runtime,StandardInputEncoding=new UTF8Encoding(false)};
            start.ArgumentList.Add(worker);start.ArgumentList.Add(Runtime);start.ArgumentList.Add(AssetStore.Work);start.Environment["TEMP"]=AssetStore.Work;start.Environment["TMP"]=AssetStore.Work;
            using var process=Process.Start(start)??throw new InvalidOperationException("无法启动本地图像识别。");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(90));
            using var kill=timeout.Token.Register(()=>{try{if(!process.HasExited)process.Kill(true);}catch{}});
            var labels=Labels;
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{image=snapshot,labels=labels.Select(x=>x.Label)}));process.StandardInput.Close();
            try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new TimeoutException("图片识别用时过长，请补充文字描述或稍后重试。");}
            string json=await output;await error;ct.ThrowIfCancellationRequested();
            if(process.ExitCode!=0)throw new InvalidOperationException("本地图片识别失败，请检查图片，或在描述框补充目标物体。");
            using var parsed=JsonDocument.Parse(json);
            var ranked=parsed.RootElement.EnumerateArray().Select(row=>(labels.First(x=>x.Label==row.GetProperty("label").GetString()).Id,row.GetProperty("score").GetDouble())).ToArray();
            return SizeInferenceRules.Visual(ranked,hash);
        }
        finally{try{File.Delete(snapshot);}catch{}gate.Release();}
    }
    public void Dispose(){lifetime.Cancel();lifetime.Dispose();}
}
