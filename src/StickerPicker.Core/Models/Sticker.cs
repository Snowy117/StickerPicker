namespace StickerPicker.Core.Models;

/// <summary>
/// A single sticker image backed by a file under library/{Category}/.
/// </summary>
public sealed class Sticker
{
    /// <summary>Path relative to the data root, using forward slashes (e.g. library/cats/a.png).</summary>
    public required string RelativePath { get; init; }

    /// <summary>Absolute path on disk.</summary>
    public required string AbsolutePath { get; init; }

    /// <summary>Owning category folder name (not the virtual All id).</summary>
    public required string CategoryId { get; init; }

    public required string FileName { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string? Hash { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
