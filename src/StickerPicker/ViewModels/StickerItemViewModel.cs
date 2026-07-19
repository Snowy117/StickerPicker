using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class StickerItemViewModel(Sticker sticker, double tileSize, ICommand selectCommand) : ViewModelBase
{
    public Sticker Sticker { get; } = sticker;
    public string RelativePath { get; } = sticker.RelativePath;
    public string AbsolutePath { get; } = sticker.AbsolutePath;
    public string FileName { get; } = sticker.FileName;
    public string CategoryId { get; } = sticker.CategoryId;
    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    public partial double TileSize { get; set; } = tileSize;

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; } = TryLoadThumbnail(sticker.AbsolutePath, (int)tileSize);

    private static Bitmap? TryLoadThumbnail(string path, int decodeSize)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, Math.Clamp(decodeSize, 32, 512));
        }
        catch
        {
            return null;
        }
    }
}
