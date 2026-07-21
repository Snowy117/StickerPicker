using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

public sealed record SelectionRequest(
    string AbsolutePath,
    string FileName,
    bool AltHeld,
    bool AutoPaste,
    bool KeepWindowOpen,
    int RestoreDelaySeconds);

public sealed record SelectionResult(
    bool Succeeded,
    bool HideWindow,
    string Status,
    string? Error,
    bool RecoveryPending);

public sealed class SelectionCoordinator(
    IClipboardImageService clipboard,
    IForegroundInputService foreground,
    IWindowChromeService windowChrome,
    TimeProvider timeProvider)
{
    private readonly IClipboardImageService _clipboard = clipboard;
    private readonly IForegroundInputService _foreground = foreground;
    private readonly IWindowChromeService _windowChrome = windowChrome;
    private readonly TimeProvider _timeProvider = timeProvider;
    private long? _deadlineTimestamp;
    private int _delaySeconds;

    public bool IsRecoveryPending => _deadlineTimestamp is not null;

    public async Task<SelectionResult> SelectAsync(SelectionRequest request)
    {
        var copy = _clipboard.CopyImageFile(request.AbsolutePath, request.RestoreDelaySeconds > 0);
        if (!copy.Succeeded)
        {
            if (!copy.RecoveryActive)
            {
                ClearDeadline();
            }

            return new SelectionResult(
                Succeeded: false,
                HideWindow: false,
                Status: "",
                Error: "复制到剪贴板失败。",
                RecoveryPending: IsRecoveryPending);
        }

        if (copy.RecoveryActive)
        {
            _delaySeconds = request.RestoreDelaySeconds;
            _deadlineTimestamp = checked(
                _timeProvider.GetTimestamp()
                + (_timeProvider.TimestampFrequency * _delaySeconds));
        }
        else
        {
            ClearDeadline();
        }

        if (!request.KeepWindowOpen)
        {
            _windowChrome.Hide();
        }

        var foregroundResult = await _foreground.ConsumeTargetAsync(
            request.AutoPaste,
            request.AutoPaste && !request.AltHeld);

        return new SelectionResult(
            Succeeded: true,
            HideWindow: !request.KeepWindowOpen,
            Status: copy.RecoverySkipReason ?? $"已复制 {request.FileName}",
            Error: foregroundResult.FailureReason,
            RecoveryPending: IsRecoveryPending);
    }

    public (double Progress, string RemainingText) GetProgress()
    {
        if (_deadlineTimestamp is not { } deadline)
        {
            return (0, "");
        }

        var remaining = Math.Max(0, _timeProvider.GetElapsedTime(_timeProvider.GetTimestamp(), deadline).TotalSeconds);
        var remainingText = Math.Ceiling(remaining).ToString(System.Globalization.CultureInfo.InvariantCulture) + " 秒";
        return (Math.Clamp(remaining / Math.Max(1, _delaySeconds), 0, 1), remainingText);
    }

    public bool RestoreIfDue(out bool restored)
    {
        restored = false;
        if (_deadlineTimestamp is not { } deadline
            || _timeProvider.GetTimestamp() < deadline)
        {
            return false;
        }

        restored = _clipboard.TryRestoreRecovery();
        ClearDeadline();
        return true;
    }

    public void InvalidateRecovery() => ClearDeadline();

    public void CancelRecovery()
    {
        ClearDeadline();
        _clipboard.CancelRecovery();
    }

    private void ClearDeadline() => _deadlineTimestamp = null;
}
