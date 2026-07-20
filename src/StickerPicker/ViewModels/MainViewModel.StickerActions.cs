using CommunityToolkit.Mvvm.Input;

namespace StickerPicker.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void EditStickerTags((StickerItemViewModel Item, IReadOnlyList<string> Tags) payload)
    {
        try
        {
            _library.SetTags(payload.Item.RelativePath, payload.Tags);
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
    private void MoveSticker((StickerItemViewModel Item, string TargetCategoryId) payload)
    {
        try
        {
            _library.MoveSticker(payload.Item.RelativePath, payload.TargetCategoryId);
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
    private void DeleteSticker(StickerItemViewModel item)
    {
        try
        {
            _library.DeleteSticker(item.RelativePath);
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
