namespace StickerPicker.Core.Abstractions;

/// <summary>
/// UI chrome operations used by hotkey/tray without coupling Core to Avalonia.
/// Implemented in the desktop host.
/// </summary>
public interface IWindowChromeService
{
    bool IsVisible { get; }
    void Show();
    void Hide();
    void Activate();
    void ToggleVisible();
    void SetTopmost(bool topmost);
    void Shutdown();
}
