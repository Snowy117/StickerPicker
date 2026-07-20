using Avalonia.Interactivity;
using StickerPicker.ViewModels;

namespace StickerPicker.Controls;

public enum StickerActionKind
{
    EditTags,
    Move,
    Delete,
}

public sealed class StickerActionEventArgs(StickerItemViewModel sticker, StickerActionKind kind, string? targetCategoryId = null)
    : RoutedEventArgs
{
    public StickerItemViewModel Sticker { get; } = sticker;
    public StickerActionKind Kind { get; } = kind;
    public string? TargetCategoryId { get; } = targetCategoryId;
}

/// <summary>
/// Bubbling event raised by <see cref="StickerTile"/> context menu; handled by
/// <c>MainWindow</c> to present a dialog and dispatch to <c>MainViewModel</c>.
/// </summary>
public static class StickerActionRouter
{
    public static readonly RoutedEvent<StickerActionEventArgs> StickerActionEvent =
        RoutedEvent.Register<StickerActionEventArgs>(
            nameof(StickerActionEvent),
            RoutingStrategies.Bubble,
            typeof(StickerActionRouter));
}
