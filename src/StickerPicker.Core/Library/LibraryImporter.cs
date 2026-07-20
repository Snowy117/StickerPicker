using System.Security.Cryptography;
using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Library;

internal sealed class LibraryImporter(IAppPaths paths, LibraryIndexStore index)
{
    private const string TemporaryFilePattern = ".stickerpicker-*.tmp";
    private static readonly TimeSpan s_staleTemporaryFileAge = TimeSpan.FromDays(1);
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
        DeleteStaleTemporaryFiles(categoryDir);
        _index.Load();

        try
        {
            foreach (var source in expanded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var result = await ImportSourceAsync(source, categoryDir, cancellationToken)
                        .ConfigureAwait(false);
                    if (result is null)
                    {
                        duplicates++;
                        continue;
                    }

                    _index.RegisterImported(result.Value.RelativePath, result.Value.Hash, DateTimeOffset.UtcNow);
                    importedPaths.Add(result.Value.RelativePath);
                    imported++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    failed++;
                    failedPaths.Add(source);
                }
            }
        }
        finally
        {
            _index.Save();
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

    private async Task<(string RelativePath, string Hash)?> ImportSourceAsync(
        string source,
        string categoryDir,
        CancellationToken cancellationToken)
    {
        var temporaryFile = Path.Combine(categoryDir, $".stickerpicker-{Guid.NewGuid():N}.tmp");
        try
        {
            var hash = await CopyAndHashAsync(source, temporaryFile, cancellationToken)
                .ConfigureAwait(false);
            if (_index.ContainsHash(hash))
            {
                File.Delete(temporaryFile);
                return null;
            }

            var destination = LibraryPathRules.AllocateUniqueFilePath(categoryDir, Path.GetFileName(source));
            File.Move(temporaryFile, destination);
            var relative = LibraryPathRules.ToRelativePath(_paths.DataRoot, destination);
            return (relative, hash);
        }
        catch
        {
            TryDeleteTemporaryFile(temporaryFile);
            throw;
        }
    }

    private static async Task<string> CopyAndHashAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = OpenReadStream(source);
        await using var destinationStream = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        while (true)
        {
            var read = await sourceStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer, 0, read);
            await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void DeleteStaleTemporaryFiles(string categoryDirectory)
    {
        try
        {
            var cutoff = DateTime.UtcNow - s_staleTemporaryFileAge;
            foreach (var path in Directory.EnumerateFiles(
                         categoryDirectory,
                         TemporaryFilePattern,
                         SearchOption.TopDirectoryOnly)
                         .Where(path => File.GetLastWriteTimeUtc(path) < cutoff))
            {
                TryDeleteTemporaryFile(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine(exception);
        }
    }

    private static FileStream OpenReadStream(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 81920,
        FileOptions.Asynchronous | FileOptions.SequentialScan);

    private string ResolveImportCategory(string? targetCategoryId)
    {
        if (!string.IsNullOrWhiteSpace(targetCategoryId)
            && !string.Equals(targetCategoryId, Category.AllId, StringComparison.Ordinal))
        {
            return LibraryPathRules.NormalizeCategoryName(targetCategoryId);
        }

        var inbox = Path.Combine(_paths.LibraryRoot, Category.InboxName);
        Directory.CreateDirectory(inbox);
        return Category.InboxName;
    }
}
