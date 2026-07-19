using Avalonia.Controls;
using Avalonia.Input;
using StickerPicker.ViewModels;

namespace StickerPicker.Controls;

public partial class StickerTile : UserControl
{
    public StickerTile()
    {
        InitializeComponent();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (DataContext is StickerItemViewModel item && item.SelectCommand.CanExecute(item))
        {
            item.SelectCommand.Execute(item);
            e.Handled = true;
        }
    }
}
