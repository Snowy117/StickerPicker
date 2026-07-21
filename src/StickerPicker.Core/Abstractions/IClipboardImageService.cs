namespace StickerPicker.Core.Abstractions;

public sealed record ClipboardCopyResult(
    bool Succeeded,
    bool RecoveryActive,
    string? RecoverySkipReason = null);

/// <summary>Owns the complete image clipboard transaction and one optional recovery chain.</summary>
public interface IClipboardImageService : IDisposable
{
    event EventHandler? RecoveryInvalidated;

    ClipboardCopyResult CopyImageFile(string absolutePath, bool requestRecovery);

    bool TryRestoreRecovery();

    void CancelRecovery();
}
