using System.Text.Json;
using StickerPicker.Core.Json;

namespace StickerPicker.Core.Tests;

public sealed class AtomicJsonTests
{
    private sealed class SampleDoc
    {
        public int Version { get; set; } = 1;
        public string Name { get; set; } = "default";
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "doc.json");

        AtomicJson.Save(path, new SampleDoc { Version = 2, Name = "alpha" });
        var loaded = AtomicJson.LoadOrCreate(path, () => new SampleDoc());

        Assert.Equal(2, loaded.Version);
        Assert.Equal("alpha", loaded.Name);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void LoadOrCreate_Missing_CreatesWithFactory()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "nested", "doc.json");

        var loaded = AtomicJson.LoadOrCreate(path, () => new SampleDoc { Name = "fresh" });

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
            () => new SampleDoc { Name = "recovered" },
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
        AtomicJson.Save(path, new SampleDoc { Name = "one" });
        AtomicJson.Save(path, new SampleDoc { Name = "two" });

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("two", doc.RootElement.GetProperty("name").GetString());
    }
}
