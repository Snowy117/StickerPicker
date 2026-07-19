using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

/// <summary>No-op hotkey service for non-Windows hosts.</summary>
public sealed class NullHotkeyService : IHotkeyService
{
    public event EventHandler? HotkeyPressed
    {
        add
        {
            // No subscribers needed on non-Windows hosts.
        }
        remove
        {
            // No subscribers needed on non-Windows hosts.
        }
    }

    public bool Register(string hotkeyGesture) => false;

    public void Unregister()
    {
        // No-op: nothing registered.
    }

    public void Dispose()
    {
        // No-op: nothing to release.
    }
}
