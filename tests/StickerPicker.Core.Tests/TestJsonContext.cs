using System.Text.Json;
using System.Text.Json.Serialization;

namespace StickerPicker.Core.Tests;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SampleDocument))]
internal sealed partial class TestJsonContext : JsonSerializerContext;

internal sealed class SampleDocument
{
    public int Version { get; set; } = 1;
    public string Name { get; set; } = "default";
    public string? Optional { get; set; }
}
