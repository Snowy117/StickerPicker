using StickerPicker.Core.Models;

namespace StickerPicker.Core.Abstractions;

/// <summary>
/// Deep module: folder scan, hashes, metadata, and import are hidden inside the implementation.
/// Categories are filesystem folders under library/.
/// </summary>
public interface IStickerLibrary
{
    IReadOnlyList<Category> Categories { get; }
    IReadOnlyList<Sticker> Stickers { get; }

    /// <summary>Rescan library folders and reconcile metadata/hashes.</summary>
    void Refresh();

    IReadOnlyList<Sticker> Query(string? categoryId, string? searchText);

    Category CreateCategory(string name);
    void RenameCategory(string categoryId, string newName);
    /// <summary>
    /// Deletes a category folder. When deleteFiles is false and the folder is non-empty, throws.
    /// </summary>
    void DeleteCategory(string categoryId, bool deleteFiles);

    Task<ImportResult> ImportAsync(
        IEnumerable<string> paths,
        string? targetCategoryId,
        CancellationToken cancellationToken = default);

    void MoveSticker(string relativePath, string targetCategoryId);

    void DeleteSticker(string relativePath);

    void SetTags(string relativePath, IReadOnlyList<string> tags);
}
