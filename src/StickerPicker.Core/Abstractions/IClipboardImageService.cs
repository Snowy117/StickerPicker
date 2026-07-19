namespace StickerPicker.Core.Abstractions;

/// <summary>
/// Platform seam: copy a local image file to the system clipboard for chat paste.
/// </summary>
public interface IClipboardImageService
{
    /// <summary>
    /// Writes file-drop + bitmap formats when supported. Returns false on failure.
    /// </summary>
    bool CopyImageFile(string absolutePath);
}
