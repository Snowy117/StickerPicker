using Avalonia.Threading;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class MainViewModel
{
    private static readonly StringComparer s_relativePathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private readonly HashSet<StickerItemViewModel> _activeStickers = [];
    private bool _isThumbnailSurfaceVisible;
    private double _pendingThumbnailSize;
    private double _appliedThumbnailSize;

    public void SetThumbnailActive(StickerItemViewModel item, bool isActive)
    {
        if (isActive)
        {
            _activeStickers.Add(item);
            item.TileSize = ThumbnailSize;
        }
        else
        {
            _activeStickers.Remove(item);
        }

        item.SetThumbnailActive(isActive && _isThumbnailSurfaceVisible);
    }

    public void SetThumbnailSurfaceVisible(bool isVisible)
    {
        if (_isThumbnailSurfaceVisible == isVisible)
        {
            return;
        }

        _isThumbnailSurfaceVisible = isVisible;
        foreach (var sticker in _activeStickers)
        {
            sticker.SetThumbnailActive(isVisible);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = value;
        if (_isReady)
        {
            ScheduleFilter();
        }
    }

    partial void OnThumbnailSizeChanged(double value)
    {
        if (!_isReady)
        {
            return;
        }

        ScheduleThumbnailResize(value);
        ScheduleThumbnailDecode(value);
        ScheduleThumbnailSizePersist(value);
    }

    private void ScheduleThumbnailResize(double value)
    {
        _pendingThumbnailSize = value;
        _thumbnailResizeTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        RestartTimer(_thumbnailResizeTimer, OnThumbnailResizeTick);
    }

    private void OnThumbnailResizeTick(object? sender, EventArgs e)
    {
        _thumbnailResizeTimer?.Stop();
        if (Math.Abs(_appliedThumbnailSize - _pendingThumbnailSize) < 0.5)
        {
            return;
        }

        _appliedThumbnailSize = _pendingThumbnailSize;
        foreach (var sticker in _activeStickers)
        {
            sticker.TileSize = _pendingThumbnailSize;
        }
    }

    private void ScheduleThumbnailDecode(double value)
    {
        _pendingThumbnailSize = value;
        _thumbnailDecodeTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        RestartTimer(_thumbnailDecodeTimer, OnThumbnailDecodeTick);
    }

    private void OnThumbnailDecodeTick(object? sender, EventArgs e)
    {
        _thumbnailDecodeTimer?.Stop();
        foreach (var sticker in _activeStickers)
        {
            sticker.RequestThumbnail(_pendingThumbnailSize);
        }
    }

    private void ScheduleThumbnailSizePersist(double value)
    {
        _pendingThumbnailSize = value;
        _thumbnailSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        RestartTimer(_thumbnailSaveTimer, OnThumbnailSaveTick);
    }

    private void OnThumbnailSaveTick(object? sender, EventArgs e)
    {
        _thumbnailSaveTimer?.Stop();
        _config.ThumbnailSize = _pendingThumbnailSize;
        _configStore.Save(_config);
    }

    private void ApplyFilter(bool forceThumbnailReload = false)
    {
        if (!_isReady)
        {
            return;
        }

        var categoryId = SelectedCategory?.Id ?? Category.AllId;
        var results = FilterSnapshot(categoryId, SearchText);
        var existing = Stickers.ToDictionary(item => item.RelativePath, s_relativePathComparer);
        var next = new List<StickerItemViewModel>(results.Count);
        foreach (var sticker in results)
        {
            if (existing.Remove(sticker.RelativePath, out var item))
            {
                item.UpdateSticker(sticker);
                if (forceThumbnailReload)
                {
                    item.ReloadThumbnail(ThumbnailSize);
                }

                next.Add(item);
            }
            else
            {
                next.Add(new StickerItemViewModel(sticker, ThumbnailSize, SelectStickerCommand));
            }
        }

        DisposeStickers(existing.Values);
        Stickers.Clear();
        foreach (var item in next)
        {
            Stickers.Add(item);
        }

        StatusText = $"{Stickers.Count} 张表情";
    }

    private IReadOnlyList<Sticker> FilterSnapshot(string categoryId, string searchText)
    {
        IEnumerable<Sticker> query = _libraryStickers;
        if (!string.Equals(categoryId, Category.AllId, StringComparison.Ordinal))
        {
            query = query.Where(sticker =>
                string.Equals(sticker.CategoryId, categoryId, StringComparison.OrdinalIgnoreCase));
        }

        var terms = searchText.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return [.. query.Where(sticker => terms.All(term => MatchesSearch(sticker, term)))];
    }

    private static bool MatchesSearch(Sticker sticker, string term) =>
        sticker.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)
        || sticker.RelativePath.Contains(term, StringComparison.OrdinalIgnoreCase)
        || sticker.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));

    private void ScheduleFilter()
    {
        _searchTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        RestartTimer(_searchTimer, OnSearchTimerTick);
    }

    private void OnSearchTimerTick(object? sender, EventArgs e)
    {
        _searchTimer?.Stop();
        ApplyFilter();
    }

    private static void RestartTimer(DispatcherTimer timer, EventHandler handler)
    {
        timer.Tick -= handler;
        timer.Tick += handler;
        timer.Stop();
        timer.Start();
    }
}
