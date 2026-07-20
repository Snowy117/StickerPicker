using CommunityToolkit.Mvvm.Input;

namespace StickerPicker.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private async Task EditStickerTagsAsync((StickerItemViewModel Item, IReadOnlyList<string> Tags) payload)
    {
        try
        {
            await RunLibraryOperationAsync(
                () => Task.Run(() => _library.SetTags(payload.Item.RelativePath, payload.Tags)));
            ApplyFilter();
            StatusText = $"已更新标签：{payload.Item.FileName}";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task MoveStickerAsync((StickerItemViewModel Item, string TargetCategoryId) payload)
    {
        try
        {
            await RunLibraryOperationAsync(
                () => Task.Run(() => _library.MoveSticker(payload.Item.RelativePath, payload.TargetCategoryId)));
            RebuildCategories();
            ApplyFilter();
            StatusText = $"已移动：{payload.Item.FileName}";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DeleteStickerAsync(StickerItemViewModel item)
    {
        try
        {
            await RunLibraryOperationAsync(
                () => Task.Run(() => _library.DeleteSticker(item.RelativePath)));
            RebuildCategories();
            ApplyFilter();
            StatusText = $"已删除：{item.FileName}";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
