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
        if (!File.Exists(node)) throw new IOException("The local model decoder is missing. Run the Standard or Full installer again; you do not need to regenerate the model.");
        string output = Path.Combine(AssetStore.Work, "AssetCopilot-" + Guid.NewGuid().ToString("N") + ".glb");
        try
        {
            var info = new ProcessStartInfo(node) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
            Compat.SetArguments(info,script,Path.GetFullPath(path),output);
            Process process;
            try { process = Process.Start(info) ?? throw new IOException("The decoder could not be started."); }
            catch (System.ComponentModel.Win32Exception) { throw new IOException("Compressed models require the bundled Node.js runtime. Reinstall Standard or Full, then reopen Rhino and import the cached model. Regeneration is not required."); }
            using (process)
            {
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { process.Kill(); throw new IOException("Local model decompression timed out. The original model is preserved."); }
                if (process.ExitCode != 0) throw new IOException("Local model decompression failed. The original model is preserved. Check that the plugin's tools folder is complete.");
            }
            var result = GlbReader.Read(File.ReadAllBytes(output));
            result.Warnings.Add("The Meshopt model was decompressed locally. The original file is preserved.");
            return result;
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }
}
