using StickerPicker.Core.Config;
using StickerPicker.Core.Library;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;

namespace StickerPicker.Core.Tests;

public sealed class LibraryIntegrationTests
{
    [Fact]
    public async Task EndToEnd_CreateImportExplorerMoveRefresh_MatchesFilesystem()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();

        fixture.Library.CreateCategory("cats");
        fixture.Library.CreateCategory("dogs");
        await fixture.Library.ImportAsync(
            [fixture.WritePng("neko.png", 1), fixture.WritePng("doggo.png", 2)],
            "cats");

        Assert.Equal(2, fixture.Library.Query("cats", null).Count);

        var catsDir = Path.Combine(fixture.Paths.LibraryRoot, "cats");
        var petsDir = Path.Combine(fixture.Paths.LibraryRoot, "pets");
        Directory.Move(catsDir, petsDir);
        var dogsDir = Path.Combine(fixture.Paths.LibraryRoot, "dogs");
        var doggo = Directory.GetFiles(petsDir, "doggo.png").Single();
        File.Move(doggo, Path.Combine(dogsDir, "doggo.png"));

        fixture.Library.Refresh();

        Assert.Contains(fixture.Library.Categories, c => c.Id == "pets");
        Assert.DoesNotContain(fixture.Library.Categories, c => c.Id == "cats");
        Assert.Single(fixture.Library.Query("pets", null));
        Assert.Single(fixture.Library.Query("dogs", null));
        Assert.Equal(2, fixture.Library.Stickers.Count);
        Assert.All(fixture.Library.Stickers, s => Assert.True(File.Exists(s.AbsolutePath)));
    }

    [Fact]
    public async Task SwitchDataRoot_ViaBootstrap_ReloadsLibraryView()
    {
        using var temp = new TempDirectory();
        var defaultRoot = Path.Combine(temp.Path, "default");
        var portableRoot = Path.Combine(temp.Path, "portable");
        Directory.CreateDirectory(defaultRoot);

        var paths = new AppPaths(defaultRoot);
        var library = new FolderStickerLibrary(paths);
        library.Refresh();
        library.CreateCategory("alpha");
        await library.ImportAsync(
            [WritePng(temp.Path, "a.png", 1)],
            "alpha");
        Assert.Single(library.Stickers);

        paths.SetDataRoot(portableRoot);
        library.Refresh();
        Assert.Empty(library.Stickers);

        library.CreateCategory("beta");
        await library.ImportAsync(
            [WritePng(temp.Path, "b.png", 2)],
            "beta");
        Assert.Single(library.Stickers);
        Assert.Equal("beta", library.Stickers[0].CategoryId);
        Assert.StartsWith(Path.GetFullPath(portableRoot), library.Stickers[0].AbsolutePath);

        var reloadedPaths = new AppPaths(defaultRoot);
        var reloadedLibrary = new FolderStickerLibrary(reloadedPaths);
        reloadedLibrary.Refresh();
        Assert.Equal(Path.GetFullPath(portableRoot), reloadedPaths.DataRoot);
        Assert.Single(reloadedLibrary.Stickers);
        Assert.Equal("beta", reloadedLibrary.Stickers[0].CategoryId);
    }

    [Fact]
    public async Task SequentialImports_ThenRefresh_StayConsistent()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("seq");

        for (var i = 0; i < 5; i++)
        {
            var result = await fixture.Library.ImportAsync(
                [fixture.WritePng($"n{i}.png", (byte)(i + 1))],
                "seq");
            Assert.Equal(1, result.Imported);
        }

        for (var i = 0; i < 5; i++)
        {
            var path = fixture.WritePng($"dup{i}.png", (byte)(i + 1));
            var result = await fixture.Library.ImportAsync([path], "seq");
            Assert.Equal(1, result.Duplicates);
        }

        fixture.Library.Refresh();
        Assert.Equal(5, fixture.Library.Stickers.Count);
        Assert.Equal(5, fixture.Library.Categories.First(c => c.Id == "seq").StickerCount);
        Assert.Equal(5, fixture.Library.Query("seq", null).Count);
    }

    [Fact]
    public async Task ConfigAndLibrary_ShareDataRoot_AfterBootstrapSwitch()
    {
        using var temp = new TempDirectory();
        var defaultRoot = Path.Combine(temp.Path, "app");
        var customRoot = Path.Combine(temp.Path, "data");
        Directory.CreateDirectory(defaultRoot);

        var paths = new AppPaths(defaultRoot);
        var configStore = new ConfigStore(paths);
        var library = new FolderStickerLibrary(paths);

        var config = configStore.Load();
        config.Theme = "light";
        config.Hotkey = "Ctrl+Shift+Q";
        configStore.Save(config);

        paths.SetDataRoot(customRoot);
        var switched = configStore.Load();
        Assert.Equal("system", switched.Theme);

        switched.Theme = "dark";
        switched.Hotkey = "Ctrl+Shift+Z";
        configStore.Save(switched);

        library.Refresh();
        library.CreateCategory("inbox-ish");
        await library.ImportAsync([WritePng(temp.Path, "c.png", 3)], "inbox-ish");

        var again = new AppPaths(defaultRoot);
        var configAgain = new ConfigStore(again).Load();
        var libAgain = new FolderStickerLibrary(again);
        libAgain.Refresh();

        Assert.Equal(Path.GetFullPath(customRoot), again.DataRoot);
        Assert.Equal("dark", configAgain.Theme);
        Assert.Equal("Ctrl+Shift+Z", configAgain.Hotkey);
        Assert.Single(libAgain.Stickers);
    }

    [Fact]
    public async Task RenameThenSearch_ReflectsNewPaths()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("src");
        await fixture.Library.ImportAsync([fixture.WritePng("findme.png", 5)], "src");
        fixture.Library.RenameCategory("src", "dst");

        var hits = fixture.Library.Query(Category.AllId, "findme");
        Assert.Single(hits);
        Assert.Contains("library/dst/", hits[0].RelativePath, StringComparison.OrdinalIgnoreCase);
    }

    private static string WritePng(string root, string fileName, byte seed)
    {
        var dir = Path.Combine(root, "incoming");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, LibraryFixture.MinimalPngBytes(seed));
        return path;
    }
}
