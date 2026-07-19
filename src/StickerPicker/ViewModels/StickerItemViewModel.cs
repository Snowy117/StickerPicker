using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class StickerItemViewModel : ViewModelBase
{
    public StickerItemViewModel(Sticker sticker, double tileSize, ICommand selectCommand)
    {
        Sticker = sticker;
        RelativePath = sticker.RelativePath;
        AbsolutePath = sticker.AbsolutePath;
        FileName = sticker.FileName;
        CategoryId = sticker.CategoryId;
        TileSize = tileSize;
        SelectCommand = selectCommand;
        Thumbnail = TryLoadThumbnail(sticker.AbsolutePath, (int)tileSize);
    }

    public Sticker Sticker { get; }
    public string RelativePath { get; }
    public string AbsolutePath { get; }
    public string FileName { get; }
    public string CategoryId { get; }
    public ICommand SelectCommand { get; }

    [ObservableProperty]
    public partial double TileSize { get; set; }

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

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
