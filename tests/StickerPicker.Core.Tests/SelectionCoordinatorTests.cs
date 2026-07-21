using StickerPicker.Core.Abstractions;
using StickerPicker.Services;

namespace StickerPicker.Core.Tests;

public sealed class SelectionCoordinatorTests
{
    [Fact]
    public async Task SelectAsync_AltSuppressesPasteButRestoresFocusAndHides()
    {
        var fixture = new Fixture();

        var result = await fixture.Coordinator.SelectAsync(Fixture.Request(altHeld: true, autoPaste: true));

        Assert.True(result.Succeeded);
        Assert.True(result.HideWindow);
        Assert.Equal(1, fixture.Window.HideCount);
        Assert.Equal((true, false), fixture.Foreground.LastAction);
    }

    [Fact]
    public async Task SelectAsync_CopyFailurePreservesTargetForRetry()
    {
        var fixture = new Fixture();
        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: false, RecoveryActive: false));
        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: true, RecoveryActive: false));

        var failed = await fixture.Coordinator.SelectAsync(Fixture.Request(autoPaste: true));
        var retried = await fixture.Coordinator.SelectAsync(Fixture.Request(autoPaste: true));

        Assert.False(failed.Succeeded);
        Assert.True(retried.Succeeded);
        Assert.Equal(1, fixture.Foreground.ConsumeCount);
    }

    [Fact]
    public async Task SelectAsync_KeepOpenDoesNotDependOnForegroundTarget()
    {
        var fixture = new Fixture();

        var result = await fixture.Coordinator.SelectAsync(Fixture.Request(keepOpen: true));

        Assert.False(result.HideWindow);
        Assert.Equal(0, fixture.Window.HideCount);
        Assert.Equal(1, fixture.Foreground.ConsumeCount);
        Assert.Equal((false, false), fixture.Foreground.LastAction);
    }

    [Fact]
    public async Task Recovery_ConsecutiveSelectionRestartsFullDelay()
    {
        var fixture = new Fixture();
        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: true, RecoveryActive: true));
        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: true, RecoveryActive: true));

        await fixture.Coordinator.SelectAsync(Fixture.Request(delay: 10));
        fixture.Clock.Advance(TimeSpan.FromSeconds(8));
        await fixture.Coordinator.SelectAsync(Fixture.Request(delay: 10));
        fixture.Clock.Advance(TimeSpan.FromSeconds(9));

        Assert.False(fixture.Coordinator.RestoreIfDue(out _));
        fixture.Clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(fixture.Coordinator.RestoreIfDue(out var restored));
        Assert.True(restored);
        Assert.Equal(1, fixture.Clipboard.RestoreCount);
    }

    [Fact]
    public async Task Recovery_DisableAndExternalInvalidationCancelPresentation()
    {
        var fixture = new Fixture();
        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: true, RecoveryActive: true));
        await fixture.Coordinator.SelectAsync(Fixture.Request(delay: 10));

        fixture.Coordinator.InvalidateRecovery();
        Assert.False(fixture.Coordinator.IsRecoveryPending);

        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: true, RecoveryActive: true));
        await fixture.Coordinator.SelectAsync(Fixture.Request(delay: 10));
        fixture.Coordinator.CancelRecovery();
        Assert.False(fixture.Coordinator.IsRecoveryPending);
        Assert.Equal(1, fixture.Clipboard.CancelCount);
    }

    [Fact]
    public async Task Recovery_InvalidatedWhileForegroundActionIsPending_DoesNotRestart()
    {
        var fixture = new Fixture();
        fixture.Clipboard.CopyResults.Enqueue(new(Succeeded: true, RecoveryActive: true));
        fixture.Foreground.DelayCompletion = true;

        var selection = fixture.Coordinator.SelectAsync(Fixture.Request(autoPaste: true, delay: 10));
        Assert.True(fixture.Coordinator.IsRecoveryPending);

        fixture.Coordinator.InvalidateRecovery();
        fixture.Foreground.CompletePendingAction();
        var result = await selection;

        Assert.False(result.RecoveryPending);
        Assert.False(fixture.Coordinator.IsRecoveryPending);
    }

    private sealed class Fixture
    {
        public FakeClipboard Clipboard { get; } = new();
        public FakeForeground Foreground { get; } = new();
        public FakeWindowChrome Window { get; } = new();
        public FakeClock Clock { get; } = new();
        public SelectionCoordinator Coordinator { get; }

        public Fixture() => Coordinator = new(Clipboard, Foreground, Window, Clock);

        public static SelectionRequest Request(
            bool altHeld = false,
            bool autoPaste = false,
            bool keepOpen = false,
            int delay = 0) =>
            new("/tmp/sticker.png", "sticker.png", altHeld, autoPaste, keepOpen, delay);
    }

    private sealed class FakeClipboard : IClipboardImageService
    {
        public Queue<ClipboardCopyResult> CopyResults { get; } = new();
        public int RestoreCount { get; private set; }
        public int CancelCount { get; private set; }
        public event EventHandler? RecoveryInvalidated
        {
            add { }
            remove { }
        }

        public ClipboardCopyResult CopyImageFile(string absolutePath, bool requestRecovery)
        {
            LastPath = absolutePath;
            LastRecoveryRequested = requestRecovery;
            return CopyResults.TryDequeue(out var result)
                ? result
                : new ClipboardCopyResult(Succeeded: true, RecoveryActive: false);
        }

        public string? LastPath { get; private set; }
        public bool LastRecoveryRequested { get; private set; }

        public bool TryRestoreRecovery()
        {
            RestoreCount++;
            return true;
        }

        public void CancelRecovery() => CancelCount++;
        public void Dispose() { }
    }

    private sealed class FakeForeground : IForegroundInputService
    {
        private TaskCompletionSource<ForegroundActionResult>? _pendingAction;
        public int ConsumeCount { get; private set; }
        public (bool Restore, bool Paste) LastAction { get; private set; }
        public bool DelayCompletion { get; set; }
        public void CaptureTarget() { }
        public void InvalidateTarget() { }

        public void CompletePendingAction() => _pendingAction?.SetResult(CreateResult());

        public Task<ForegroundActionResult> ConsumeTargetAsync(
            bool restoreFocus,
            bool sendPaste,
            CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            ConsumeCount++;
            LastAction = (restoreFocus, sendPaste);
            if (DelayCompletion)
            {
                _pendingAction = new TaskCompletionSource<ForegroundActionResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return _pendingAction.Task;
            }

            return Task.FromResult(CreateResult());
        }

        private ForegroundActionResult CreateResult() => new(
                HadTarget: true,
                FocusRestored: LastAction.Restore,
                PasteSent: LastAction.Paste);
    }

    private sealed class FakeClock : TimeProvider
    {
        private long _timestamp;

        public override long GetTimestamp() => _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public void Advance(TimeSpan amount) => _timestamp += amount.Ticks;
    }

    private sealed class FakeWindowChrome : IWindowChromeService
    {
        public bool IsVisible => true;
        public bool IsActive => true;
        public int HideCount { get; private set; }
        public void Show() { }
        public void Hide() => HideCount++;
        public void Activate() { }
        public void ToggleVisible() { }
        public void SetTopmost(bool topmost) => _ = topmost;
        public void Shutdown() { }
    }
}
