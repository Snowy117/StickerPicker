using System.Security.Cryptography;
using System.Text;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Json;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Library;

public sealed class FolderStickerLibrary : IStickerLibrary
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp",
    };

    private static readonly HashSet<string> ReservedCategoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".", "..", "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private readonly IAppPaths _paths;
    private readonly object _gate = new();
    private List<Category> _categories = [];
    private List<Sticker> _stickers = [];
    private MetadataDocument _metadata = new();
    private HashesDocument _hashes = new();

    public FolderStickerLibrary(IAppPaths paths)
    {
        _paths = paths;
    }

    public IReadOnlyList<Category> Categories
    {
        get
        {
            lock (_gate)
            {
                return _categories;
            }
        }
    }

    public IReadOnlyList<Sticker> Stickers
    {
        get
        {
            lock (_gate)
            {
                return _stickers;
            }
        }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            RefreshUnlocked();
        }
    }

    public IReadOnlyList<Sticker> Query(string? categoryId, string? searchText)
    {
        lock (_gate)
        {
            IEnumerable<Sticker> query = _stickers;

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

            return query.ToList();
        }
    }

    public Category CreateCategory(string name)
    {
        var normalized = NormalizeCategoryName(name);
        lock (_gate)
        {
            var dir = Path.Combine(_paths.LibraryRoot, normalized);
            if (Directory.Exists(dir))
            {
                throw new InvalidOperationException($"分类已存在：{normalized}");
            }

            Directory.CreateDirectory(dir);
            RefreshUnlocked();
            return _categories.First(c =>
                !c.IsVirtual && string.Equals(c.Id, normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void RenameCategory(string categoryId, string newName)
    {
        if (string.Equals(categoryId, Category.AllId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不能重命名虚拟分类「全部」。");
        }

        var normalized = NormalizeCategoryName(newName);
        lock (_gate)
        {
            var source = Path.Combine(_paths.LibraryRoot, categoryId);
            if (!Directory.Exists(source))
            {
                throw new DirectoryNotFoundException($"分类不存在：{categoryId}");
            }

            var dest = Path.Combine(_paths.LibraryRoot, normalized);
            if (Directory.Exists(dest)
                && !string.Equals(categoryId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"目标分类已存在：{normalized}");
            }

            if (!string.Equals(categoryId, normalized, StringComparison.OrdinalIgnoreCase))
            {
                Directory.Move(source, dest);
                RemapCategoryPaths(categoryId, normalized);
                AtomicJson.Save(_paths.MetadataPath, _metadata);
                AtomicJson.Save(_paths.HashesPath, _hashes);
            }

            RefreshUnlocked();
        }
    }

    public void DeleteCategory(string categoryId, bool deleteFiles)
    {
        if (string.Equals(categoryId, Category.AllId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不能删除虚拟分类「全部」。");
        }

        lock (_gate)
        {
            var dir = Path.Combine(_paths.LibraryRoot, categoryId);
            if (!Directory.Exists(dir))
            {
                throw new DirectoryNotFoundException($"分类不存在：{categoryId}");
            }

            var files = Directory.EnumerateFiles(dir).Any();
            if (files && !deleteFiles)
            {
                throw new InvalidOperationException("分类非空，请先移走图片或确认删除文件。");
            }

            Directory.Delete(dir, recursive: true);
            RemoveCategoryFromIndexes(categoryId);
            AtomicJson.Save(_paths.MetadataPath, _metadata);
            AtomicJson.Save(_paths.HashesPath, _hashes);
            RefreshUnlocked();
        }
    }

    public async Task<ImportResult> ImportAsync(
        IEnumerable<string> paths,
        string? targetCategoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var expanded = ExpandSourceFiles(paths).ToList();
        var importedPaths = new List<string>();
        var failedPaths = new List<string>();
        var imported = 0;
        var duplicates = 0;
        var failed = 0;

        string categoryName;
        lock (_gate)
        {
            categoryName = ResolveImportCategory(targetCategoryId);
            Directory.CreateDirectory(Path.Combine(_paths.LibraryRoot, categoryName));
            _metadata = AtomicJson.LoadOrCreate(_paths.MetadataPath, () => new MetadataDocument());
            _hashes = AtomicJson.LoadOrCreate(_paths.HashesPath, () => new HashesDocument());
        }

        foreach (var source in expanded)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

                lock (_gate)
                {
                    if (_hashes.Hashes.ContainsKey(hash))
                    {
                        duplicates++;
                        continue;
                    }

                    var fileName = Path.GetFileName(source);
                    var destFile = AllocateUniqueFilePath(categoryName, fileName);
                    File.WriteAllBytes(destFile, bytes);

                    var relative = ToRelativePath(destFile);
                    var now = DateTimeOffset.UtcNow;
                    _metadata.Stickers[relative] = new StickerMetadataEntry
                    {
                        RelativePath = relative,
                        Tags = [],
                        CreatedAt = now,
                        Hash = hash,
                    };
                    _hashes.Hashes[hash] = relative;
                    importedPaths.Add(relative);
                    imported++;
                }
            }
            catch (Exception)
            {
                failed++;
                failedPaths.Add(source);
            }
        }

        lock (_gate)
        {
            AtomicJson.Save(_paths.MetadataPath, _metadata);
            AtomicJson.Save(_paths.HashesPath, _hashes);
            RefreshUnlocked();
        }

        return new ImportResult
        {
            Imported = imported,
            Duplicates = duplicates,
            Failed = failed,
            FailedPaths = failedPaths,
            ImportedRelativePaths = importedPaths,
        };
    }

    public void MoveSticker(string relativePath, string targetCategoryId)
    {
        if (string.Equals(targetCategoryId, Category.AllId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不能移动到虚拟分类「全部」。");
        }

        var normalizedCategory = NormalizeCategoryName(targetCategoryId);
        lock (_gate)
        {
            var key = NormalizeRelativeKey(relativePath);
            var sourceAbs = ToAbsolutePath(key);
            if (!File.Exists(sourceAbs))
            {
                throw new FileNotFoundException("贴纸文件不存在。", sourceAbs);
            }

            var fileName = Path.GetFileName(sourceAbs);
            Directory.CreateDirectory(Path.Combine(_paths.LibraryRoot, normalizedCategory));
            var destAbs = AllocateUniqueFilePath(normalizedCategory, fileName);
            File.Move(sourceAbs, destAbs);

            var newRelative = ToRelativePath(destAbs);
            if (_metadata.Stickers.TryGetValue(key, out var entry))
            {
                _metadata.Stickers.Remove(key);
                entry.RelativePath = newRelative;
                _metadata.Stickers[newRelative] = entry;
                if (!string.IsNullOrEmpty(entry.Hash))
                {
                    _hashes.Hashes[entry.Hash] = newRelative;
                }
            }

            AtomicJson.Save(_paths.MetadataPath, _metadata);
            AtomicJson.Save(_paths.HashesPath, _hashes);
            RefreshUnlocked();
        }
    }

    public void SetTags(string relativePath, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        lock (_gate)
        {
            var key = NormalizeRelativeKey(relativePath);
            if (!_metadata.Stickers.TryGetValue(key, out var entry))
            {
                throw new KeyNotFoundException($"未知贴纸：{relativePath}");
            }

            entry.Tags = tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            AtomicJson.Save(_paths.MetadataPath, _metadata);
            RefreshUnlocked();
        }
    }

    private void RefreshUnlocked()
    {
        _paths.EnsureDataLayout();
        _metadata = AtomicJson.LoadOrCreate(_paths.MetadataPath, () => new MetadataDocument());
        _hashes = AtomicJson.LoadOrCreate(_paths.HashesPath, () => new HashesDocument());

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
                var ext = Path.GetExtension(file);
                if (!SupportedExtensions.Contains(ext))
                {
                    continue;
                }

                var relative = ToRelativePath(file);
                var entry = GetOrCreateMetadata(relative, file);
                stickers.Add(ToSticker(relative, file, categoryName, entry));
                categoryCounts[categoryName]++;
            }
        }

        PruneMissingMetadata(stickers);
        RebuildHashesFromMetadata();
        AtomicJson.Save(_paths.MetadataPath, _metadata);
        AtomicJson.Save(_paths.HashesPath, _hashes);

        _stickers = stickers
            .OrderBy(s => s.CategoryId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categories = new List<Category> { Category.All(_stickers.Count) };
        foreach (var name in categoryCounts.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            categories.Add(Category.FromFolder(name, categoryCounts[name]));
        }

        _categories = categories;
    }

    private StickerMetadataEntry GetOrCreateMetadata(string relative, string absolutePath)
    {
        if (_metadata.Stickers.TryGetValue(relative, out var existing))
        {
            return existing;
        }

        string? hash = null;
        try
        {
            var bytes = File.ReadAllBytes(absolutePath);
            hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        var entry = new StickerMetadataEntry
        {
            RelativePath = relative,
            Tags = [],
            CreatedAt = File.GetCreationTimeUtc(absolutePath),
            Hash = hash,
        };
        _metadata.Stickers[relative] = entry;
        if (hash is not null)
        {
            _hashes.Hashes[hash] = relative;
        }

        return entry;
    }

    private void PruneMissingMetadata(List<Sticker> stickers)
    {
        var live = stickers.Select(s => s.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphanKeys = _metadata.Stickers.Keys.Where(k => !live.Contains(k)).ToList();
        foreach (var key in orphanKeys)
        {
            _metadata.Stickers.Remove(key);
        }
    }

    private void RebuildHashesFromMetadata()
    {
        var rebuilt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relative, entry) in _metadata.Stickers)
        {
            if (!string.IsNullOrEmpty(entry.Hash))
            {
                rebuilt[entry.Hash] = relative;
            }
        }

        _hashes.Hashes = rebuilt;
    }

    private void RemapCategoryPaths(string oldCategory, string newCategory)
    {
        var prefix = $"library/{oldCategory}/";
        var updates = new List<(string Old, string New, StickerMetadataEntry Entry)>();
        foreach (var (key, entry) in _metadata.Stickers)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = key[prefix.Length..];
            var newKey = $"library/{newCategory}/{fileName}";
            updates.Add((key, newKey, entry));
        }

        foreach (var (oldKey, newKey, entry) in updates)
        {
            _metadata.Stickers.Remove(oldKey);
            entry.RelativePath = newKey;
            _metadata.Stickers[newKey] = entry;
            if (!string.IsNullOrEmpty(entry.Hash))
            {
                _hashes.Hashes[entry.Hash] = newKey;
            }
        }
    }

    private void RemoveCategoryFromIndexes(string categoryId)
    {
        var prefix = $"library/{categoryId}/";
        var keys = _metadata.Stickers.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in keys)
        {
            if (_metadata.Stickers.TryGetValue(key, out var entry)
                && !string.IsNullOrEmpty(entry.Hash))
            {
                _hashes.Hashes.Remove(entry.Hash);
            }

            _metadata.Stickers.Remove(key);
        }
    }

    private string ResolveImportCategory(string? targetCategoryId)
    {
        if (string.IsNullOrWhiteSpace(targetCategoryId)
            || string.Equals(targetCategoryId, Category.AllId, StringComparison.Ordinal))
        {
            var inbox = Path.Combine(_paths.LibraryRoot, Category.InboxName);
            Directory.CreateDirectory(inbox);
            return Category.InboxName;
        }

        return NormalizeCategoryName(targetCategoryId);
    }

    private string AllocateUniqueFilePath(string categoryName, string fileName)
    {
        var safeName = SanitizeFileName(fileName);
        var dir = Path.Combine(_paths.LibraryRoot, categoryName);
        var dest = Path.Combine(dir, safeName);
        if (!File.Exists(dest))
        {
            return dest;
        }

        var name = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i}{ext}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(dir, $"{name}_{Guid.NewGuid():N}{ext}");
    }

    private static IEnumerable<string> ExpandSourceFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    {
                        yield return file;
                    }
                }
            }
            else if (File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path)))
            {
                yield return path;
            }
        }
    }

    private static bool MatchesSearch(Sticker sticker, string term)
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
            Tags = entry.Tags.ToList(),
            Hash = entry.Hash,
            CreatedAt = entry.CreatedAt,
        };

    private string ToRelativePath(string absolutePath)
    {
        var relative = Path.GetRelativePath(_paths.DataRoot, absolutePath);
        return NormalizeRelativeKey(relative);
    }

    private string ToAbsolutePath(string relativePath)
    {
        var parts = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Path.GetFullPath(Path.Combine(new[] { _paths.DataRoot }.Concat(parts).ToArray()));
    }

    private static string NormalizeRelativeKey(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    private static string NormalizeCategoryName(string name)
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

    private static string SanitizeFileName(string fileName)
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

}
