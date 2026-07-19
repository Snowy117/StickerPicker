using StickerPicker.Core.Library;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Tests;

public sealed class LibraryPathRulesTests
{
    [Theory]
    [InlineData("photo.PNG", true)]
    [InlineData("a.jpg", true)]
    [InlineData("a.jpeg", true)]
    [InlineData("a.gif", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.txt", false)]
    [InlineData("a.webm", false)]
    public void IsSupportedImage_MatchesExtensions(string name, bool expected)
    {
        Assert.Equal(expected, LibraryPathRules.IsSupportedImage(name));
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidChars()
    {
        var invalid = Path.GetInvalidFileNameChars().First(c => c != '\0');
        var raw = $"a{invalid}b.png";
        var sanitized = LibraryPathRules.SanitizeFileName(raw);
        Assert.DoesNotContain(invalid, sanitized);
        Assert.EndsWith(".png", sanitized);
    }

    [Fact]
    public void NormalizeRelativeKey_UsesForwardSlashes()
    {
        Assert.Equal("library/a/b.png", LibraryPathRules.NormalizeRelativeKey(@"library\a\b.png"));
        Assert.Equal("library/a/b.png", LibraryPathRules.NormalizeRelativeKey("/library/a/b.png"));
    }

    [Fact]
    public void Filter_AndTerms_RequireAllMatches()
    {
        var stickers = new[]
        {
            new Sticker
            {
                RelativePath = "library/c/a.png",
                AbsolutePath = "/tmp/a.png",
                CategoryId = "c",
                FileName = "happy-cat.png",
                Tags = ["cute"],
            },
            new Sticker
            {
                RelativePath = "library/c/b.png",
                AbsolutePath = "/tmp/b.png",
                CategoryId = "c",
                FileName = "sad-dog.png",
                Tags = ["cute"],
            },
        };

        var hits = LibraryPathRules.Filter(stickers, Category.AllId, "happy cute");
        Assert.Single(hits);
        Assert.Equal("happy-cat.png", hits[0].FileName);
    }

    [Fact]
    public void ExpandSourceFiles_SkipsUnsupported()
    {
        using var temp = new TempDirectory();
        var dir = Path.Combine(temp.Path, "mix");
        Directory.CreateDirectory(dir);
        File.WriteAllBytes(Path.Combine(dir, "a.png"), LibraryFixture.MinimalPngBytes(1));
        File.WriteAllText(Path.Combine(dir, "b.txt"), "x");

        var expanded = LibraryPathRules.ExpandSourceFiles([dir]).ToList();
        Assert.Single(expanded);
        Assert.EndsWith("a.png", expanded[0]);
    }

    [Fact]
    public void AllocateUniqueFilePath_SuffixesOnCollision()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "x.png"), "1");
        var second = LibraryPathRules.AllocateUniqueFilePath(temp.Path, "x.png");
        Assert.Equal(Path.Combine(temp.Path, "x_1.png"), second);
    }
}
