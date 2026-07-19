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
            onCorrupt: null);
        return MergeDefaults(loaded);
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _paths.EnsureDataLayout();
        var toSave = MergeDefaults(config.Clone());
        AtomicJson.Save(_paths.ConfigPath, toSave);
    }

    private static AppConfig MergeDefaults(AppConfig config)
    {
        var defaults = new AppConfig();
        if (config.Version <= 0)
        {
            config.Version = defaults.Version;
        }

        if (string.IsNullOrWhiteSpace(config.Theme))
        {
            config.Theme = defaults.Theme;
        }

        if (string.IsNullOrWhiteSpace(config.Hotkey))
        {
            config.Hotkey = defaults.Hotkey;
        }

        if (config.ThumbnailSize <= 0)
        {
            config.ThumbnailSize = defaults.ThumbnailSize;
        }

        config.Window ??= new WindowGeometry();
        if (config.Window.Width <= 0)
        {
            config.Window.Width = defaults.Window.Width;
        }

        if (config.Window.Height <= 0)
        {
            config.Window.Height = defaults.Window.Height;
        }

        return config;
    }
}
