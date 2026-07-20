using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StickerPicker.Controls;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private Bitmap? _hoverBitmap;
    private bool _hoverVisible;

    private void RegisterHoverHandlers()
    {
        AddHandler(StickerHoverRouter.StickerHoverEvent, OnStickerHover);
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

        ShowHoverPreview(e.Sticker, e.Source as Control);
    }

    private void ShowHoverPreview(StickerItemViewModel item, Control? source)
    {
        _hoverBitmap?.Dispose();
        _hoverBitmap = LoadHoverBitmap(item.AbsolutePath);
        if (_hoverBitmap is null)
        {
            HideHoverPreview();
            return;
        }

        HoverPreviewImage.Source = _hoverBitmap;
        HoverPreviewBorder.IsVisible = true;
        _hoverVisible = true;
        PositionHoverPreview(source);
    }

    private void HideHoverPreview()
    {
        if (!_hoverVisible)
        {
            return;
        }

        _hoverVisible = false;
        HoverPreviewBorder.IsVisible = false;
        HoverPreviewImage.Source = null;
        _hoverBitmap?.Dispose();
        _hoverBitmap = null;
    }

    private void PositionHoverPreview(Control? source)
    {
        var origin = source is null
            ? new Point(0, 0)
            : source.TranslatePoint(new Point(source.Bounds.Width, 0), this) ?? new Point(0, 0);

        const double Offset = 16;
        var previewWidth = HoverPreviewBorder.Bounds.Width > 0 ? HoverPreviewBorder.Bounds.Width : 240;
        var previewHeight = HoverPreviewBorder.Bounds.Height > 0 ? HoverPreviewBorder.Bounds.Height : 240;

        var x = origin.X + Offset;
        var y = origin.Y + Offset;
        if (x + previewWidth > Bounds.Width)
        {
            x = Math.Max(0, origin.X - Offset - previewWidth);
        }

        if (y + previewHeight > Bounds.Height)
        {
            y = Math.Max(0, origin.Y - Offset - previewHeight);
        }

        HoverPreviewBorder.RenderTransform = new TranslateTransform(x, y);
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
