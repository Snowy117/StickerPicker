using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

public static class ServiceFactory
{
    public static IClipboardImageService CreateClipboard()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Platform.Windows.WindowsClipboardImageService();
        }

        return new NullClipboardImageService();
    }

    public static IHotkeyService CreateHotkey()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Platform.Windows.WindowsHotkeyService();
        }

        return new NullHotkeyService();
    }
}
