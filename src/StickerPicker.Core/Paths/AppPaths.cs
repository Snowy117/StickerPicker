using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Json;

namespace StickerPicker.Core.Paths;

public sealed class AppPaths : IAppPaths
{
    private readonly string? _overrideDefaultAppFolder;

    public AppPaths(string? overrideDefaultAppFolder = null)
    {
        _overrideDefaultAppFolder = overrideDefaultAppFolder;
        DefaultAppFolder = ResolveDefaultAppFolder();
        BootstrapPath = Path.Combine(DefaultAppFolder, "bootstrap.json");
        DataRoot = DefaultAppFolder;
        Resolve();
    }

    public string DefaultAppFolder { get; }
    public string BootstrapPath { get; }
    public string DataRoot { get; private set; }
    public string LibraryRoot => Path.Combine(DataRoot, "library");
    public string ConfigPath => Path.Combine(DataRoot, "config.json");
    public string MetadataPath => Path.Combine(DataRoot, "metadata.json");
    public string HashesPath => Path.Combine(DataRoot, "hashes.json");

    public string Resolve()
    {
        Directory.CreateDirectory(DefaultAppFolder);

        var custom = ReadBootstrapDataRoot();
        DataRoot = !string.IsNullOrWhiteSpace(custom) ? Path.GetFullPath(custom) : DefaultAppFolder;

        EnsureDataLayout();
        return DataRoot;
    }

    public void SetDataRoot(string? customDataRoot)
    {
        Directory.CreateDirectory(DefaultAppFolder);

        if (string.IsNullOrWhiteSpace(customDataRoot)
            || PathsEqual(customDataRoot, DefaultAppFolder))
        {
            if (File.Exists(BootstrapPath))
            {
                File.Delete(BootstrapPath);
            }

            DataRoot = DefaultAppFolder;
            EnsureDataLayout();
            return;
        }

        var full = Path.GetFullPath(customDataRoot);
        AtomicJson.Save(BootstrapPath, new BootstrapDocument { DataRoot = full });
        DataRoot = full;
        EnsureDataLayout();
    }

    public void EnsureDataLayout()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(LibraryRoot);
    }

    private string ResolveDefaultAppFolder()
    {
        if (!string.IsNullOrWhiteSpace(_overrideDefaultAppFolder))
        {
            return Path.GetFullPath(_overrideDefaultAppFolder);
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
        {
            local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share");
        }

        return Path.Combine(local, "StickerPicker");
    }

    private string? ReadBootstrapDataRoot()
    {
        if (!File.Exists(BootstrapPath))
        {
            return null;
        }

        try
        {
            var doc = AtomicJson.LoadOrCreate(
                BootstrapPath,
                () => new BootstrapDocument(),
                onCorrupt: null);
            return string.IsNullOrWhiteSpace(doc.DataRoot) ? null : doc.DataRoot;
        }
        catch
        {
            return null;
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private sealed class BootstrapDocument
    {
        public string? DataRoot { get; set; }
    }
}
