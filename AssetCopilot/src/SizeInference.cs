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
        if(ids.Length>1)throw new ArgumentException("The description contains multiple objects. Specify one target, for example: generate only the black chair.");
        var dimensions=Regex.Matches(text,@"(?<axis>宽(?:度)?|高(?:度)?|深(?:度)?|\bwidth\b|\bheight\b|\bdepth\b)\s*(?:为|是|约|大约|=|:|：)?\s*(?<number>\d+(?:\.\d+)?)\s*(?<unit>毫米|厘米|公分|英寸|英尺|米|mm\b|cm\b|inches?\b|inch\b|feet\b|ft\b|m\b)",RegexOptions.IgnoreCase);
        if(dimensions.Count>1)throw new ArgumentException("Specify one dimension (width, height, or depth) to preserve the model's proportions. Other dimensions scale uniformly.");
        if(dimensions.Count==1)
        {
            var d=dimensions[0];var u=d.Groups["unit"].Value.ToLowerInvariant();double unit=u switch{"毫米" or "mm"=>.001,"厘米" or "公分" or "cm"=>.01,"英寸" or "inch" or "inches"=>.0254,"英尺" or "feet" or "ft"=>.3048,_=>1};
            double meters=double.Parse(d.Groups["number"].Value,System.Globalization.CultureInfo.InvariantCulture)*unit;
            if(meters<.001||meters>1000)throw new ArgumentException("The described size is outside the supported range. Check the value and units.");
            string a=d.Groups["axis"].Value.ToLowerInvariant();int axis=a.StartsWith("高")||a=="height"?2:a.StartsWith("深")||a=="depth"?1:0;
            return new(){Category=ids.FirstOrDefault()??"",Source="Explicit text dimension",Axis=axis,Meters=meters,Evidence=d.Value,Model="explicit-dimension-v1"};
        }
        if(ids.Length==1)return new(){Category=ids[0],Source="Recognized from description",Model="object-vocabulary-v2"};
        return null;
    }
    public static SizeEstimate Visual(IReadOnlyList<(string Id,double Score)> ranked,string hash)
    {
        var sorted=ranked.OrderByDescending(x=>x.Score).ToArray();
        if(sorted.Length<2||sorted.Any(x=>!Compat.IsFinite(x.Score)||x.Score<0||x.Score>1))throw new InvalidOperationException("The image recognition result is invalid. Add the image again.");
        var first=sorted[0];double margin=first.Score-sorted[1].Score;
        if(first.Id.StartsWith("unknown")||!Sizing.Presets.Any(p=>p.Id==first.Id)||first.Score<.30||margin<.05)
            throw new ArgumentException("The target in the image could not be identified reliably. Add its name to the description or use Manual size.");
        return new(){Category=first.Id,Source="Recognized from image",Model="CLIP ViT-B/32 · local",InputHash=hash,Score=first.Score};
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
        if(string.IsNullOrWhiteSpace(image)||!File.Exists(image))throw new ArgumentException("Add an image or describe the target to estimate its size. For a local GLB, add the object name to the description.");
        var file=new FileInfo(image);if(file.Length==0||file.Length>20*1024*1024)throw new ArgumentException("Size estimation requires a JPG or PNG image of up to 20 MB.");
        // Async hashing also validates the actual source contents, independent of the attachment name.
        byte[] bytes=await Compat.ReadAllBytesAsync(image!,ct);string hash=Compat.HashHex(bytes);
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
            if(!File.Exists(node)||!File.Exists(worker)||!File.Exists(Path.Combine(Runtime,"model","onnx","model_quantized.onnx")))throw new InvalidOperationException("The offline image recognition component is missing. Run the Full installer and select Offline image size estimates, or use a text description.");
            Directory.CreateDirectory(AssetStore.Work);await Compat.WriteAllBytesAsync(snapshot,bytes,ct);
            var start=new ProcessStartInfo(node){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Runtime};
            Compat.SetArguments(start,worker,Runtime,AssetStore.Work);start.EnvironmentVariables["TEMP"]=AssetStore.Work;start.EnvironmentVariables["TMP"]=AssetStore.Work;
            using var process=Process.Start(start)??throw new InvalidOperationException("Could not start offline image recognition.");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(90));
            using var kill=timeout.Token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch{}});
            var labels=Labels;
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new{image=snapshot,labels=labels.Select(x=>x.Label)}));process.StandardInput.Close();
            try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new TimeoutException("Image recognition timed out. Add a text description or try again later.");}
            string json=await output;await error;ct.ThrowIfCancellationRequested();
            if(process.ExitCode!=0)throw new InvalidOperationException("Offline image recognition failed. Check the image or describe the target object.");
            using var parsed=JsonDocument.Parse(json);
            var ranked=parsed.RootElement.EnumerateArray().Select(row=>(labels.First(x=>x.Label==row.GetProperty("label").GetString()).Id,row.GetProperty("score").GetDouble())).ToArray();
            return SizeInferenceRules.Visual(ranked,hash);
        }
        finally{try{File.Delete(snapshot);}catch{}gate.Release();}
    }
    public void Dispose(){lifetime.Cancel();lifetime.Dispose();}
}
