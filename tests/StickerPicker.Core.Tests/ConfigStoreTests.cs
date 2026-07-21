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
        Assert.False(config.AutoPaste);
        Assert.Equal(0, config.ClipboardRestoreDelaySeconds);
        Assert.False(config.KeepWindowOpenAfterSelection);
        Assert.True(File.Exists(paths.ConfigPath));
    }

    [Fact]
    public void Save_RoundTripsThemeAndHotkey()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var store = new ConfigStore(paths);

        var config = store.Load();
        config.Theme = "dark";
        config.AlwaysOnTop = false;
        config.Hotkey = "Ctrl+Alt+S";
        config.ThumbnailSize = 128;
        config.AutoPaste = true;
        config.ClipboardRestoreDelaySeconds = 15;
        config.KeepWindowOpenAfterSelection = false;
        store.Save(config);

        var reloaded = store.Load();
        Assert.Equal("dark", reloaded.Theme);
        Assert.False(reloaded.AlwaysOnTop);
        Assert.Equal("Ctrl+Alt+S", reloaded.Hotkey);
        Assert.Equal(128, reloaded.ThumbnailSize);
        Assert.True(reloaded.AutoPaste);
        Assert.Equal(15, reloaded.ClipboardRestoreDelaySeconds);
        Assert.False(reloaded.KeepWindowOpenAfterSelection);
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

    [Fact]
    public void Load_MergesMissingDefaults_FromPartialFile()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        paths.EnsureDataLayout();
        File.WriteAllText(paths.ConfigPath, """{"version":1,"theme":"","hotkey":"","thumbnailSize":0}""");

        var store = new ConfigStore(paths);
        var config = store.Load();

        Assert.Equal("system", config.Theme);
        Assert.Equal("Ctrl+Shift+E", config.Hotkey);
        Assert.Equal(96, config.ThumbnailSize);
        Assert.NotNull(config.Window);
        Assert.Equal(900, config.Window.Width);
        Assert.Equal(640, config.Window.Height);
    }

    [Fact]
    public void Save_PersistsWindowGeometry()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var store = new ConfigStore(paths);

        var config = store.Load();
        config.Window = new WindowGeometry { X = 10, Y = 20, Width = 800, Height = 600 };
        store.Save(config);

        var reloaded = store.Load();
        Assert.Equal(10, reloaded.Window.X);
        Assert.Equal(20, reloaded.Window.Y);
        Assert.Equal(800, reloaded.Window.Width);
        Assert.Equal(600, reloaded.Window.Height);
    }

    [Fact]
    public void Save_NullConfig_Throws()
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(new AppPaths(temp.Path));
        Assert.Throws<ArgumentNullException>(() => store.Save(null!));
    }
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(60, 60)]
    [InlineData(61, 60)]
    public void Save_ClampsClipboardRestoreDelay(int value, int expected)
    {
        using var temp = new TempDirectory();
        var store = new ConfigStore(new AppPaths(temp.Path));
        var config = new AppConfig { ClipboardRestoreDelaySeconds = value };

        store.Save(config);

        Assert.Equal(expected, store.Load().ClipboardRestoreDelaySeconds);
    }

    [Fact]
    public void Load_NormalizesConflictingSelectionOptions_AndPersistsResult()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        paths.EnsureDataLayout();
        File.WriteAllText(
            paths.ConfigPath,
            """{"autoPaste":true,"keepWindowOpenAfterSelection":true,"clipboardRestoreDelaySeconds":99}""");

        var store = new ConfigStore(paths);
        var config = store.Load();

        Assert.False(config.AutoPaste);
        Assert.True(config.KeepWindowOpenAfterSelection);
        Assert.Equal(60, config.ClipboardRestoreDelaySeconds);
        var persisted = File.ReadAllText(paths.ConfigPath);
        Assert.Contains("\"autoPaste\": false", persisted, StringComparison.Ordinal);
        Assert.Contains("\"clipboardRestoreDelaySeconds\": 60", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_OldJson_UsesNewFeatureDefaults()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        paths.EnsureDataLayout();
        File.WriteAllText(paths.ConfigPath, """{"version":1,"theme":"dark"}""");

        var config = new ConfigStore(paths).Load();

        Assert.False(config.AutoPaste);
        Assert.Equal(0, config.ClipboardRestoreDelaySeconds);
        Assert.False(config.KeepWindowOpenAfterSelection);
    }

    [Fact]
    public void Clone_PreservesSelectionSettings()
    {
        var config = new AppConfig
        {
            AutoPaste = false,
            ClipboardRestoreDelaySeconds = 23,
            KeepWindowOpenAfterSelection = true,
        };

        var clone = config.Clone();

        Assert.False(clone.AutoPaste);
        Assert.Equal(23, clone.ClipboardRestoreDelaySeconds);
        Assert.True(clone.KeepWindowOpenAfterSelection);
    }
}
