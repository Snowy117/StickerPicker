using StickerPicker.Core.Library;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;

namespace StickerPicker.Core.Tests;

public sealed class FolderStickerLibraryTests
{
    [Fact]
    public void Refresh_EmptyLibrary_HasVirtualAllOnly()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();

        Assert.Single(fixture.Library.Categories);
        Assert.Equal(Category.AllId, fixture.Library.Categories[0].Id);
        Assert.Empty(fixture.Library.Stickers);
    }

    [Fact]
    public void CreateCategory_CreatesFolder()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();

        var category = fixture.Library.CreateCategory("cats");

        Assert.Equal("cats", category.Id);
        Assert.True(Directory.Exists(Path.Combine(fixture.Paths.LibraryRoot, "cats")));
        Assert.Contains(fixture.Library.Categories, c => c.Id == "cats");
    }

    [Fact]
    public async Task ImportAsync_CopiesFile_AndDedupes()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("memes");

        var source = fixture.WritePng("source.png");
        var first = await fixture.Library.ImportAsync([source], "memes");
        var second = await fixture.Library.ImportAsync([source], "memes");

        Assert.Equal(1, first.Imported);
        Assert.Equal(0, first.Duplicates);
        Assert.Equal(0, second.Imported);
        Assert.Equal(1, second.Duplicates);
        Assert.Single(fixture.Library.Stickers);
        Assert.True(File.Exists(fixture.Library.Stickers[0].AbsolutePath));
    }

    [Fact]
    public async Task ImportAsync_WhenAllSelected_UsesInbox()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();

        var source = fixture.WritePng("a.png");
        var result = await fixture.Library.ImportAsync([source], Category.AllId);

        Assert.Equal(1, result.Imported);
        Assert.True(Directory.Exists(Path.Combine(fixture.Paths.LibraryRoot, Category.InboxName)));
        Assert.Equal(Category.InboxName, fixture.Library.Stickers[0].CategoryId);
    }

    [Fact]
    public async Task Query_FiltersByCategoryAndSearch()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("cats");
        fixture.Library.CreateCategory("dogs");

        await fixture.Library.ImportAsync([fixture.WritePng("neko.png")], "cats");
        await fixture.Library.ImportAsync([fixture.WritePng("inu.png")], "dogs");

        var sticker = fixture.Library.Stickers.First(s => s.FileName == "neko.png");
        fixture.Library.SetTags(sticker.RelativePath, ["happy", "猫"]);

        Assert.Single(fixture.Library.Query("cats", null));
        Assert.Single(fixture.Library.Query(Category.AllId, "neko"));
        Assert.Single(fixture.Library.Query(Category.AllId, "猫"));
        Assert.Empty(fixture.Library.Query("dogs", "neko"));
    }

    [Fact]
    public async Task ExplorerReorganization_IsPickedUpByRefresh()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("a");
        await fixture.Library.ImportAsync([fixture.WritePng("x.png")], "a");

        var newDir = Path.Combine(fixture.Paths.LibraryRoot, "b");
        Directory.CreateDirectory(newDir);
        var sticker = fixture.Library.Stickers.Single();
        File.Move(sticker.AbsolutePath, Path.Combine(newDir, "x.png"));
        Directory.Delete(Path.Combine(fixture.Paths.LibraryRoot, "a"));

        fixture.Library.Refresh();

        Assert.Contains(fixture.Library.Categories, c => c.Id == "b");
        Assert.DoesNotContain(fixture.Library.Categories, c => c.Id == "a");
        Assert.Equal("b", fixture.Library.Stickers.Single().CategoryId);
    }

    [Fact]
    public async Task DeleteCategory_NonEmptyWithoutFlag_Throws()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("keep");
        var source = fixture.WritePng("z.png");
        await fixture.Library.ImportAsync([source], "keep");

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Library.DeleteCategory("keep", deleteFiles: false));
    }

    [Fact]
    public async Task MoveSticker_ChangesCategoryFolder()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("from");
        fixture.Library.CreateCategory("to");
        await fixture.Library.ImportAsync([fixture.WritePng("m.png")], "from");
        var sticker = fixture.Library.Stickers.Single();

        fixture.Library.MoveSticker(sticker.RelativePath, "to");

        Assert.Equal("to", fixture.Library.Stickers.Single().CategoryId);
        Assert.True(File.Exists(fixture.Library.Stickers.Single().AbsolutePath));
    }

    [Fact]
    public void Load_Recovers_FromCorruptMetadata()
    {
        using var fixture = new LibraryFixture();
        fixture.Paths.EnsureDataLayout();
        File.WriteAllText(fixture.Paths.MetadataPath, "!!!");
        File.WriteAllText(fixture.Paths.HashesPath, "!!!");

        fixture.Library.Refresh();

        Assert.Empty(fixture.Library.Stickers);
        Assert.True(File.Exists(fixture.Paths.MetadataPath));
    }
}

internal sealed class LibraryFixture : IDisposable
{
    private readonly TempDirectory _temp = new();

    public LibraryFixture()
    {
        Paths = new AppPaths(_temp.Path);
        Library = new FolderStickerLibrary(Paths);
    }

    public AppPaths Paths { get; }
    public FolderStickerLibrary Library { get; }

    public string WritePng(string fileName)
    {
        var path = Path.Combine(_temp.Path, "incoming");
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, fileName);
        File.WriteAllBytes(file,
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D, 0xB0, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82,
        ]);
        return file;
    }

    public void Dispose() => _temp.Dispose();
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StickerPickerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
