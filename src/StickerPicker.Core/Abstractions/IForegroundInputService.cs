namespace StickerPicker.Core.Abstractions;

public sealed record ForegroundActionResult(
    bool HadTarget,
    bool FocusRestored,
    bool PasteSent,
    string? FailureReason = null);

/// <summary>Owns a one-round external foreground target captured at hotkey time.</summary>
public interface IForegroundInputService
{
    void CaptureTarget();

    void InvalidateTarget();

    Task<ForegroundActionResult> ConsumeTargetAsync(
        bool restoreFocus,
        bool sendPaste,
        CancellationToken cancellationToken = default);
}
