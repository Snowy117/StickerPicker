namespace StickerPicker.Core.Abstractions;

/// <summary>
/// Resolves the active data root and the fixed bootstrap pointer location.
/// </summary>
public interface IAppPaths
{
    /// <summary>Fixed LocalAppData/StickerPicker location that may hold bootstrap.json.</summary>
    string DefaultAppFolder { get; }

    /// <summary>Path to bootstrap.json under DefaultAppFolder.</summary>
    string BootstrapPath { get; }

    /// <summary>Currently resolved data root (library, config, metadata live here).</summary>
    string DataRoot { get; }

    string LibraryRoot { get; }
    string ConfigPath { get; }
    string MetadataPath { get; }
    string HashesPath { get; }

    /// <summary>
    /// Re-read bootstrap.json and recompute DataRoot.
    /// Returns the resolved data root after ensuring it exists.
    /// </summary>
    string Resolve();

    /// <summary>
    /// Point bootstrap at a custom data root (or clear it when null/default).
    /// Does not move existing data.
    /// </summary>
    void SetDataRoot(string? customDataRoot);

    /// <summary>Ensure data root and library/ exist.</summary>
    void EnsureDataLayout();
}
