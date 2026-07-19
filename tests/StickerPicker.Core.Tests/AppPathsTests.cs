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
}
