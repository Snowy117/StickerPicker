using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

/// <summary>No-op hotkey service for non-Windows hosts.</summary>
public sealed class NullHotkeyService : IHotkeyService
{
    public event EventHandler? HotkeyPressed
    {
        add { }
        remove { }
    }

    public bool Register(string hotkeyGesture) => false;

    public void Unregister()
    {
    }

    public void Dispose()
    {
    }
}
