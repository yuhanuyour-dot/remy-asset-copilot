using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace AssetCopilot;

public static class GlbDecoder
{
    public static AssetData Read(string path)
    {
        AssetStore.Initialize();
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || bytes.Length > 150 * 1024 * 1024) return GlbReader.Read(bytes);
        int length = BitConverter.ToInt32(bytes, 12);
        if (length < 0 || length > bytes.Length - 20) return GlbReader.Read(bytes);
        using var json = JsonDocument.Parse(bytes.AsMemory(20, length));
        if (!json.RootElement.TryGetProperty("bufferViews", out var views) || !views.EnumerateArray().Any(v => v.TryGetProperty("extensions", out var e) && e.TryGetProperty("EXT_meshopt_compression", out _))) return GlbReader.Read(bytes);
        string folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        string script = Path.Combine(folder, "tools/decode-glb.mjs");
        string node = AppPaths.Node;
        if (!File.Exists(node)) throw new IOException("本地模型解码组件缺失，请重新运行标准版或完整版安装程序，无需重新生成模型。");
        string output = Path.Combine(AssetStore.Work, "AssetCopilot-" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            var info = new ProcessStartInfo(node) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            Compat.SetArguments(info,script,Path.GetFullPath(path),output);
            Process process;
            try { process = Process.Start(info) ?? throw new IOException("解码器未启动。"); }
            catch (System.ComponentModel.Win32Exception) { throw new IOException("压缩模型需要 Node.js 运行时。请重新安装标准版或完整版，再打开 Rhino 导入缓存模型，无需重新生成。"); }
            using (process)
            {
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { process.Kill(); throw new IOException("本地模型解压超时，原模型已保留。"); }
                if (process.ExitCode != 0) throw new IOException("本地模型解压失败，原模型已保留。请检查插件 tools 文件夹是否完整。");
            }
            var result = GlbReader.Read(File.ReadAllBytes(output));
            result.Warnings.Add("已在本机解压 Meshopt 模型，原始文件已保留。");
            return result;
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }
}
