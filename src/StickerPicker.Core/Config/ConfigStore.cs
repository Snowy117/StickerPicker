using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Json;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Config;

public sealed class ConfigStore(IAppPaths paths) : IConfigStore
{
    private readonly IAppPaths _paths = paths;

    public AppConfig Load()
    {
        _paths.EnsureDataLayout();
        var loaded = AtomicJson.LoadOrCreate(
            _paths.ConfigPath,
            () => new AppConfig(),
            CoreJsonContext.Default.AppConfig,
            onCorrupt: null);
        var normalized = MergeDefaults(loaded, out var changed);
        if (changed)
        {
            AtomicJson.Save(_paths.ConfigPath, normalized, CoreJsonContext.Default.AppConfig);
        }

        return normalized;
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _paths.EnsureDataLayout();
        var toSave = MergeDefaults(config.Clone(), out _);
        AtomicJson.Save(_paths.ConfigPath, toSave, CoreJsonContext.Default.AppConfig);
    }

    private static AppConfig MergeDefaults(AppConfig config, out bool changed)
    {
        changed = false;
        var defaults = new AppConfig();
        if (config.Version <= 0)
        {
            config.Version = defaults.Version;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(config.Theme))
        {
            config.Theme = defaults.Theme;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(config.Hotkey))
        {
            config.Hotkey = defaults.Hotkey;
            changed = true;
        }

        if (config.ThumbnailSize <= 0)
        {
            config.ThumbnailSize = defaults.ThumbnailSize;
            changed = true;
        }

        if (config.Window is null)
        {
            config.Window = new WindowGeometry();
            changed = true;
        }
        if (config.Window.Width <= 0)
        {
            config.Window.Width = defaults.Window.Width;
            changed = true;
        }

        if (config.Window.Height <= 0)
        {
            config.Window.Height = defaults.Window.Height;
            changed = true;
        }

        var restoreDelay = Math.Clamp(config.ClipboardRestoreDelaySeconds, 0, 60);
        if (restoreDelay != config.ClipboardRestoreDelaySeconds)
        {
            config.ClipboardRestoreDelaySeconds = restoreDelay;
            changed = true;
        }

        if (config.AutoPaste && config.KeepWindowOpenAfterSelection)
        {
            config.AutoPaste = false;
            changed = true;
        }

        return config;
    }
}
