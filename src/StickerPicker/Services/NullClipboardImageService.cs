using StickerPicker.Core.Abstractions;

namespace StickerPicker.Services;

/// <summary>Fallback when OS clipboard dual-format is unavailable.</summary>
public sealed class NullClipboardImageService : IClipboardImageService
{
    public event EventHandler? RecoveryInvalidated;

    public ClipboardCopyResult CopyImageFile(string absolutePath, bool requestRecovery)
    {
        _ = absolutePath;
        _ = requestRecovery;
        return new ClipboardCopyResult(Succeeded: false, RecoveryActive: false);
    }

    public bool TryRestoreRecovery() => false;

    public void CancelRecovery() => RecoveryInvalidated?.Invoke(this, EventArgs.Empty);

    public void Dispose() => CancelRecovery();
}
