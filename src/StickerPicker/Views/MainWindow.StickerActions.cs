using StickerPicker.Controls;
using StickerPicker.ViewModels;

namespace StickerPicker.Views;

public partial class MainWindow
{
    private void RegisterStickerActionHandlers()
    {
        AddHandler(StickerActionRouter.StickerActionEvent, OnStickerAction);
    }

    private async void OnStickerAction(object? sender, StickerActionEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        switch (e.Kind)
        {
            case StickerActionKind.EditTags:
                vm.OpenTagEditor(e.Sticker);
                break;
            case StickerActionKind.Move:
                await vm.MoveStickerCommand.ExecuteAsync((e.Sticker, e.TargetCategoryId!));
                break;
            case StickerActionKind.Delete:
                await DeleteStickerAsync(vm, e.Sticker);
                break;
            default:
                throw new InvalidOperationException($"Unknown sticker action kind: {e.Kind}");
        }
    }

    private async Task DeleteStickerAsync(MainViewModel vm, StickerItemViewModel item)
    {
        var confirmed = await ConfirmAsync("删除表情", $"确定删除「{item.FileName}」？\n文件将从库中永久移除。");
        if (!confirmed)
        {
            return;
        }

        await vm.DeleteStickerCommand.ExecuteAsync(item);
    }
}
