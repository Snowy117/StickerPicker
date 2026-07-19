using System.Text;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Library;

internal static class LibraryPathRules
{
    public static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
    };

    private static readonly HashSet<string> ReservedCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".", "..", "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static bool IsSupportedImage(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path));

    public static string NormalizeCategoryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("分类名不能为空。", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed.Contains('/')
            || trimmed.Contains('\\'))
        {
            throw new ArgumentException("分类名包含非法字符。", nameof(name));
        }

        if (ReservedCategoryNames.Contains(trimmed)
            || string.Equals(trimmed, Category.AllId, StringComparison.Ordinal))
        {
            throw new ArgumentException("分类名被保留。", nameof(name));
        }

        return trimmed;
    }

    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return "sticker.png";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
        }

        return sb.ToString();
    }

    public static string NormalizeRelativeKey(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    public static string ToRelativePath(string dataRoot, string absolutePath)
    {
        var relative = Path.GetRelativePath(dataRoot, absolutePath);
        return NormalizeRelativeKey(relative);
    }

    public static string ToAbsolutePath(string dataRoot, string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Path.GetFullPath(Path.Combine([dataRoot, .. parts]));
    }

    public static string AllocateUniqueFilePath(string categoryDirectory, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        var dest = Path.Combine(categoryDirectory, safeName);
        if (!File.Exists(dest))
        {
            return dest;
        }

        var name = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(categoryDirectory, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(categoryDirectory, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    public static IEnumerable<string> ExpandSourceFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (IsSupportedImage(file))
                    {
                        yield return file;
                    }
                }
            }
            else if (File.Exists(path) && IsSupportedImage(path))
            {
                yield return path;
            }
        }
    }

    public static bool MatchesSearch(Sticker sticker, string term)
    {
        if (sticker.FileName.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (sticker.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return sticker.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<Sticker> Filter(
        IEnumerable<Sticker> stickers,
        string? categoryId,
        string? searchText)
    {
        IEnumerable<Sticker> query = stickers;

        if (!string.IsNullOrWhiteSpace(categoryId)
            && !string.Equals(categoryId, Category.AllId, StringComparison.Ordinal))
        {
            query = query.Where(s =>
                string.Equals(s.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var terms = searchText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var term in terms)
            {
                query = query.Where(s => MatchesSearch(s, term));
            }
        }

        return [.. query];
    }
}
