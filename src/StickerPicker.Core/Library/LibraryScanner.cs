using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Library;

internal sealed class LibraryScanner(IAppPaths paths, LibraryIndexStore index)
{
    private readonly IAppPaths _paths = paths;
    private readonly LibraryIndexStore _index = index;

    public (List<Category> Categories, List<Sticker> Stickers) Scan()
    {
        _paths.EnsureDataLayout();
        _index.Load();

        var stickers = new List<Sticker>();
        var categoryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in Directory.EnumerateDirectories(_paths.LibraryRoot))
        {
            var categoryName = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                continue;
            }

            categoryCounts[categoryName] = 0;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (!LibraryPathRules.IsSupportedImage(file))
                {
                    continue;
                }

                var relative = LibraryPathRules.ToRelativePath(_paths.DataRoot, file);
                var entry = _index.GetOrCreateMetadata(relative, file);
                stickers.Add(ToSticker(relative, file, categoryName, entry));
                categoryCounts[categoryName]++;
            }
        }

        _index.PruneMissingMetadata(stickers.Select(s => s.RelativePath));
        _index.RebuildHashesFromMetadata();
        _index.Save();

        var orderedStickers = stickers
            .OrderBy(s => s.CategoryId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categories = new List<Category> { Category.All(orderedStickers.Count) };
        foreach (var name in categoryCounts.Keys.Order(StringComparer.OrdinalIgnoreCase))
        {
            categories.Add(Category.FromFolder(name, categoryCounts[name]));
        }

        return (categories, orderedStickers);
    }

    private static Sticker ToSticker(
        string relative,
        string absolute,
        string categoryName,
        StickerMetadataEntry entry) =>
        new()
        {
            RelativePath = relative,
            AbsolutePath = absolute,
            CategoryId = categoryName,
            FileName = Path.GetFileName(absolute),
            Tags = [.. entry.Tags],
            Hash = entry.Hash,
            CreatedAt = entry.CreatedAt,
        };
}
