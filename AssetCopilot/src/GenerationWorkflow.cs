namespace AssetCopilot;
public interface IGenerationClient
{
    Task<string> Upload(string path,CancellationToken ct);
    Task<string> Generate(string input,bool text,GenerationOptions options,CancellationToken ct);
    Task<string> EditImage(string input,string prompt,CancellationToken ct);
    Task<string> Wait(string id,Action<string> progress,CancellationToken ct);
    Task<string> WaitImage(string id,Action<string> progress,CancellationToken ct);
    Task Download(string url,string path,CancellationToken ct);
}
public static class GenerationWorkflow
{
    public const string Uncertain="CHECK_CONSOLE";
    public static string Route(bool image,string prompt)=>image?(string.IsNullOrWhiteSpace(prompt)?"image":"image-text"):"text";
    // Save an uncertainty marker before each paid POST. Cancellation/recovery never silently re-submits it.
    public static async Task Run(IGenerationClient client,AssetRecord record,Action<AssetRecord> save,Action<string> progress,CancellationToken ct)
    {
        var options=new GenerationOptions(record.Model,record.RequestedFaces);
        bool mixed=record.Source=="image-text";
        if(mixed && !record.ImageComplete)
        {
            if(string.IsNullOrEmpty(record.ImageTaskId))
            {
                options.Validate();progress("1/2 · 上传参考图");var token=await client.Upload(record.ReferencePath,ct);
                ct.ThrowIfCancellationRequested();record.ImageTaskId=Uncertain;record.State="image-submitting";save(record);
                record.ImageTaskId=await client.EditImage(token,record.Prompt,ct);record.State="image-generating";save(record);
            }
            RequireKnown(record.ImageTaskId);
            var imageUrl=await client.WaitImage(record.ImageTaskId,s=>progress("1/2 · "+s),ct);
            await client.Download(imageUrl,Path.Combine(record.Folder,"processed-reference.png"),ct);
            record.ImageComplete=true;record.State="image-ready";save(record);
        }
        if(string.IsNullOrEmpty(record.TaskId))
        {
            options.Validate();bool text=record.Source=="text";string input=record.Prompt;
            options.Payload(text?input:"validate",text);
            if(!text){progress(mixed?"2/2 · 准备生成模型":"正在上传图片");input=await client.Upload(mixed?Path.Combine(record.Folder,"processed-reference.png"):record.ReferencePath,ct);}
            ct.ThrowIfCancellationRequested();record.TaskId=Uncertain;record.State="model-submitting";save(record);
            record.TaskId=await client.Generate(input,text,options,ct);record.State="generating";save(record);
        }
        RequireKnown(record.TaskId);
        var url=await client.Wait(record.TaskId,s=>progress((mixed?"2/2 · ":"")+s),ct);
        progress("正在下载模型与贴图");await client.Download(url,record.Glb,ct);record.State="downloaded";save(record);
    }
    static void RequireKnown(string id)
    {
        if(id==Uncertain||string.IsNullOrWhiteSpace(id))throw new InvalidOperationException("上次提交的结果尚未确认。请在 Tripo 控制台查到该阶段的任务编号，填入后继续查询，避免重复扣费。");
    }
}
