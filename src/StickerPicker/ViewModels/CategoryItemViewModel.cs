using CommunityToolkit.Mvvm.ComponentModel;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class CategoryItemViewModel : ViewModelBase
{
    public CategoryItemViewModel(Category category)
    {
        Id = category.Id;
        Name = category.Name;
        IsVirtual = category.IsVirtual;
        StickerCount = category.StickerCount;
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsVirtual { get; }

    [ObservableProperty]
    public partial int StickerCount { get; set; }

    public string DisplayName => IsVirtual ? Name : $"{Name} ({StickerCount})";
}
