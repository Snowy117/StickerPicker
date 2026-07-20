using System.Text.Json;
using StickerPicker.Core.Json;
using StickerPicker.Core.Library;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;

namespace StickerPicker.Core.Tests;

public sealed class CoreJsonContextTests
{
    [Fact]
    public void PersistedDocumentRoots_RoundTripWithGeneratedMetadata()
    {
        using var temp = new TempDirectory();
        var appConfig = RoundTrip(
            temp.Path,
            "config.json",
            new AppConfig
            {
                Theme = "dark",
                DataRoot = null,
                Window = new WindowGeometry { X = 10, Y = 20, Width = 800, Height = 600 },
            },
            CoreJsonContext.Default.AppConfig);
        var metadata = RoundTrip(
            temp.Path,
            "metadata.json",
            new MetadataDocument
            {
                Stickers =
                {
                    ["library/cats/neko.png"] = new StickerMetadataEntry
                    {
                        RelativePath = "library/cats/neko.png",
                        Tags = ["cat"],
                        CreatedAt = DateTimeOffset.UnixEpoch,
                        Hash = "abc",
                    },
                },
            },
            CoreJsonContext.Default.MetadataDocument);
        var hashes = RoundTrip(
            temp.Path,
            "hashes.json",
            new HashesDocument { Hashes = { ["abc"] = "library/cats/neko.png" } },
            CoreJsonContext.Default.HashesDocument);
        var bootstrap = RoundTrip(
            temp.Path,
            "bootstrap.json",
            new BootstrapDocument { DataRoot = "/portable" },
            CoreJsonContext.Default.BootstrapDocument);

        Assert.Equal("dark", appConfig.Theme);
        Assert.Equal(800, appConfig.Window.Width);
        Assert.Equal("cat", metadata.Stickers["library/cats/neko.png"].Tags.Single());
        Assert.Equal("library/cats/neko.png", hashes.Hashes["abc"]);
        Assert.Equal("/portable", bootstrap.DataRoot);

        var configJson = File.ReadAllText(Path.Combine(temp.Path, "config.json"));
        Assert.Contains("\"alwaysOnTop\"", configJson, StringComparison.Ordinal);
        Assert.Contains("\"window\"", configJson, StringComparison.Ordinal);
        Assert.DoesNotContain("dataRoot", configJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistedDocumentRoots_RecoverCorruptionWithGeneratedMetadata()
    {
        using var temp = new TempDirectory();

        AssertRecoversCorruption(
            temp.Path,
            "config.json",
            () => new AppConfig { Theme = "system" },
            CoreJsonContext.Default.AppConfig);
        AssertRecoversCorruption(
            temp.Path,
            "metadata.json",
            () => new MetadataDocument(),
            CoreJsonContext.Default.MetadataDocument);
        AssertRecoversCorruption(
            temp.Path,
            "hashes.json",
            () => new HashesDocument(),
            CoreJsonContext.Default.HashesDocument);
        AssertRecoversCorruption(
            temp.Path,
            "bootstrap.json",
            () => new BootstrapDocument(),
            CoreJsonContext.Default.BootstrapDocument);
    }

    [Fact]
    public void ReflectionSerialization_IsDisabledForOrdinaryTests()
    {
        Assert.False(JsonSerializer.IsReflectionEnabledByDefault);
    }

    private static void AssertRecoversCorruption<T>(
        string directory,
        string fileName,
        Func<T> factory,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "{ invalid json");

        var recovered = AtomicJson.LoadOrCreate(path, factory, typeInfo);

        Assert.NotNull(recovered);
        var backup = Assert.Single(Directory.GetFiles(directory, fileName + ".corrupt-*"));
        Assert.Equal("{ invalid json", File.ReadAllText(backup));
        Assert.NotEqual("{ invalid json", File.ReadAllText(path));
    }

    private static T RoundTrip<T>(
        string directory,
        string fileName,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var path = Path.Combine(directory, fileName);
        AtomicJson.Save(path, value, typeInfo);
        return AtomicJson.LoadOrCreate(path, () => throw new InvalidOperationException(), typeInfo);
    }
}
