using System.Security.Cryptography;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Library;

internal sealed class LibraryImporter(IAppPaths paths, LibraryIndexStore index)
{
    private readonly IAppPaths _paths = paths;
    private readonly LibraryIndexStore _index = index;

    public async Task<ImportResult> ImportAsync(
        IEnumerable<string> paths,
        string? targetCategoryId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var expanded = LibraryPathRules.ExpandSourceFiles(paths).ToList();
        var importedPaths = new List<string>();
        var failedPaths = new List<string>();
        var imported = 0;
        var duplicates = 0;
        var failed = 0;

        var categoryName = ResolveImportCategory(targetCategoryId);
        var categoryDir = Path.Combine(_paths.LibraryRoot, categoryName);
        Directory.CreateDirectory(categoryDir);
        _index.Load();

        foreach (var source in expanded)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

                if (_index.ContainsHash(hash))
                {
                    duplicates++;
                    continue;
                }

                var fileName = Path.GetFileName(source);
                var destFile = LibraryPathRules.AllocateUniqueFilePath(categoryDir, fileName);
                await File.WriteAllBytesAsync(destFile, bytes, cancellationToken).ConfigureAwait(false);

                var relative = LibraryPathRules.ToRelativePath(_paths.DataRoot, destFile);
                _index.RegisterImported(relative, hash, DateTimeOffset.UtcNow);
                importedPaths.Add(relative);
                imported++;
            }
            catch (Exception)
            {
                failed++;
                failedPaths.Add(source);
            }
        }

        _index.Save();

        return new ImportResult
        {
            Imported = imported,
            Duplicates = duplicates,
            Failed = failed,
            FailedPaths = failedPaths,
            ImportedRelativePaths = importedPaths,
        };
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

        return LibraryPathRules.NormalizeCategoryName(targetCategoryId);
    }
}
