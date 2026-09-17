namespace AssetCopilot;

// Program files follow the loaded plugin; user data follows the install marker.
// No drive letter, user name or Rhino process working directory is assumed.
public static class AppPaths
{
    public static string InstallRoot => FindInstallRoot(typeof(AppPaths).Assembly.Location);
    public static string DataRoot => ResolveDataRoot(InstallRoot,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
    public static string Node => Path.Combine(InstallRoot, "runtime", "node.exe");
    public static string Vision => Path.Combine(InstallRoot, "runtime", "vision");

    public static string FindInstallRoot(string assemblyPath)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!);
        for (var d = directory; d != null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "remy-install.ini"))) return d.FullName;
            // Portable/development layout, including existing 0.4.x installations.
            if (Directory.Exists(Path.Combine(d.FullName, "AssetCopilot")) &&
                File.Exists(Path.Combine(d.FullName, "runtime", "node.exe"))) return d.FullName;
        }
        return directory.FullName;
    }

    public static string ResolveDataRoot(string installRoot, string localAppData)
    {
        var marker = Path.Combine(installRoot, "remy-install.ini");
        if (File.Exists(marker))
        {
            string section = "";
            foreach (var raw in File.ReadAllLines(marker))
            {
                var line = raw.Trim();
                if (line.StartsWith("[",StringComparison.Ordinal) && line.EndsWith("]",StringComparison.Ordinal)) { section = line[1..^1]; continue; }
                int equals = line.IndexOf('=');
                if (equals < 0 || !section.Equals("Storage", StringComparison.OrdinalIgnoreCase)) continue;
                if (line[..equals].Trim().Equals("DataRoot", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line[(equals + 1)..].Trim();
                    if (!Compat.IsPathFullyQualified(value)) throw new IOException("The configured data folder is invalid. Run the installer again.");
                    return Path.GetFullPath(value);
                }
            }
            throw new IOException("The installation configuration has no data folder. Run the installer again.");
        }
        if (Directory.Exists(Path.Combine(installRoot, "AssetCopilot")) &&
            File.Exists(Path.Combine(installRoot, "runtime", "node.exe"))) return installRoot;
        return Path.Combine(localAppData, "RemyAssetCopilot", "Data");
    }
}
