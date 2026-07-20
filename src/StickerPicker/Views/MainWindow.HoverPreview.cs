using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StickerPicker.Controls;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private Window? _hoverPreviewWindow;
    private Image? _hoverPreviewImage;
    private Bitmap? _hoverBitmap;
    private bool _hoverVisible;
    private Point _lastScreenPos;

    private void RegisterHoverHandlers()
    {
        AddHandler(StickerHoverRouter.StickerHoverEvent, OnStickerHover);
        PointerMoved += OnHoverPointerMoved;
        Deactivated += (_, _) => HideHoverPreview();
    }

    private void OnStickerHover(object? sender, StickerHoverEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!e.IsEnter)
        {
            HideHoverPreview();
            return;
        }

        if (!vm.Settings.HoverPreview)
        {
            return;
        }

        ShowHoverPreview(e.Sticker);
    }

    private void OnHoverPointerMoved(object? sender, PointerEventArgs e)
    {
        _lastScreenPos = e.GetPosition(this);
        if (_hoverVisible)
        {
            PositionHoverPreview(_lastScreenPos);
        }
    }

    private void ShowHoverPreview(StickerItemViewModel item)
    {
        _hoverBitmap?.Dispose();
        _hoverBitmap = LoadHoverBitmap(item.AbsolutePath);
        if (_hoverBitmap is null)
        {
            HideHoverPreview();
            return;
        }

        EnsureHoverPreviewWindow();
        var window = _hoverPreviewWindow!;
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
        if (!_hoverVisible)
        {
            return;
        }

        _hoverVisible = false;
        _hoverPreviewWindow?.Hide();
        _hoverPreviewImage?.Source = null;

        _hoverBitmap?.Dispose();
        _hoverBitmap = null;
    }

    // Separate topmost window so the preview is never clipped by the main window
    // and can follow the cursor anywhere on screen.
    private void EnsureHoverPreviewWindow()
    {
        if (_hoverPreviewWindow is not null)
        {
            return;
        }

        _hoverPreviewImage = new Image { Stretch = Stretch.Uniform };
        var app = Application.Current;
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
            Content = new Border
            {
                Opacity = 0.92,
                Background = app?.FindResource("SteamPanelBrush") as IBrush,
                BorderBrush = app?.FindResource("SteamBorderBrush") as IBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4),
                Child = _hoverPreviewImage,
            },
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

    private static Bitmap? LoadHoverBitmap(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, 480);
        }
        catch
        {
            return null;
        }
    }
}
