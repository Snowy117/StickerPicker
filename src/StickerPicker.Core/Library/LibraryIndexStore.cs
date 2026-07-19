using System.Security.Cryptography;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Json;

namespace StickerPicker.Core.Library;

internal sealed class LibraryIndexStore(IAppPaths paths)
{
    private readonly IAppPaths _paths = paths;

    public MetadataDocument Metadata { get; private set; } = new();
    public HashesDocument Hashes { get; private set; } = new();

    public void Load()
    {
        Metadata = AtomicJson.LoadOrCreate(_paths.MetadataPath, () => new MetadataDocument());
        Hashes = AtomicJson.LoadOrCreate(_paths.HashesPath, () => new HashesDocument());
    }

    public void Save()
    {
        AtomicJson.Save(_paths.MetadataPath, Metadata);
        AtomicJson.Save(_paths.HashesPath, Hashes);
    }

    public StickerMetadataEntry GetOrCreateMetadata(string relative, string absolutePath)
    {
        if (Metadata.Stickers.TryGetValue(relative, out var existing))
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
            // File may be locked or missing mid-scan; leave hash unset.
        }
        catch (UnauthorizedAccessException)
        {
            // Skip hashing when the process cannot read the file.
        }

        var entry = new StickerMetadataEntry
        {
            RelativePath = relative,
            Tags = [],
            CreatedAt = new DateTimeOffset(File.GetCreationTimeUtc(absolutePath), TimeSpan.Zero),
            Hash = hash,
        };
        Metadata.Stickers[relative] = entry;
        if (hash is not null)
        {
            Hashes.Hashes[hash] = relative;
        }

        return entry;
    }

    public void RegisterImported(string relative, string hash, DateTimeOffset createdAt)
    {
        Metadata.Stickers[relative] = new StickerMetadataEntry
        {
            RelativePath = relative,
            Tags = [],
            CreatedAt = createdAt,
            Hash = hash,
        };
        Hashes.Hashes[hash] = relative;
    }

    public bool ContainsHash(string hash) => Hashes.Hashes.ContainsKey(hash);

    public void PruneMissingMetadata(IEnumerable<string> liveRelativePaths)
    {
        var live = liveRelativePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orphanKeys = Metadata.Stickers.Keys.Where(k => !live.Contains(k)).ToList();
        foreach (var key in orphanKeys)
        {
            Metadata.Stickers.Remove(key);
        }
    }

    public void RebuildHashesFromMetadata()
    {
        var rebuilt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relative, entry) in Metadata.Stickers)
        {
            if (!string.IsNullOrEmpty(entry.Hash))
            {
                rebuilt[entry.Hash] = relative;
            }
        }

        Hashes.Hashes = rebuilt;
    }

    public void RemapCategoryPaths(string oldCategory, string newCategory)
    {
        var prefix = $"library/{oldCategory}/";
        var updates = new List<(string Old, string New, StickerMetadataEntry Entry)>();
        foreach (var (key, entry) in Metadata.Stickers)
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
            Metadata.Stickers.Remove(oldKey);
            entry.RelativePath = newKey;
            Metadata.Stickers[newKey] = entry;
            if (!string.IsNullOrEmpty(entry.Hash))
            {
                Hashes.Hashes[entry.Hash] = newKey;
            }
        }
    }

    public void RemoveCategoryFromIndexes(string categoryId)
    {
        var prefix = $"library/{categoryId}/";
        var keys = Metadata.Stickers.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var key in keys)
        {
            if (Metadata.Stickers.TryGetValue(key, out var entry)
                && !string.IsNullOrEmpty(entry.Hash))
            {
                Hashes.Hashes.Remove(entry.Hash);
            }

            Metadata.Stickers.Remove(key);
        }
    }

    public void MoveMetadataKey(string oldRelative, string newRelative)
    {
        if (!Metadata.Stickers.TryGetValue(oldRelative, out var entry))
        {
            return;
        }

        Metadata.Stickers.Remove(oldRelative);
        entry.RelativePath = newRelative;
        Metadata.Stickers[newRelative] = entry;
        if (!string.IsNullOrEmpty(entry.Hash))
        {
            Hashes.Hashes[entry.Hash] = newRelative;
        }
    }

    public void SetTags(string relative, IReadOnlyList<string> tags)
    {
        if (!Metadata.Stickers.TryGetValue(relative, out var entry))
        {
            throw new KeyNotFoundException($"未知贴纸：{relative}");
        }

        entry.Tags =
        [
            .. tags
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }
}
