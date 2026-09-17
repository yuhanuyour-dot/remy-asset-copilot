using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace AssetCopilot;

public sealed class AssetRecord
{
    public string Folder { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string ImageTaskId { get; set; } = "";
    public bool ImageComplete { get; set; }
    public string ReferencePath { get; set; } = "";
    public string ActiveTaskId => string.IsNullOrEmpty(TaskId) ? ImageTaskId : TaskId;
    public string State { get; set; } = "created";
    public string Source { get; set; } = "";
    public string Model { get; set; } = "";
    public string Prompt { get; set; } = "";
    public string SourceName { get; set; } = "";
    public SizingSettings? Sizing { get; set; }
    public SizingDecision? AppliedSize { get; set; }
    public SizeEstimate? InferredSize { get; set; }
    public string Document { get; set; } = "";
    public int RequestedFaces { get; set; }
    public int ActualFaces { get; set; }
    public string Created { get; set; } = DateTime.UtcNow.ToString("O");
    public string Glb => Path.Combine(Folder, "model.glb");
    public string Textures => Path.Combine(Folder, "textures");
}
public static class AssetStore
{
    public static string Root => AppPaths.DataRoot;
    public static string Work => Path.Combine(Root, "work");
    static string Settings => Path.Combine(Root, "settings.json");
    public static string LastRecord => Path.Combine(Root, "last-asset.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static void Initialize() { Directory.CreateDirectory(Root); Directory.CreateDirectory(Work); Directory.CreateDirectory(Path.Combine(Root,"models")); }
    public static string ValidateFolder(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Compat.IsPathFullyQualified(value)) throw new ArgumentException("请选择完整的模型保存路径。");
        var path = Compat.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        return path;
    }
    public static string SaveRoot()
    {
        Initialize();
        try { if (File.Exists(Settings)) return ValidateFolder(JsonDocument.Parse(File.ReadAllText(Settings)).RootElement.GetProperty("models").GetString()!); } catch { }
        return Path.Combine(Root,"models");
    }
    public static void SetRoot(string folder) { folder=ValidateFolder(folder); Directory.CreateDirectory(folder);
        var probe=Path.Combine(folder,".remy-write-"+Guid.NewGuid().ToString("N"));
        try { using(File.Create(probe)) {} } finally { if(File.Exists(probe)) File.Delete(probe); }
        WriteJson(Settings,new {models=folder}); }
    public static string Slug(string value)
    {
        var chars = value.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? '_' : c).ToArray();
        var result = new string(chars).Trim(' ','.');
        return string.IsNullOrWhiteSpace(result) ? "Untitled" : result[..Math.Min(result.Length,60)];
    }
    public static AssetRecord Create(string root, string document, string identity, string source, string prompt, GenerationOptions? options)
    {
        root=ValidateFolder(root); Directory.CreateDirectory(root);
        var hash=Compat.HashHex(Encoding.UTF8.GetBytes(identity))[..8];
        var folder=Path.Combine(root,Slug(Path.GetFileNameWithoutExtension(document))+"_"+hash,DateTime.Now.ToString("yyyyMMdd_HHmmss")+"_"+Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(folder); Directory.CreateDirectory(Path.Combine(folder,"textures"));
        var r=new AssetRecord {Folder=folder,Document=document,Source=source,Prompt=prompt,Model=options?.Model??"local",RequestedFaces=options?.Faces??0}; Save(r); return r;
    }
    public static void WriteJson<T>(string path,T value) { File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,Json));Compat.ReplaceFile(path+".tmp",path); }
    public static void Save(AssetRecord record) { WriteJson(Path.Combine(record.Folder,"asset.json"),record);WriteJson(LastRecord,record); }
    public static AssetRecord? Restore()
    {
        try { var r=JsonSerializer.Deserialize<AssetRecord>(File.ReadAllText(LastRecord)); if(r!=null && Directory.Exists(ValidateFolder(r.Folder)))return r; } catch { }
        return null;
    }
    public static void ClearPointer() { WriteJson(LastRecord,new AssetRecord()); }
}

