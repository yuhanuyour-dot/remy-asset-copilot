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
        if(string.IsNullOrWhiteSpace(key)) throw new ArgumentException("请在设置里输入 Tripo API Key。");
        secret=key.Trim();
        if(secret.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)) secret=secret[7..].Trim();
        if(secret.Length==0 || secret.Any(char.IsWhiteSpace)) throw new ArgumentException("请只粘贴密钥本身，不要包含空格或换行。");
        api=handler==null?new HttpClient():new HttpClient(handler);
        api.BaseAddress=new Uri("https://openapi.tripo3d.ai/v3/");
        api.Timeout=TimeSpan.FromSeconds(90);
        api.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",secret);
        files=new HttpClient { Timeout=TimeSpan.FromMinutes(5) }; // Never forward the key to CDN URLs.
    }
    string Safe(string value)
    {
        value=value.Replace(secret,"[密钥已隐藏]",StringComparison.Ordinal);
        value=Regex.Replace(value,@"(?i)Bearer\s+[^\s""<>]+|\b(?:sk|tripo)[-_][a-zA-Z0-9_-]+","[密钥已隐藏]");
        value=Regex.Replace(value,@"https?://\S+|[\w.+-]+@[\w.-]+\.[a-zA-Z]{2,}","[地址已隐藏]");
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
            string detail="服务端没有返回 JSON 错误说明。可能由网关或访问策略拦截，需要进一步确认。";
            if(doc!=null)
            {
                var fields=new List<string>();
                if(doc.RootElement.ValueKind==JsonValueKind.Object)
                    foreach(var name in new[]{"code","message","msg","detail","error"})
                        if(doc.RootElement.TryGetProperty(name,out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number) fields.Add(name+": "+Safe(v.ToString()));
                detail=fields.Count>0?string.Join("\n",fields):"JSON 响应没有可显示的错误说明。";
            }
            bool apiError=doc!=null && doc.RootElement.ValueKind==JsonValueKind.Object && doc.RootElement.TryGetProperty("code",out var code) && code.ToString()!="0";
            if(!response.IsSuccessStatusCode || apiError)
                throw new HttpRequestException($"阶段：{stage}\nHTTP {(int)response.StatusCode}\n{detail}\n\n403 本身不能证明余额不足或密钥错误。可先点“检查连接”进行只读鉴权检查；不要重复提交生成。",null,response.StatusCode);
            if(doc==null) throw new InvalidDataException($"{stage}：接口返回了非 JSON 内容，请检查网络或服务状态。");
            var root=doc.RootElement;
            if(root.ValueKind!=JsonValueKind.Object || !root.TryGetProperty("data",out var data)) throw new InvalidDataException($"{stage}：响应缺少 data 字段。");
            return data.Clone();
        }
    }
    public async Task CheckConnection(CancellationToken ct)
    {
        await Result(await api.GetAsync("account/balance",ct),ct,"只读鉴权检查 GET /v3/account/balance");
    }
    public async Task<string> Upload(string path,CancellationToken ct)
    {
        var info=new FileInfo(path); var ext=info.Extension.ToLowerInvariant();
        if(!info.Exists || info.Length==0 || info.Length>20*1024*1024 || (ext!=".jpg" && ext!=".jpeg" && ext!=".png")) throw new ArgumentException("请选择不超过 20 MB 的 JPG 或 PNG 图片。");
        using var form=new MultipartFormDataContent(); using var stream=File.OpenRead(path); using var content=new StreamContent(stream);
        content.Headers.ContentType=new MediaTypeHeaderValue(ext==".png"?"image/png":"image/jpeg"); form.Add(content,"file",Path.GetFileName(path));
        var data=await Result(await api.PostAsync("files",form,ct),ct,"图片上传 POST /v3/files"); return data.GetProperty("file_token").GetString()!;
    }
    public Task<string> Generate(string token,CancellationToken ct) => Generate(token,false,new GenerationOptions(GenerationOptions.V31,5000),ct);
    public async Task<string> Generate(string input,bool text,GenerationOptions options,CancellationToken ct)
    {
        var endpoint=text?"generation/text-to-model":"generation/image-to-model";
        using var body=new StringContent(JsonSerializer.Serialize(options.Payload(input,text)),Encoding.UTF8,"application/json");
        var data=await Result(await api.PostAsync(endpoint,body,ct),ct,"提交生成 POST /v3/"+endpoint);
        return data.GetProperty("task_id").GetString()!;
    }
    public async Task<string> EditImage(string input,string prompt,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(prompt)||prompt.Length>1024)throw new ArgumentException("请输入 1–1024 字的描述。");
        var instruction="根据参考图和用户要求提取或调整目标物体，用于随后生成单个 3D 资产。保留目标的结构、比例、颜色和材质；除非用户要求，不改动设计。去除无关物体与背景，将完整目标置于简洁中性背景中。用户要求："+prompt.Trim();
        using var body=new StringContent(JsonSerializer.Serialize(new{input,prompt=instruction,model="seedream_v5",size="2K",output_format="png"}),Encoding.UTF8,"application/json");
        var data=await Result(await api.PostAsync("generation/image-to-image",body,ct),ct,"按描述处理参考图");
        return data.GetProperty("task_id").GetString()!;
    }
    public async Task<JsonElement> GetTask(string id,CancellationToken ct)
    {
        if(!Regex.IsMatch(id,@"\A[a-zA-Z0-9_-]{1,120}\z")) throw new ArgumentException("任务编号格式不正确。");
        return await Result(await api.GetAsync("tasks/"+id,ct),ct,"任务查询 GET /v3/tasks/{id}");
    }
    public Task<string> Wait(string id,Action<string> progress,CancellationToken ct)=>WaitOutput(id,false,progress,ct);
    public Task<string> WaitImage(string id,Action<string> progress,CancellationToken ct)=>WaitOutput(id,true,progress,ct);
    async Task<string> WaitOutput(string id,bool image,Action<string> progress,CancellationToken ct)
    {
        var until=DateTime.UtcNow.AddMinutes(15);
        while(DateTime.UtcNow<until)
        {
            var t=await GetTask(id,ct); var status=t.GetProperty("status").GetString();
            var percent=t.TryGetProperty("progress",out var p)?p.ToString():""; progress($"生成中 · {status} {percent}%");
            if(status=="success")
            {
                var output=t.GetProperty("output");
                foreach(var name in (image?new[]{"generated_image_url","generated_image","image_url"}:new[]{"model_url","pbr_model","model"}))
                    if(output.TryGetProperty(name,out var url) && url.ValueKind==JsonValueKind.String && Uri.TryCreate(url.GetString(),UriKind.Absolute,out var uri) && uri.Scheme=="https") return uri.AbsoluteUri;
                throw new InvalidDataException("生成成功但缺少下载链接。保留任务编号并检查控制台。");
            }
            if(status is "failed" or "cancelled" or "canceled" or "banned" or "expired") throw new InvalidOperationException("任务已结束："+status+"。请检查原图或控制台信息。");
            await Task.Delay(2500,ct);
        }
        throw new TimeoutException("已等待 15 分钟。任务编号已保留，可继续查询；请勿重复提交。");
    }
    public async Task Download(string url,string path,CancellationToken ct)
    {
        var uri=new Uri(url);
        if(uri.Scheme!="https" || uri.IsLoopback) throw new InvalidDataException("无效模型下载地址。");
        using var response=await files.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct); response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>150*1024*1024) throw new InvalidDataException("模型文件超过 150 MB 上限。");
        using var input=await response.Content.ReadAsStreamAsync(ct); using var output=File.Create(path+".partial");
        byte[] buffer=new byte[81920]; long total=0; int n;
        while((n=await input.ReadAsync(buffer,ct))>0) { total+=n; if(total>150*1024*1024) throw new InvalidDataException("模型文件超过 150 MB 上限。"); await output.WriteAsync(buffer.AsMemory(0,n),ct); }
        output.Close(); File.Move(path+".partial",path,true);
    }
    public void Dispose() { api.Dispose(); files.Dispose(); }
}
