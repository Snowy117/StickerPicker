using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

/// <summary>Fallback when OS clipboard dual-format is unavailable.</summary>
public sealed class NullClipboardImageService : IClipboardImageService
{
    public bool CopyImageFile(string absolutePath) => false;
}
