using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

public sealed class NullForegroundInputService : IForegroundInputService
{
    public void CaptureTarget()
    {
    }

    public void InvalidateTarget()
    {
    }

    public Task<ForegroundActionResult> ConsumeTargetAsync(
        bool restoreFocus,
        bool sendPaste,
        CancellationToken cancellationToken = default)
    {
        _ = restoreFocus;
        _ = sendPaste;
        _ = cancellationToken;
        return Task.FromResult(new ForegroundActionResult(
            HadTarget: false,
            FocusRestored: false,
            PasteSent: false));
    }
}
