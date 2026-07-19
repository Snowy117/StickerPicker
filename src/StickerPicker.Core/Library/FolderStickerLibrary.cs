using StickerPicker.Core.Abstractions;
using StickerPicker.Core.Models;

namespace StickerPicker.Core.Library;

public sealed class FolderStickerLibrary : IStickerLibrary
{
    private readonly IAppPaths _paths;
    private readonly LibraryIndexStore _index;
    private readonly LibraryScanner _scanner;
    private readonly LibraryImporter _importer;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<Category> _categories = [];
    private List<Sticker> _stickers = [];

    public FolderStickerLibrary(IAppPaths paths)
    {
        _paths = paths;
        _index = new LibraryIndexStore(paths);
        _scanner = new LibraryScanner(paths, _index);
        _importer = new LibraryImporter(paths, _index);
    }

    public IReadOnlyList<Category> Categories
    {
        get
        {
            _gate.Wait();
            try
            {
                return _categories;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public IReadOnlyList<Sticker> Stickers
    {
        get
        {
            _gate.Wait();
            try
            {
                return _stickers;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public void Refresh()
    {
        _gate.Wait();
        try
        {
            RefreshUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<Sticker> Query(string? categoryId, string? searchText)
    {
        _gate.Wait();
        try
        {
            return LibraryPathRules.Filter(_stickers, categoryId, searchText);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Category CreateCategory(string name)
    {
        var normalized = LibraryPathRules.NormalizeCategoryName(name);
        _gate.Wait();
        try
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
        finally
        {
            _gate.Release();
        }
    }

    public void RenameCategory(string categoryId, string newName)
    {
        if (string.Equals(categoryId, Category.AllId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不能重命名虚拟分类「全部」。");
        }

        var normalized = LibraryPathRules.NormalizeCategoryName(newName);
        _gate.Wait();
        try
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
                _index.Load();
                _index.RemapCategoryPaths(categoryId, normalized);
                _index.Save();
            }

            RefreshUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void DeleteCategory(string categoryId, bool deleteFiles)
    {
        if (string.Equals(categoryId, Category.AllId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不能删除虚拟分类「全部」。");
        }

        _gate.Wait();
        try
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
            _index.Load();
            _index.RemoveCategoryFromIndexes(categoryId);
            _index.Save();
            RefreshUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ImportResult> ImportAsync(
        IEnumerable<string> paths,
        string? targetCategoryId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _importer
                .ImportAsync(paths, targetCategoryId, cancellationToken)
                .ConfigureAwait(false);
            RefreshUnlocked();
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void MoveSticker(string relativePath, string targetCategoryId)
    {
        if (string.Equals(targetCategoryId, Category.AllId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("不能移动到虚拟分类「全部」。");
        }

        var normalizedCategory = LibraryPathRules.NormalizeCategoryName(targetCategoryId);
        _gate.Wait();
        try
        {
            var key = LibraryPathRules.NormalizeRelativeKey(relativePath);
            var sourceAbs = LibraryPathRules.ToAbsolutePath(_paths.DataRoot, key);
            if (!File.Exists(sourceAbs))
            {
                throw new FileNotFoundException("贴纸文件不存在。", sourceAbs);
            }

            var fileName = Path.GetFileName(sourceAbs);
            var categoryDir = Path.Combine(_paths.LibraryRoot, normalizedCategory);
            Directory.CreateDirectory(categoryDir);
            var destAbs = LibraryPathRules.AllocateUniqueFilePath(categoryDir, fileName);
            File.Move(sourceAbs, destAbs);

            var newRelative = LibraryPathRules.ToRelativePath(_paths.DataRoot, destAbs);
            _index.Load();
            _index.MoveMetadataKey(key, newRelative);
            _index.Save();
            RefreshUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void SetTags(string relativePath, IReadOnlyList<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        _gate.Wait();
        try
        {
            var key = LibraryPathRules.NormalizeRelativeKey(relativePath);
            _index.Load();
            _index.SetTags(key, tags);
            _index.Save();
            RefreshUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void RefreshUnlocked()
    {
        var (categories, stickers) = _scanner.Scan();
        _categories = categories;
        _stickers = stickers;
    }
}
