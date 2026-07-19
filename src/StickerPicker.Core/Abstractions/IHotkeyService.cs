namespace StickerPicker.Core.Abstractions;

/// <summary>
/// Platform seam: global hotkey registration.
/// </summary>
public interface IHotkeyService : IDisposable
{
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Register or re-register the hotkey (e.g. "Ctrl+Shift+E").
    /// Returns false if registration failed (conflict / unsupported).
    /// </summary>
    bool Register(string hotkeyGesture);

    void Unregister();
}
