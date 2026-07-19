using CommunityToolkit.Mvvm.ComponentModel;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class CategoryItemViewModel(Category category) : ViewModelBase
{
    public string Id { get; } = category.Id;
    public string Name { get; } = category.Name;
    public bool IsVirtual { get; } = category.IsVirtual;

    [ObservableProperty]
    public partial int StickerCount { get; set; } = category.StickerCount;

    public string DisplayName => IsVirtual
        ? Name
        : string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{Name} ({StickerCount})");
}
