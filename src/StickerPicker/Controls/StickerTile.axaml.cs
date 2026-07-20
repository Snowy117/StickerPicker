using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using StickerPicker.ViewModels;

namespace StickerPicker.Controls;

public partial class StickerTile : UserControl
{
    public StickerTile()
    {
        InitializeComponent();
        Root.ContextMenu!.Opening += OnContextMenuOpening;
        EditTagsItem.Click += OnEditTagsClick;
        DeleteItem.Click += OnDeleteClick;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (DataContext is not StickerItemViewModel item || !item.SelectCommand.CanExecute(item))
        {
            return;
        }

        item.SelectCommand.Execute(item);
        e.Handled = true;
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        RebuildMoveSubmenu();
    }

    private void OnEditTagsClick(object? sender, RoutedEventArgs e) =>
        RaiseAction(StickerActionKind.EditTags, targetCategoryId: null);

    private void OnDeleteClick(object? sender, RoutedEventArgs e) =>
        RaiseAction(StickerActionKind.Delete, targetCategoryId: null);

    private void RebuildMoveSubmenu()
    {
        MoveSubmenu.Items.Clear();
        if (DataContext is not StickerItemViewModel item)
        {
            return;
        }

        var window = TopLevel.GetTopLevel(this) as Window;
        if (window?.DataContext is not MainViewModel main || main.Categories.Count == 0)
        {
            MoveSubmenu.IsEnabled = false;
            return;
        }

        MoveSubmenu.IsEnabled = true;
        var current = item.CategoryId;
        foreach (var category in main.Categories)
        {
            if (category.IsVirtual || string.Equals(category.Id, current, StringComparison.Ordinal))
            {
                continue;
            }

            var child = new MenuItem { Header = category.Name, CommandParameter = category.Id };
            child.Click += OnMoveToCategoryClick;
            MoveSubmenu.Items.Add(child);
        }
    }

    private void OnMoveToCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.CommandParameter is string targetId)
        {
            RaiseAction(StickerActionKind.Move, targetId);
        }
    }

    private void RaiseAction(StickerActionKind kind, string? targetCategoryId)
    {
        if (DataContext is not StickerItemViewModel item)
        {
            return;
        }

        var args = new StickerActionEventArgs(item, kind, targetCategoryId)
        {
            RoutedEvent = StickerActionRouter.StickerActionEvent,
            Source = this,
        };
        RaiseEvent(args);
    }
}
