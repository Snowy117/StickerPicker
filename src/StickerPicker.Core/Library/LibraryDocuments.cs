namespace StickerPicker.Core.Library;

internal sealed class MetadataDocument
{
    public int Version { get; set; } = 1;
    public Dictionary<string, StickerMetadataEntry> Stickers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class StickerMetadataEntry
{
    public string RelativePath { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public string? Hash { get; set; }
}

internal sealed class HashesDocument
{
    public int Version { get; set; } = 1;
    public Dictionary<string, string> Hashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
