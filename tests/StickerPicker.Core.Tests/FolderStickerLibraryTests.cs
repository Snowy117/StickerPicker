using StickerPicker.Core.Models;

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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad/name")]
    [InlineData("bad\\name")]
    [InlineData("__all__")]
    [InlineData("CON")]
    public void CreateCategory_InvalidNames_Throw(string name)
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        Assert.ThrowsAny<ArgumentException>(() => fixture.Library.CreateCategory(name));
    }

    [Fact]
    public void CreateCategory_Duplicate_Throws()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("dup");
        Assert.Throws<InvalidOperationException>(() => fixture.Library.CreateCategory("dup"));
    }

    [Fact]
    public async Task RenameCategory_UpdatesFolderAndMetadataKeys()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("old");
        var source = fixture.WritePng("a.png");
        await fixture.Library.ImportAsync([source], "old");

        fixture.Library.RenameCategory("old", "new");

        Assert.DoesNotContain(fixture.Library.Categories, c => c.Id == "old");
        Assert.Contains(fixture.Library.Categories, c => c.Id == "new");
        var sticker = fixture.Library.Stickers.Single();
        Assert.Equal("new", sticker.CategoryId);
        Assert.StartsWith("library/new/", sticker.RelativePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sticker.AbsolutePath));
    }

    [Fact]
    public void RenameCategory_Collision_Throws()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("a");
        fixture.Library.CreateCategory("b");
        Assert.Throws<InvalidOperationException>(() => fixture.Library.RenameCategory("a", "b"));
    }

    [Fact]
    public void RenameCategory_VirtualAll_Throws()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Library.RenameCategory(Category.AllId, "x"));
    }

    [Fact]
    public async Task DeleteCategory_NonEmptyWithoutFlag_Throws()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("keep");
        await fixture.Library.ImportAsync([fixture.WritePng("z.png")], "keep");

        Assert.Throws<InvalidOperationException>(() =>
            fixture.Library.DeleteCategory("keep", deleteFiles: false));
    }

    [Fact]
    public async Task DeleteCategory_WithDeleteFiles_RemovesStickers()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("gone");
        await fixture.Library.ImportAsync([fixture.WritePng("z.png")], "gone");

        fixture.Library.DeleteCategory("gone", deleteFiles: true);

        Assert.DoesNotContain(fixture.Library.Categories, c => c.Id == "gone");
        Assert.Empty(fixture.Library.Stickers);
        Assert.False(Directory.Exists(Path.Combine(fixture.Paths.LibraryRoot, "gone")));
    }

    [Fact]
    public void DeleteCategory_Empty_SucceedsWithoutFlag()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("empty");

        fixture.Library.DeleteCategory("empty", deleteFiles: false);

        Assert.DoesNotContain(fixture.Library.Categories, c => c.Id == "empty");
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
    public async Task ImportAsync_MultiFileBatch_ImportsAllUnique()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("batch");

        var files = new[]
        {
            fixture.WritePng("one.png", 1),
            fixture.WritePng("two.png", 2),
            fixture.WritePng("three.png", 3),
        };

        var result = await fixture.Library.ImportAsync(files, "batch");

        Assert.Equal(3, result.Imported);
        Assert.Equal(0, result.Duplicates);
        Assert.Equal(3, fixture.Library.Stickers.Count);
    }

    [Fact]
    public async Task ImportAsync_FolderFlatten_IgnoresNestedCategoryStructure()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("flat");

        var folder = Path.Combine(fixture.Root, "tree");
        var nested = Path.Combine(folder, "sub");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(folder, "root.png"), LibraryFixture.MinimalPngBytes(10));
        File.WriteAllBytes(Path.Combine(nested, "nested.png"), LibraryFixture.MinimalPngBytes(11));
        File.WriteAllText(Path.Combine(nested, "notes.txt"), "skip");

        var result = await fixture.Library.ImportAsync([folder], "flat");

        Assert.Equal(2, result.Imported);
        Assert.All(fixture.Library.Stickers, s => Assert.Equal("flat", s.CategoryId));
        Assert.False(Directory.Exists(Path.Combine(fixture.Paths.LibraryRoot, "flat", "sub")));
    }

    [Fact]
    public async Task ImportAsync_UnsupportedExtension_IsSkipped()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("ok");

        var txt = fixture.WriteText("x.txt");
        var png = fixture.WritePng("y.png", 7);
        var result = await fixture.Library.ImportAsync([txt, png], "ok");

        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.Single(fixture.Library.Stickers);
    }

    [Fact]
    public async Task ImportAsync_FilenameCollision_AddsSuffix()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("col");

        var first = fixture.WritePng("same.png", 1);
        var altDir = Path.Combine(fixture.Root, "incoming2");
        Directory.CreateDirectory(altDir);
        var secondPath = Path.Combine(altDir, "same.png");
        File.WriteAllBytes(secondPath, LibraryFixture.MinimalPngBytes(99));

        await fixture.Library.ImportAsync([first], "col");
        await fixture.Library.ImportAsync([secondPath], "col");

        var names = fixture.Library.Stickers.Select(s => s.FileName).OrderBy(n => n).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("same.png", names);
        Assert.Contains(names, n => n.StartsWith("same_", StringComparison.Ordinal) && n.EndsWith(".png"));
    }

    [Fact]
    public async Task Query_FiltersByCategoryAndSearch()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("cats");
        fixture.Library.CreateCategory("dogs");

        await fixture.Library.ImportAsync([fixture.WritePng("neko.png", 1)], "cats");
        await fixture.Library.ImportAsync([fixture.WritePng("inu.png", 2)], "dogs");

        var sticker = fixture.Library.Stickers.First(s => s.FileName == "neko.png");
        fixture.Library.SetTags(sticker.RelativePath, ["happy", "猫"]);

        Assert.Single(fixture.Library.Query("cats", null));
        Assert.Single(fixture.Library.Query(Category.AllId, "neko"));
        Assert.Single(fixture.Library.Query(Category.AllId, "猫"));
        Assert.Empty(fixture.Library.Query("dogs", "neko"));
    }

    [Fact]
    public async Task Query_MultiKeyword_UsesAndSemantics()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("tags");
        await fixture.Library.ImportAsync([fixture.WritePng("alpha.png", 1)], "tags");
        await fixture.Library.ImportAsync([fixture.WritePng("beta.png", 2)], "tags");
        var alpha = fixture.Library.Stickers.First(s => s.FileName == "alpha.png");
        fixture.Library.SetTags(alpha.RelativePath, ["happy", "cat"]);

        Assert.Single(fixture.Library.Query(Category.AllId, "happy cat"));
        Assert.Empty(fixture.Library.Query(Category.AllId, "happy dog"));
        Assert.Empty(fixture.Library.Query(Category.AllId, "beta happy"));
    }

    [Fact]
    public async Task MoveSticker_ChangesCategoryFolder_AndHashPath()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        fixture.Library.CreateCategory("from");
        fixture.Library.CreateCategory("to");
        await fixture.Library.ImportAsync([fixture.WritePng("m.png")], "from");
        var sticker = fixture.Library.Stickers.Single();
        var hash = sticker.Hash;
        Assert.False(string.IsNullOrEmpty(hash));

        fixture.Library.MoveSticker(sticker.RelativePath, "to");

        var moved = fixture.Library.Stickers.Single();
        Assert.Equal("to", moved.CategoryId);
        Assert.Equal(hash, moved.Hash);
        Assert.StartsWith("library/to/", moved.RelativePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(moved.AbsolutePath));
        Assert.False(File.Exists(sticker.AbsolutePath));
    }

    [Fact]
    public void MoveSticker_ToVirtualAll_Throws()
    {
        using var fixture = new LibraryFixture();
        fixture.Library.Refresh();
        Assert.Throws<InvalidOperationException>(() =>
            fixture.Library.MoveSticker("library/x/a.png", Category.AllId));
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
}
