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
                await EditTagsAsync(vm, e.Sticker);
                break;
            case StickerActionKind.Move:
                vm.MoveStickerCommand.Execute((e.Sticker, e.TargetCategoryId!));
                break;
            case StickerActionKind.Delete:
                await DeleteStickerAsync(vm, e.Sticker);
                break;
        }
    }

    private async Task EditTagsAsync(MainViewModel vm, StickerItemViewModel item)
    {
        var current = string.Join(", ", item.Sticker.Tags);
        var result = await PromptForNameAsync("编辑标签", "标签（逗号分隔）", initial: current);
        if (result is null)
        {
            return;
        }

        var tags = result.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        vm.EditStickerTagsCommand.Execute((item, tags));
    }

    private async Task DeleteStickerAsync(MainViewModel vm, StickerItemViewModel item)
    {
        var confirmed = await ConfirmAsync("删除表情", $"确定删除「{item.FileName}」？\n文件将从库中永久移除。");
        if (!confirmed)
        {
            return;
        }

        vm.DeleteStickerCommand.Execute(item);
    }
}
