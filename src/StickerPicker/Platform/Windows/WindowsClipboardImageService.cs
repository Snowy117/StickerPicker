using System.Runtime.Versioning;
using System.Security.Cryptography;
using StickerPicker.Core.Abstractions;

namespace StickerPicker.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsClipboardImageService : IClipboardImageService
{
    private const uint CfHdrop = 15;
    private const uint CfDib = 8;
    private const int MarkerLength = 16;
    private readonly Lock _gate = new();
    private readonly IntPtr _ownerWindow;
    private readonly uint _markerFormat;
    private readonly bool _canMonitorRecovery;
    private RecoveryChain? _chain;
    private bool _disposed;

    public WindowsClipboardImageService()
    {
        _ownerWindow = ClipboardNativeWindow.Ensure(OnClipboardUpdated);
        _markerFormat = NativeMethods.RegisterClipboardFormat("StickerPicker.RecoveryMarker.v1");
        _canMonitorRecovery = ClipboardNativeWindow.IsListening;
    }

    public event EventHandler? RecoveryInvalidated;

    public ClipboardCopyResult CopyImageFile(string absolutePath, bool requestRecovery)
    {
        if (_disposed || !OperatingSystem.IsWindows() || !File.Exists(absolutePath)
            || _ownerWindow == IntPtr.Zero)
        {
            return new ClipboardCopyResult(Succeeded: false, RecoveryActive: false);
        }

        var fullPath = Path.GetFullPath(absolutePath);
        lock (_gate)
        {
            return CopyLocked(fullPath, requestRecovery);
        }
    }

    public bool TryRestoreRecovery()
    {
        bool restored;
        lock (_gate)
        {
            restored = TryRestoreLocked();
            ClearChainLocked();
        }

        RecoveryInvalidated?.Invoke(this, EventArgs.Empty);
        return restored;
    }

    public void CancelRecovery()
    {
        bool hadChain;
        lock (_gate)
        {
            hadChain = _chain is not null;
            ClearChainLocked();
        }

        if (hadChain)
        {
            RecoveryInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelRecovery();
        ClipboardNativeWindow.Detach(OnClipboardUpdated);
    }

    private ClipboardCopyResult CopyLocked(string fullPath, bool requestRecovery)
    {
        if (!NativeMethods.TryOpenClipboard(_ownerWindow))
        {
            return new ClipboardCopyResult(Succeeded: false, RecoveryActive: _chain is not null);
        }

        var (snapshot, skipReason, canMarkForRecovery) = PrepareRecoverySnapshotLocked(requestRecovery);

        var marker = canMarkForRecovery ? RandomNumberGenerator.GetBytes(MarkerLength) : [];
        var copySucceeded = false;
        var markerWritten = false;
        try
        {
            copySucceeded = WriteSticker(fullPath, marker, out markerWritten);
        }
        finally
        {
            _ = NativeMethods.CloseClipboard();
        }

        if (!copySucceeded)
        {
            return new ClipboardCopyResult(Succeeded: false, RecoveryActive: false);
        }

        var sequence = NativeMethods.GetClipboardSequenceNumber();
        if (requestRecovery && snapshot is not null && markerWritten && sequence != 0)
        {
            _chain = new RecoveryChain(snapshot, marker, sequence);
            return new ClipboardCopyResult(Succeeded: true, RecoveryActive: true);
        }

        if (requestRecovery && skipReason is null)
        {
            skipReason = !markerWritten
                ? "无法写入恢复标记，本次已跳过剪贴板恢复。"
                : "无法确认剪贴板序列号，本次已跳过剪贴板恢复。";
        }

        return new ClipboardCopyResult(
            Succeeded: true,
            RecoveryActive: false,
            RecoverySkipReason: skipReason);
    }

    private (ClipboardSnapshot? Snapshot, string? SkipReason, bool CanMark) PrepareRecoverySnapshotLocked(
        bool requestRecovery)
    {
        var canMark = requestRecovery && _markerFormat != 0 && _canMonitorRecovery;
        if (canMark && IsChainCurrentLocked())
        {
            var originalSnapshot = _chain?.Snapshot;
            _chain = null;
            return (originalSnapshot, null, true);
        }

        ClearChainLocked();
        if (canMark)
        {
            var (snapshot, reason) = ClipboardSnapshot.TryCapture();
            return (snapshot, reason, true);
        }

        var skipReason = requestRecovery
            ? GetRecoveryUnavailableReason()
            : null;
        return (null, skipReason, false);
    }

    private string GetRecoveryUnavailableReason() => !_canMonitorRecovery
        ? "无法监听剪贴板变化，本次已跳过剪贴板恢复。"
        : "无法注册恢复标记，本次已跳过剪贴板恢复。";

    private bool WriteSticker(string fullPath, byte[] marker, out bool markerWritten)
    {
        markerWritten = false;
        var hdrop = ClipboardHandles.CreateHdrop(fullPath);
        var dib = ClipboardHandles.TryCreateDib(fullPath);
        var markerHandle = marker.Length > 0 ? ClipboardHandles.CreateBytes(marker) : IntPtr.Zero;
        if (!NativeMethods.EmptyClipboard())
        {
            ClipboardHandles.Free(hdrop, dib, markerHandle);
            return false;
        }

        var hdropWritten = Transfer(CfHdrop, ref hdrop);
        var dibWritten = Transfer(CfDib, ref dib);
        var copiedConsumerFormat = hdropWritten || dibWritten;
        markerWritten = markerHandle != IntPtr.Zero && Transfer(_markerFormat, ref markerHandle);
        ClipboardHandles.Free(hdrop, dib, markerHandle);
        return copiedConsumerFormat;
    }

    private bool IsChainCurrentLocked()
    {
        if (_chain is null
            || NativeMethods.GetClipboardSequenceNumber() != _chain.ExpectedSequence)
        {
            return false;
        }

        return MarkerMatches(_chain.Marker);
    }

    private bool TryRestoreLocked()
    {
        if (_chain is null || !NativeMethods.TryOpenClipboard(_ownerWindow))
        {
            return false;
        }

        try
        {
            if (NativeMethods.GetClipboardSequenceNumber() != _chain.ExpectedSequence
                || !MarkerMatches(_chain.Marker))
            {
                return false;
            }

            var prepared = _chain.Snapshot.PrepareHandles();
            if (prepared is null)
            {
                return false;
            }

            if (!NativeMethods.EmptyClipboard())
            {
                ClipboardHandles.Free([.. prepared.Select(item => item.Handle)]);
                return false;
            }

            var allTransferred = true;
            foreach (var item in prepared)
            {
                var handle = item.Handle;
                if (!Transfer(item.Format, ref handle))
                {
                    allTransferred = false;
                }

                ClipboardHandles.Free(handle);
            }

            return allTransferred;
        }
        finally
        {
            _ = NativeMethods.CloseClipboard();
        }
    }

    private bool MarkerMatches(byte[] expected)
    {
        var handle = NativeMethods.GetClipboardData(_markerFormat);
        if (handle == IntPtr.Zero || NativeMethods.GlobalSize(handle).ToUInt64() != MarkerLength)
        {
            return false;
        }

        var pointer = NativeMethods.GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var actual = new byte[MarkerLength];
            System.Runtime.InteropServices.Marshal.Copy(pointer, actual, 0, actual.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        finally
        {
            _ = NativeMethods.GlobalUnlock(handle);
        }
    }

    private void OnClipboardUpdated()
    {
        var invalidated = false;
        lock (_gate)
        {
            if (_chain is not null
                && NativeMethods.GetClipboardSequenceNumber() != _chain.ExpectedSequence)
            {
                ClearChainLocked();
                invalidated = true;
            }
        }

        if (invalidated)
        {
            RecoveryInvalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ClearChainLocked() => _chain = null;

    private static bool Transfer(uint format, ref IntPtr handle)
    {
        if (handle == IntPtr.Zero || NativeMethods.SetClipboardData(format, handle) == IntPtr.Zero)
        {
            return false;
        }

        handle = IntPtr.Zero;
        return true;
    }

    private sealed record RecoveryChain(
        ClipboardSnapshot Snapshot,
        byte[] Marker,
        uint ExpectedSequence);
}
