using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StickerPicker.Core.Abstractions;
using StickerPicker.Services;

namespace StickerPicker.ViewModels;

public partial class MainViewModel
{
    private readonly IClipboardImageService _clipboard;
    private readonly IForegroundInputService _foregroundInput;
    private readonly SelectionCoordinator _selection;
    private DispatcherTimer? _restoreTimer;

    [ObservableProperty]
    public partial bool IsClipboardRestorePending { get; set; }

    [ObservableProperty]
    public partial string ClipboardRestoreRemainingText { get; set; } = "";

    [ObservableProperty]
    public partial double ClipboardRestoreProgress { get; set; }

    public void InvalidateAutoPasteTarget() => _foregroundInput.InvalidateTarget();

    private void ShutdownSelection()
    {
        _clipboard.RecoveryInvalidated -= OnRecoveryInvalidated;
        StopRestoreCountdown(cancelClipboard: true);
        _clipboard.Dispose();
    }

    [RelayCommand]
    private async Task SelectStickerAsync((StickerItemViewModel Item, bool AltHeld) request)
    {
        try
        {
            var result = await _selection.SelectAsync(new SelectionRequest(
                request.Item.AbsolutePath,
                request.Item.FileName,
                request.AltHeld,
                _config.AutoPaste,
                _config.KeepWindowOpenAfterSelection,
                _config.ClipboardRestoreDelaySeconds));
            if (!result.Succeeded)
            {
                ErrorMessage = result.Error;
                if (!result.RecoveryPending)
                {
                    StopRestoreCountdown(cancelClipboard: false);
                }
                return;
            }

            SearchText = "";
            StatusText = result.Status;
            ErrorMessage = result.Error;
            if (result.RecoveryPending)
            {
                StartRestoreCountdown();
            }
            else
            {
                StopRestoreCountdown(cancelClipboard: false);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        void ToggleFromHotkey()
        {
            if (!_windowChrome.IsVisible || !_windowChrome.IsActive)
            {
                _foregroundInput.CaptureTarget();
            }
            else
            {
                _foregroundInput.InvalidateTarget();
            }

            _windowChrome.ToggleVisible();
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            ToggleFromHotkey();
        }
        else
        {
            Dispatcher.UIThread.Post(ToggleFromHotkey);
        }
    }

    private void StartRestoreCountdown()
    {
        _restoreTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _restoreTimer.Tick -= OnRestoreTick;
        _restoreTimer.Tick += OnRestoreTick;
        IsClipboardRestorePending = true;
        (ClipboardRestoreProgress, ClipboardRestoreRemainingText) = _selection.GetProgress();
        _restoreTimer.Start();
    }

    private void OnRestoreTick(object? sender, EventArgs e)
    {
        if (_selection.RestoreIfDue(out var restored))
        {
            StopRestoreCountdown(cancelClipboard: false);
            StatusText = restored ? "已恢复剪贴板" : "剪贴板已变化，未恢复";
            return;
        }

        (ClipboardRestoreProgress, ClipboardRestoreRemainingText) = _selection.GetProgress();
    }

    private void OnRecoveryInvalidated(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _selection.InvalidateRecovery();
            StopRestoreCountdown(cancelClipboard: false);
        });

    private void StopRestoreCountdown(bool cancelClipboard)
    {
        _restoreTimer?.Stop();
        IsClipboardRestorePending = false;
        ClipboardRestoreProgress = 0;
        ClipboardRestoreRemainingText = "";
        if (cancelClipboard)
        {
            _selection.CancelRecovery();
        }
    }
}
