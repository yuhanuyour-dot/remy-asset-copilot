using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssetCopilot;

public sealed class TripoClient : IDisposable, IGenerationClient
{
    readonly HttpClient api;
    readonly HttpClient files;
    readonly string secret;
    public TripoClient(string key, HttpMessageHandler? handler=null)
    {
        if(string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Enter your Tripo API key in Connection settings.");
        secret=key.Trim();
        if(secret.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)) secret=secret[7..].Trim();
        if(secret.Length==0 || secret.Any(char.IsWhiteSpace)) throw new ArgumentException("Paste only the API key, without spaces or line breaks.");
        api=handler==null?new HttpClient():new HttpClient(handler);
        api.BaseAddress=new Uri("https://openapi.tripo3d.ai/v3/");
        api.Timeout=TimeSpan.FromSeconds(90);
        api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",secret);
        files=new HttpClient { Timeout=TimeSpan.FromMinutes(5) }; // Never forward the key to CDN URLs.
    }
    string Safe(string value)
    {
        value=value.Replace(secret,"[key redacted]");
        value=Regex.Replace(value,@"(?i)Bearer\s+[^\s""<>]+|\b(?:sk|tripo)[-_][a-zA-Z0-9_-]+","[key redacted]");
        value=Regex.Replace(value,@"https?://\S+|[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}","[address redacted]");
        value=Regex.Replace(value,@"\s+"," ").Trim();
        return value.Length>400?value[..400]+"…":value;
    }
    async Task<JsonElement> Result(HttpResponseMessage response,CancellationToken ct,string stage)
    {
        using(response)
        {
            var text=await response.Content.ReadAsStringAsync(ct);
            JsonDocument? parsed=null;
            try{parsed=JsonDocument.Parse(text);}catch(JsonException){}
            using var doc=parsed;
            string detail="The server did not return a JSON error. A gateway or access policy may have blocked the request.";
            if(doc!=null)
            {
                var fields=new List<string>();
                if(doc.RootElement.ValueKind==JsonValueKind.Object)
                    foreach(var name in new[]{"code","message","msg","detail","error"})
                        if(doc.RootElement.TryGetProperty(name,out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number) fields.Add(name+": "+Safe(v.ToString()));
                detail=fields.Count>0?string.Join("\n",fields):"The JSON response contains no error details.";
            }
            bool apiError=doc!=null && doc.RootElement.ValueKind==JsonValueKind.Object && doc.RootElement.TryGetProperty("code",out var code) && code.ToString()!="0";
            if(!response.IsSuccessStatusCode || apiError)
                throw Compat.HttpError($"Stage: {stage}\nHTTP {(int)response.StatusCode}\n{detail}\n\nA 403 alone does not indicate insufficient credit or an invalid key. Use Check connection for a read-only authentication check. Do not repeatedly submit generation requests.",response.StatusCode);
            if(doc==null) throw new InvalidDataException($"{stage}: The API returned non-JSON content. Check your connection and service status.");
            var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("data",out var data)) throw new InvalidDataException($"{stage}: The response is missing the data field.");
            return data.Clone();
        }
    }
    public async Task CheckConnection(CancellationToken ct)
    {
        await Result(await api.GetAsync("account/balance",ct),ct,"Read-only authentication check GET /v3/account/balance");
    }
    public async Task<string> Upload(string path,CancellationToken ct)
    {
        var info=new FileInfo(path); var ext=info.Extension.ToLowerInvariant();
        if(!info.Exists || info.Length==0 || info.Length>20*1024*1024 || (ext!=".jpg" && ext!=".jpeg" && ext!=".png")) throw new ArgumentException("Choose a JPG or PNG image of up to 20 MB.");
        using var form=new MultipartFormDataContent(); using var stream=File.OpenRead(path); using var content=new StreamContent(stream);
        content.Headers.ContentType=new MediaTypeHeaderValue(ext==".png"?"image/png":"image/jpeg"); form.Add(content,"file",Path.GetFileName(path));
        var data=await Result(await api.PostAsync("files",form,ct),ct,"Image upload POST /v3/files"); return data.GetProperty("file_token").GetString()!;
    }
    public Task<string> Generate(string token,CancellationToken ct) => Generate(token,false,new GenerationOptions(GenerationOptions.V31,5000),ct);
    public async Task<string> Generate(string input,bool text,GenerationOptions options,CancellationToken ct)
    {
        var endpoint=text?"generation/text-to-model":"generation/image-to-model";
        using var body=new StringContent(JsonSerializer.Serialize(options.Payload(input,text)),Encoding.UTF8,"application/json");
        var data=await Result(await api.PostAsync(endpoint,body,ct),ct,"Submit generation POST /v3/"+endpoint);
        return data.GetProperty("task_id").GetString()!;
    }
    public async Task<string> EditImage(string input,string prompt,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(prompt)||prompt.Length>1024)throw new ArgumentException("Enter a description of 1–1,024 characters.");
        var instruction="Extract or adjust the target object in the reference image according to the user's instructions for subsequent generation of a single 3D asset. Preserve its structure, proportions, colors, and materials. Do not change the design unless requested. Remove unrelated objects and the background, and show the complete target against a simple neutral background. User instructions: "+prompt.Trim();
        using var body=new StringContent(JsonSerializer.Serialize(new{input,prompt=instruction,model="seedream_v5",size="2K",output_format="png"}),Encoding.UTF8,"application/json");
        var data=await Result(await api.PostAsync("generation/image-to-image",body,ct),ct,"Processing reference image from description");
        return data.GetProperty("task_id").GetString()!;
    }
    public async Task<JsonElement> GetTask(string id,CancellationToken ct)
    {
        if(!Regex.IsMatch(id,@"\A[a-zA-Z0-9_-]{1,120}\z")) throw new ArgumentException("The task ID format is invalid.");
        return await Result(await api.GetAsync("tasks/"+id,ct),ct,"Task lookup GET /v3/tasks/{id}");
    }
    public Task<string> Wait(string id,Action<string> progress,CancellationToken ct)=>WaitOutput(id,false,progress,ct);
    public Task<string> WaitImage(string id,Action<string> progress,CancellationToken ct)=>WaitOutput(id,true,progress,ct);
    async Task<string> WaitOutput(string id,bool image,Action<string> progress,CancellationToken ct)
    {
        var until=DateTime.UtcNow.AddMinutes(15);
        while(DateTime.UtcNow<until)
        {
            var t=await GetTask(id,ct); var status=t.GetProperty("status").GetString();
            var percent=t.TryGetProperty("progress",out var p)?p.ToString():""; progress($"Generating · {status} {percent}%");
            if(status=="success")
            {
                var output=t.GetProperty("output");
                foreach(var name in (image?new[]{"generated_image_url","generated_image","image_url"}:new[]{"model_url","pbr_model","model"}))
                    if(output.TryGetProperty(name,out var url) && url.ValueKind==JsonValueKind.String && Uri.TryCreate(url.GetString(),UriKind.Absolute,out var uri) && uri.Scheme=="https") return uri.AbsoluteUri;
                throw new InvalidDataException("Generation succeeded, but no download URL was returned. Keep the task ID and check the Tripo dashboard.");
            }
            if(status is "failed" or "cancelled" or "canceled" or "banned" or "expired") throw new InvalidOperationException("Task ended: "+status+". Check the reference image or the Tripo dashboard for details.");
            await Task.Delay(2500,ct);
        }
        throw new TimeoutException("The 15-minute wait has ended. The task ID is saved; resume checking instead of submitting again.");
    }
    public async Task Download(string url,string path,CancellationToken ct)
    {
        var uri=new Uri(url);
        if(uri.Scheme!="https" || uri.IsLoopback) throw new InvalidDataException("The model download URL is invalid.");
        using var response=await files.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct); response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>150*1024*1024) throw new InvalidDataException("The model exceeds the 150 MB limit.");
        using var input=await response.Content.ReadAsStreamAsync(ct); using var output=File.Create(path+".partial");
        byte[] buffer=new byte[81920]; long total=0; int n;
        while((n=await input.ReadAsync(buffer,0,buffer.Length,ct))>0) { total+=n; if(total>150*1024*1024) throw new InvalidDataException("The model exceeds the 150 MB limit."); await output.WriteAsync(buffer,0,n,ct); }
        output.Close(); Compat.ReplaceFile(path+".partial",path);
    }
    public void Dispose() { api.Dispose(); files.Dispose(); }
}
