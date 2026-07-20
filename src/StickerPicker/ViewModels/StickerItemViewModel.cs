using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StickerPicker.Core.Models;

namespace StickerPicker.ViewModels;

public partial class StickerItemViewModel(Sticker sticker, double tileSize, ICommand selectCommand) : ViewModelBase
{
    private const int MinDecodeWidth = 64;
    private const int MaxDecodeWidth = 512;
    private const double DecodeScaleFactor = 1.5;

    private int _decodedWidth = ComputeDecodeWidth(tileSize);

    public Sticker Sticker { get; } = sticker;
    public string RelativePath { get; } = sticker.RelativePath;
    public string AbsolutePath { get; } = sticker.AbsolutePath;
    public string FileName { get; } = sticker.FileName;
    public string CategoryId { get; } = sticker.CategoryId;
    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    public partial double TileSize { get; set; } = tileSize;

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; } = TryLoadThumbnail(sticker.AbsolutePath, ComputeDecodeWidth(tileSize));

    partial void OnTileSizeChanged(double value)
    {
        var target = ComputeDecodeWidth(value);
        if (target > _decodedWidth)
        {
            Thumbnail = TryLoadThumbnail(AbsolutePath, target);
            _decodedWidth = target;
        }
    }

    private static int ComputeDecodeWidth(double tileSize) =>
        Math.Clamp((int)Math.Ceiling(tileSize * DecodeScaleFactor), MinDecodeWidth, MaxDecodeWidth);

    private static Bitmap? TryLoadThumbnail(string path, int decodeSize)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, Math.Clamp(decodeSize, MinDecodeWidth, MaxDecodeWidth));
        }
        catch
        {
            return null;
        }
    }
}
