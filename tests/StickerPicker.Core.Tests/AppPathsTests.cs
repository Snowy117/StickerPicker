using StickerPicker.Core.Paths;

namespace StickerPicker.Core.Tests;

public sealed class AppPathsTests
{
    [Fact]
    public void Resolve_UsesDefaultFolder_WhenNoBootstrap()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);

        var root = paths.Resolve();

        Assert.Equal(Path.GetFullPath(temp.Path), root);
        Assert.True(Directory.Exists(paths.LibraryRoot));
    }

    [Fact]
    public void SetDataRoot_WritesBootstrap_AndResolveFollowsIt()
    {
        using var temp = new TempDirectory();
        var custom = Path.Combine(temp.Path, "custom-root");
        var paths = new AppPaths(temp.Path);

        paths.SetDataRoot(custom);

        Assert.True(File.Exists(paths.BootstrapPath));
        Assert.Equal(Path.GetFullPath(custom), paths.DataRoot);
        Assert.True(Directory.Exists(paths.LibraryRoot));

        var reloaded = new AppPaths(temp.Path);
        Assert.Equal(Path.GetFullPath(custom), reloaded.DataRoot);
    }

    [Fact]
    public void SetDataRoot_Null_ClearsBootstrap()
    {
        using var temp = new TempDirectory();
        var custom = Path.Combine(temp.Path, "custom-root");
        var paths = new AppPaths(temp.Path);
        paths.SetDataRoot(custom);

        paths.SetDataRoot(null);

        Assert.False(File.Exists(paths.BootstrapPath));
        Assert.Equal(Path.GetFullPath(temp.Path), paths.DataRoot);
    }

    [Fact]
    public void SetDataRoot_DefaultFolder_ClearsBootstrap()
    {
        using var temp = new TempDirectory();
        var custom = Path.Combine(temp.Path, "custom-root");
        var paths = new AppPaths(temp.Path);
        paths.SetDataRoot(custom);

        paths.SetDataRoot(temp.Path);

        Assert.False(File.Exists(paths.BootstrapPath));
        Assert.Equal(Path.GetFullPath(temp.Path), paths.DataRoot);
    }

    [Fact]
    public void Bootstrap_RoundTrip_SurvivesNewInstance()
    {
        using var temp = new TempDirectory();
        var custom = Path.Combine(temp.Path, "portable");
        Directory.CreateDirectory(custom);

        var writer = new AppPaths(temp.Path);
        writer.SetDataRoot(custom);

        var reader = new AppPaths(temp.Path);
        Assert.Equal(Path.GetFullPath(custom), reader.Resolve());
        Assert.Equal(Path.Combine(Path.GetFullPath(custom), "library"), reader.LibraryRoot);
        Assert.Equal(Path.Combine(Path.GetFullPath(custom), "config.json"), reader.ConfigPath);
    }

    [Fact]
    public void EnsureDataLayout_CreatesLibraryFolder()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        Directory.Delete(paths.LibraryRoot, recursive: true);

        paths.EnsureDataLayout();

        Assert.True(Directory.Exists(paths.LibraryRoot));
    }
}
