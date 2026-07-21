using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StickerPicker.Controls;
using StickerPicker.Services;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private Window? _hoverPreviewWindow;
    private Image? _hoverPreviewImage;
    private Border? _hoverPreviewBorder;
    private Bitmap? _hoverBitmap;
    private CancellationTokenSource? _hoverCancellation;
    private Task? _hoverLoadTask;
    private int _hoverRequestVersion;
    private bool _hoverVisible;
    private Point _lastScreenPos;

    private void RegisterHoverHandlers()
    {
        AddHandler(StickerHoverRouter.StickerHoverEvent, OnStickerHover);
        PointerMoved += OnHoverPointerMoved;
        Deactivated += (_, _) => HideHoverPreview();
        DataContextChanged += OnHoverDataContextChanged;
        AttachHoverSettings(DataContext);
    }

    private void OnHoverDataContextChanged(object? sender, EventArgs e) => AttachHoverSettings(DataContext);

    private void AttachHoverSettings(object? dataContext)
    {
        if (dataContext is not MainViewModel vm)
        {
            return;
        }

        vm.Settings.PropertyChanged -= OnHoverSettingsPropertyChanged;
        vm.Settings.PropertyChanged += OnHoverSettingsPropertyChanged;
        ApplyHoverPreviewOpacity(vm.Settings.PreviewOpacity);
    }

    private void OnHoverSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SettingsViewModel.PreviewOpacity), StringComparison.Ordinal)
            && sender is SettingsViewModel settings)
        {
            ApplyHoverPreviewOpacity(settings.PreviewOpacity);
        }
    }

    private void ApplyHoverPreviewOpacity(double opacity)
    {
        if (_hoverPreviewImage is { } image)
        {
            image.Opacity = opacity;
        }
    }

    private void OnStickerHover(object? sender, StickerHoverEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!e.IsEnter)
        {
            vm.HoveredFileName = "";
            HideHoverPreview();
            return;
        }

        vm.HoveredFileName = e.Sticker.FileName;

        if (!vm.Settings.HoverPreview)
        {
            return;
        }

        var requestVersion = ++_hoverRequestVersion;
        CancelHoverDecode();
        _hoverCancellation = new CancellationTokenSource();
        _hoverLoadTask = ShowHoverPreviewAsync(
            e.Sticker,
            requestVersion,
            _hoverCancellation.Token);
    }

    private void OnHoverPointerMoved(object? sender, PointerEventArgs e)
    {
        _lastScreenPos = e.GetPosition(this);
        if (_hoverVisible)
        {
            PositionHoverPreview(_lastScreenPos);
        }
    }

    private async Task ShowHoverPreviewAsync(
        StickerItemViewModel item,
        int requestVersion,
        CancellationToken cancellationToken)
    {
        if (requestVersion != _hoverRequestVersion)
        {
            return;
        }

        Bitmap? bitmap;
        try
        {
            bitmap = await BoundedImageDecoder.DecodeAsync(item.AbsolutePath, 392, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception)
        {
            if (!cancellationToken.IsCancellationRequested && requestVersion == _hoverRequestVersion)
            {
                HideHoverPreview();
            }

            return;
        }

        if (cancellationToken.IsCancellationRequested || requestVersion != _hoverRequestVersion)
        {
            bitmap?.Dispose();
            return;
        }

        if (bitmap is null)
        {
            HideHoverPreview();
            return;
        }

        EnsureHoverPreviewWindow();
        var window = _hoverPreviewWindow!;
        _hoverPreviewImage!.Source = null;
        _hoverBitmap?.Dispose();
        _hoverBitmap = bitmap;
        _hoverPreviewImage!.Source = _hoverBitmap;
        PositionHoverPreview(_lastScreenPos);
        if (!_hoverVisible)
        {
            _hoverVisible = true;
            window.Show(this);
        }
    }

    private void HideHoverPreview()
    {
        _hoverRequestVersion++;
        if (_hoverLoadTask is { IsFaulted: true } failedLoad)
        {
            _ = failedLoad.Exception;
        }

        _hoverLoadTask = null;
        CancelHoverDecode();
        _hoverVisible = false;
        _hoverPreviewWindow?.Hide();
        _hoverPreviewImage?.Source = null;

        _hoverBitmap?.Dispose();
        _hoverBitmap = null;

        if (DataContext is MainViewModel vm)
        {
            vm.HoveredFileName = "";
        }
    }

    private void CancelHoverDecode()
    {
        _hoverCancellation?.Cancel();
        _hoverCancellation?.Dispose();
        _hoverCancellation = null;
    }

    private void DisposeHoverPreview()
    {
        HideHoverPreview();
        _hoverPreviewWindow?.Close();
        _hoverPreviewWindow = null;
        _hoverPreviewImage = null;
        _hoverPreviewBorder = null;
    }

    // Separate topmost window so the preview is never clipped by the main window
    // and can follow the cursor anywhere on screen.
    private void EnsureHoverPreviewWindow()
    {
        if (_hoverPreviewWindow is not null)
        {
            return;
        }

        var opacity = DataContext is MainViewModel vm ? vm.Settings.PreviewOpacity : 0.92;
        _hoverPreviewImage = new Image
        {
            Stretch = Stretch.Uniform,
            Opacity = opacity,
        };
        var app = Application.Current;
        // Transparent background: the preview must not occlude other apps behind it.
        // Opacity is driven by the image itself so the slider actually reveals content below.
        _hoverPreviewBorder = new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = app?.FindResource("SteamBorderBrush") as IBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4),
            Child = _hoverPreviewImage,
        };
        _hoverPreviewWindow = new Window
        {
            ShowActivated = false,
            WindowDecorations = WindowDecorations.None,
            ShowInTaskbar = false,
            IsHitTestVisible = false,
            Topmost = true,
            CanResize = false,
            Background = new SolidColorBrush(Colors.Transparent),
            SizeToContent = SizeToContent.WidthAndHeight,
            MaxWidth = 400,
            MaxHeight = 400,
            Content = _hoverPreviewBorder,
        };
    }

    private void PositionHoverPreview(Point windowRelative)
    {
        if (_hoverPreviewWindow is null)
        {
            return;
        }

        const double Offset = 16;
        const int MaxBitmap = 392;
        var previewW = Math.Min(_hoverBitmap?.PixelSize.Width ?? MaxBitmap, MaxBitmap) + 10;
        var previewH = Math.Min(_hoverBitmap?.PixelSize.Height ?? MaxBitmap, MaxBitmap) + 10;

        var client = this.PointToScreen(windowRelative);
        var x = client.X + (int)Offset;
        var y = client.Y + (int)Offset;

        var screen = Screens.ScreenFromPoint(new PixelPoint(x, y));
        if (screen is not null)
        {
            if (x + previewW > screen.Bounds.Right)
            {
                x = client.X - (int)Offset - previewW;
            }

            if (y + previewH > screen.Bounds.Bottom)
            {
                y = client.Y - (int)Offset - previewH;
            }
        }

        _hoverPreviewWindow.Position = new PixelPoint(x, y);
    }

}
