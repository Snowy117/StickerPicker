using System.Text.Json;
using StickerPicker.Core.Json;

namespace StickerPicker.Core.Tests;

public sealed class AtomicJsonTests
{
    [Fact]
    public void Save_ThenLoad_RoundTripsGeneratedMetadataContract()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "doc.json");
        var typeInfo = TestJsonContext.Default.SampleDocument;

        AtomicJson.Save(
            path,
            new SampleDocument { Version = 2, Name = "alpha", Optional = null },
            typeInfo);
        File.AppendAllText(path, "\n// accepted comment\n");
        var loaded = AtomicJson.LoadOrCreate(path, () => new SampleDocument(), typeInfo);

        Assert.Equal(2, loaded.Version);
        Assert.Equal("alpha", loaded.Name);
        Assert.False(File.Exists(path + ".tmp"));

        var json = File.ReadAllText(path);
        Assert.Contains("\n  \"version\": 2", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"alpha\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Optional", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadOrCreate_Missing_CreatesWithFactory()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "nested", "doc.json");

        var loaded = AtomicJson.LoadOrCreate(
            path,
            () => new SampleDocument { Name = "fresh" },
            TestJsonContext.Default.SampleDocument);

        Assert.Equal("fresh", loaded.Name);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadOrCreate_Corrupt_BacksUpAndRecreates()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "broken.json");
        File.WriteAllText(path, "{ not json");

        string? corruptPath = null;
        Exception? corruptEx = null;
        var loaded = AtomicJson.LoadOrCreate(
            path,
            () => new SampleDocument { Name = "recovered" },
            TestJsonContext.Default.SampleDocument,
            onCorrupt: (p, ex) =>
            {
                corruptPath = p;
                corruptEx = ex;
            });

        Assert.Equal("recovered", loaded.Name);
        Assert.Equal(path, corruptPath);
        Assert.NotNull(corruptEx);
        Assert.True(File.Exists(path));
        var backups = Directory.GetFiles(temp.Path, "broken.json.corrupt-*");
        Assert.NotEmpty(backups);
    }

    [Fact]
    public void Save_OverwritesExisting_Atomically()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "doc.json");
        var typeInfo = TestJsonContext.Default.SampleDocument;
        AtomicJson.Save(path, new SampleDocument { Name = "one" }, typeInfo);
        AtomicJson.Save(path, new SampleDocument { Name = "two" }, typeInfo);

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("two", doc.RootElement.GetProperty("name").GetString());
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void LoadOrCreate_AcceptsCaseInsensitiveNamesAndTrailingCommas()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "permissive.json");
        File.WriteAllText(path, """{"VERSION":3,"NAME":"compatible",}""");

        var loaded = AtomicJson.LoadOrCreate(
            path,
            () => new SampleDocument(),
            TestJsonContext.Default.SampleDocument);

        Assert.Equal(3, loaded.Version);
        Assert.Equal("compatible", loaded.Name);
    }
}
