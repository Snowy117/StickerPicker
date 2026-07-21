namespace StickerPicker.Core.Abstractions;

/// <summary>
/// UI chrome operations used by hotkey/tray without coupling Core to Avalonia.
/// Implemented in the desktop host.
/// </summary>
public interface IWindowChromeService
{
    // Kept as part of the public chrome contract even though no current caller uses it;
    // removing it would force future consumers to depend on the concrete implementation.
    // ReSharper disable once UnusedMember.Global
    bool IsVisible { get; }
    bool IsActive { get; }
    void Show();
    void Hide();
    void Activate();
    void ToggleVisible();
    void SetTopmost(bool topmost);
    void Shutdown();
}
