using Avalonia.Interactivity;
using StickerPicker.ViewModels;

namespace StickerPicker.Controls;

public sealed class StickerHoverEventArgs(StickerItemViewModel sticker, bool isEnter) : RoutedEventArgs
{
    public StickerItemViewModel Sticker { get; } = sticker;
    public bool IsEnter { get; } = isEnter;
}

/// <summary>
/// Bubbling event raised by <see cref="StickerTile"/> on pointer enter (after a short delay)
/// and pointer leave. Handled by <c>MainWindow</c> to show/hide a large preview image.
/// </summary>
public static class StickerHoverRouter
{
    public static readonly RoutedEvent<StickerHoverEventArgs> StickerHoverEvent =
        RoutedEvent.Register<StickerHoverEventArgs>(
            nameof(StickerHoverEvent),
            RoutingStrategies.Bubble,
            typeof(StickerHoverRouter));
}
