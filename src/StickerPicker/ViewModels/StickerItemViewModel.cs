using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using StickerPicker.Core.Models;
using StickerPicker.Services;

namespace StickerPicker.ViewModels;

public sealed class StickerItemViewModel(
    Sticker sticker,
    double tileSize,
    ICommand selectCommand) : ViewModelBase, IDisposable
{
    private static readonly int[] s_decodeBuckets = [96, 128, 192, 256, 384];
    private CancellationTokenSource? _thumbnailCancellation;
    private Task? _thumbnailLoadTask;
    private Bitmap? _thumbnail;
    private double _tileSize = tileSize;
    private int _decodedSide;
    private int _requestedSide;
    private bool _isThumbnailActive;
    private bool _disposed;

    public Sticker Sticker { get; private set; } = sticker;
    public string RelativePath => Sticker.RelativePath;
    public string AbsolutePath => Sticker.AbsolutePath;
    public string FileName => Sticker.FileName;
    public string CategoryId => Sticker.CategoryId;
    public ICommand SelectCommand { get; } = selectCommand;
    public bool IsThumbnailActive => _isThumbnailActive;

    public double TileSize
    {
        get => _tileSize;
        set => SetProperty(ref _tileSize, value);
    }

    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    public void UpdateSticker(Sticker sticker)
    {
        var sourceChanged = !string.Equals(AbsolutePath, sticker.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Sticker = sticker;
        OnPropertyChanged(nameof(Sticker));
        OnPropertyChanged(nameof(RelativePath));
        OnPropertyChanged(nameof(AbsolutePath));
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(CategoryId));
        if (sourceChanged)
        {
            ReloadThumbnail(TileSize);
        }
    }

    public void ReloadThumbnail(double tileSize)
    {
        CancelThumbnailLoad();
        _decodedSide = 0;
        _requestedSide = 0;
        ReplaceThumbnail(replacement: null);
        RequestThumbnail(tileSize);
    }

    public void RequestThumbnail(double tileSize)
    {
        if (_disposed || !_isThumbnailActive)
        {
            return;
        }

        var targetSide = SelectDecodeBucket(tileSize);
        if (targetSide <= _decodedSide
            || (targetSide == _requestedSide && _thumbnailLoadTask is { IsCompleted: false }))
        {
            return;
        }

        CancelThumbnailLoad();
        _requestedSide = targetSide;
        var request = new CancellationTokenSource();
        _thumbnailCancellation = request;
        _thumbnailLoadTask = LoadThumbnailAsync(targetSide, request);
    }

    public void SetThumbnailActive(bool isActive)
    {
        if (_disposed || _isThumbnailActive == isActive)
        {
            return;
        }

        _isThumbnailActive = isActive;
        if (isActive)
        {
            RequestThumbnail(TileSize);
            return;
        }

        CancelThumbnailLoad();
        _requestedSide = 0;
        _decodedSide = 0;
        ReplaceThumbnail(replacement: null);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelThumbnailLoad();
        ReplaceThumbnail(replacement: null);
    }

    private void CancelThumbnailLoad()
    {
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        _thumbnailCancellation = null;
        _thumbnailLoadTask = null;
    }

    private async Task LoadThumbnailAsync(int targetSide, CancellationTokenSource request)
    {
        Bitmap? bitmap = null;
        try
        {
            bitmap = await BoundedImageDecoder
                .DecodeAsync(AbsolutePath, targetSide, request.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            ResetFailedRequest(targetSide, request);
            return;
        }

        if (bitmap is null)
        {
            ResetFailedRequest(targetSide, request);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyDecodedThumbnail(bitmap, targetSide, request));
    }

    private void ApplyDecodedThumbnail(Bitmap bitmap, int targetSide, CancellationTokenSource request)
    {
        if (_disposed
            || request.IsCancellationRequested
            || !ReferenceEquals(request, _thumbnailCancellation)
            || targetSide != _requestedSide)
        {
            bitmap.Dispose();
            return;
        }

        ReplaceThumbnail(bitmap);
        _decodedSide = targetSide;
        CompleteThumbnailRequest(request);
    }

    private void ResetFailedRequest(int targetSide, CancellationTokenSource request)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed
                && ReferenceEquals(request, _thumbnailCancellation)
                && targetSide == _requestedSide)
            {
                _requestedSide = 0;
                CompleteThumbnailRequest(request);
            }
        });
    }

    private void CompleteThumbnailRequest(CancellationTokenSource request)
    {
        if (!ReferenceEquals(request, _thumbnailCancellation))
        {
            return;
        }

        request.Dispose();
        _thumbnailCancellation = null;
        _thumbnailLoadTask = null;
    }

    private void ReplaceThumbnail(Bitmap? replacement)
    {
        var previous = Thumbnail;
        Thumbnail = replacement;
        previous?.Dispose();
    }

    private static int SelectDecodeBucket(double tileSize)
    {
        var desired = (int)Math.Ceiling(tileSize * 1.25);
        return s_decodeBuckets.FirstOrDefault(bucket => bucket >= desired, s_decodeBuckets[^1]);
    }
}
