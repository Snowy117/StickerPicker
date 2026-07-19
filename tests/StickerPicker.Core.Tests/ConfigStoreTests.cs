using StickerPicker.Core.Config;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;

namespace StickerPicker.Core.Tests;

public sealed class ConfigStoreTests
{
    [Fact]
    public void Load_CreatesDefaults_WhenMissing()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var store = new ConfigStore(paths);

        var config = store.Load();

        Assert.Equal("system", config.Theme);
        Assert.Equal("Ctrl+Shift+E", config.Hotkey);
        Assert.True(config.AlwaysOnTop);
        Assert.Equal(96, config.ThumbnailSize);
        Assert.True(File.Exists(paths.ConfigPath));
    }

    [Fact]
    public void Save_RoundTrips()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var store = new ConfigStore(paths);

        var config = store.Load();
        config.Theme = "dark";
        config.AlwaysOnTop = false;
        config.Hotkey = "Ctrl+Alt+S";
        config.ThumbnailSize = 128;
        store.Save(config);

        var reloaded = store.Load();
        Assert.Equal("dark", reloaded.Theme);
        Assert.False(reloaded.AlwaysOnTop);
        Assert.Equal("Ctrl+Alt+S", reloaded.Hotkey);
        Assert.Equal(128, reloaded.ThumbnailSize);
    }

    [Fact]
    public void Load_Recovers_FromCorruptJson()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        paths.EnsureDataLayout();
        File.WriteAllText(paths.ConfigPath, "{ not valid json");

        var store = new ConfigStore(paths);
        var config = store.Load();

        Assert.Equal("system", config.Theme);
        Assert.True(File.Exists(paths.ConfigPath));
    }
}
