using CommunityToolkit.Mvvm.Input;

namespace StickerPicker.ViewModels;

public partial class MainViewModel
{
    [RelayCommand]
    private void CreateCategory(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            var created = _library.CreateCategory(name.Trim());
            RebuildCategories();
            SelectedCategory = Categories.FirstOrDefault(c =>
                string.Equals(c.Id, created.Id, StringComparison.Ordinal));
            StatusText = $"已创建分类 {created.Name}";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void RenameCategory(string? newName)
    {
        if (SelectedCategory is null || SelectedCategory.IsVirtual || string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            var id = SelectedCategory.Id;
            _library.RenameCategory(id, newName.Trim());
            RebuildCategories();
            SelectedCategory = Categories.FirstOrDefault(c =>
                string.Equals(c.Name, newName.Trim(), StringComparison.OrdinalIgnoreCase));
            StatusText = "分类已重命名";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void DeleteCategory(bool deleteFiles)
    {
        if (SelectedCategory is null || SelectedCategory.IsVirtual)
        {
            return;
        }

        try
        {
            _library.DeleteCategory(SelectedCategory.Id, deleteFiles);
            RebuildCategories();
            SelectedCategory = Categories.FirstOrDefault();
            ApplyFilter();
            StatusText = "分类已删除";
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

}
