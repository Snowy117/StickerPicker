using System.Text.Json;
using System.Text.Json.Serialization;
using StickerPicker.Core.Library;
using StickerPicker.Core.Models;
using StickerPicker.Core.Paths;

namespace StickerPicker.Core.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(MetadataDocument))]
[JsonSerializable(typeof(HashesDocument))]
[JsonSerializable(typeof(BootstrapDocument))]
internal sealed partial class CoreJsonContext : JsonSerializerContext;
