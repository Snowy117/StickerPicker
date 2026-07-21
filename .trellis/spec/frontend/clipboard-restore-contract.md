# Selection, Clipboard Restore, and Auto-Paste Contract

> Executable contract for the sticker-selection transaction that copies an
> image, optionally restores the previous clipboard, optionally focuses the
> original window and pastes, and shows a countdown.

This is **code-spec**: signatures, contracts, validation/error behavior, and
test points. It is the authoritative reference for the three platform seams
(`IClipboardImageService`, `IForegroundInputService`) and the pure
`SelectionCoordinator` introduced by the `auto-paste-clipboard-restore` task.

---

## 1. Scope / Trigger

Apply this contract whenever you change:

- the clipboard service interface or its Windows adapter
- the foreground-input service or its Windows adapter
- the selection command / `SelectionCoordinator` orchestration
- selection-related settings (`AutoPaste`, `ClipboardRestoreDelaySeconds`,
  `KeepWindowOpenAfterSelection`)
- the status-bar countdown display

The contract exists because clipboard mutation + input injection + focus
restoration is the highest-risk surface in the app. Violating any invariant
below can **silently corrupt the user's clipboard** or **send keystrokes into
the wrong application**.

---

## 2. Signatures

### `IClipboardImageService` (Core seam, `IDisposable`)

```csharp
public sealed record ClipboardCopyResult(
    bool Succeeded,
    bool RecoveryActive,
    string? RecoverySkipReason = null);

public interface IClipboardImageService : IDisposable
{
    event EventHandler? RecoveryInvalidated;

    ClipboardCopyResult CopyImageFile(string absolutePath, bool requestRecovery);
    bool TryRestoreRecovery();
    void CancelRecovery();
}
```

### `IForegroundInputService` (Core seam)

```csharp
public sealed record ForegroundActionResult(
    bool HadTarget,
    bool FocusRestored,
    bool PasteSent,
    string? FailureReason = null);

public interface IForegroundInputService
{
    void CaptureTarget();
    void InvalidateTarget();
    Task<ForegroundActionResult> ConsumeTargetAsync(
        bool restoreFocus,
        bool sendPaste,
        CancellationToken cancellationToken = default);
}
```

### `SelectionCoordinator` (host-side, pure)

```csharp
public sealed record SelectionRequest(
    string AbsolutePath, string FileName, bool AltHeld,
    bool AutoPaste, bool KeepWindowOpen, int RestoreDelaySeconds);

public sealed record SelectionResult(
    bool Succeeded, bool HideWindow, string Status,
    string? Error, bool RecoveryPending);

public sealed class SelectionCoordinator(
    IClipboardImageService clipboard,
    IForegroundInputService foreground,
    IWindowChromeService windowChrome,
    TimeProvider timeProvider);
```

---

## 3. Contracts

### Selection transaction ordering (mandatory)

Every successful selection, in this exact order:

1. `CopyImageFile(path, requestRecovery: delay > 0)`.
2. On **copy failure**: do NOT clear search, hide window, or consume the
   target. If the failure also invalidated a prior recovery chain, cancel
   that chain and its countdown. Report the failure. **The user must be able
   to retry.**
3. On success: **clear search every time** (regardless of settings).
4. Snapshot reuse: if the active chain's marker+sequence still match, keep
   its original snapshot; otherwise attempt a fresh complete snapshot before
   replacing clipboard contents.
5. Hide the window unless `KeepWindowOpenAfterSelection` is true — this
   decision is **independent** of whether a paste target exists.
6. `ConsumeTargetAsync(restoreFocus: AutoPaste, sendPaste: AutoPaste && !AltHeld)`.
   - The target is consumed only on **successful** copy.
   - `AutoPaste` off → do not explicitly change focus.
   - `Alt` held → skip `Ctrl+V` but still restore focus when `AutoPaste` is on.
7. If `RecoveryActive`, start/restart the full-delay countdown; otherwise
   clear countdown presentation and surface `RecoverySkipReason` (non-fatal).

### Clipboard recovery chain (single, owned by the adapter)

- At most **one** active chain at a time. Replacing, cancelling, completing,
  disabling recovery, or shutdown disposes it.
- Consecutive selection: if the clipboard still holds the previous
  StickerPicker write (marker + post-write sequence match), **reuse the
  original snapshot** and restart the countdown. Never snapshot the prior
  sticker as the "original content."
- External change: `WM_CLIPBOARDUPDATE` (via `AddClipboardFormatListener`
  on the hidden owner window) whose sequence ≠ expected sequence disposes
  the chain immediately and raises `RecoveryInvalidated`. The chain's own
  completed write does **not** invalidate (sequence matches).
- Expiry restore requires **both**: (a) current sequence == post-write
  sequence, AND (b) registered marker exists and exactly matches the
  operation value. Any mismatch cancels with **no mutation**.
- Before `EmptyClipboard` at restore time, **prebuild every native handle**;
  then empty once and transfer in original order. Empty snapshot restores by
  emptying only.

### Snapshot eligibility (all-or-nothing)

- Enumerate from zero; preserve order including synthesized/materialized
  formats. Any enumeration error, copy error, allocation error, or size
  breach invalidates the **entire** snapshot.
- Memory-backed allowlist only (copy as bytes): CF 1,4,5,6,7,8,10,11,12,13,
  15,16,17 and registered formats `0xC000..0xFFFF` after successful
  `GlobalSize`/`GlobalLock`.
- **Conservatively reject** (whole snapshot ineligible): `CF_OWNERDISPLAY`,
  `CF_PRIVATE*`, `CF_GDIOBJ*`, GDI/metafile formats (`CF_BITMAP`,
  `CF_PALETTE`, `CF_METAFILEPICT`, `CF_ENHMETAFILE` and display variants),
  `CF_DSPTEXT`, delayed-rendering (null handle), or any unrecognized
  ownership contract.
- Size limits (checked arithmetic): 64 MiB per format, 128 MiB total.
  Exceeding either skips recovery (non-fatal; the sticker is still copied).
- Empty clipboard is an eligible empty snapshot.

### Foreground / input injection (safety invariants)

- Capture only a non-null, valid **external** top-level window — never
  StickerPicker's own native window.
- Target is **one-round** state; invalidated by tray show, tray settings,
  startup, window close, and hotkey-hide.
- Before paste: validate `IsWindow`, call `SetForegroundWindow`, then
  confirm `GetForegroundWindow == target`. **Never** inject input if
  activation fails — it could reach an unrelated application.
- Paste = balanced `Ctrl↓, V↓, V↑, Ctrl↑`. Before sending, wait (bounded,
  async, never blocking the UI thread) for physical Ctrl/Shift/Alt/Win to
  be released.
- Partial `SendInput` return (a serial prefix was inserted): send key-up
  cleanup **only** for synthetic keys left down by the prefix, in reverse
  order, with no new key-down events. Bounded to one attempt; failure is
  reported distinctly. (Physical modifiers were confirmed released first,
  so cleanup cannot release a user-held key.)
- `SendInput` returning zero or blocked by UIPI is a **non-fatal paste
  failure** — copy and recovery scheduling remain valid.

### Countdown

- Exactly one cancellation source/timer for the active chain.
- Derive remaining time from a **monotonic** `TimeProvider` deadline
  (`GetTimestamp` / `TimestampFrequency` / `GetElapsedTime`), not from
  decrementing a counter per tick. `DateTimeOffset.UtcNow` drifts under
  wall-clock adjustments — do not use it.
- Status bar shows compact progress + remaining seconds only while active;
  removed immediately on cancel, mismatch, failure, success, or shutdown.

---

## 4. Validation & Error Matrix

| Condition | Required outcome |
|---|---|
| Copy succeeds, delay > 0, snapshot eligible | RecoveryActive=true; start/restart full countdown |
| Copy succeeds, snapshot ineligible (unsupported/oversize) | RecoveryActive=false; surface skip reason; **no** countdown; sticker still copied |
| Copy succeeds, `RecoveryInvalidated` fires mid-countdown | Clear countdown immediately; release snapshot |
| Expiry, marker OR sequence mismatch | Cancel, no clipboard mutation, dispose chain |
| Expiry, both match | Restore original (or empty) snapshot; dispose chain |
| External clipboard change during countdown | Chain disposed on `WM_CLIPBOARDUPDATE`; countdown hidden |
| `AddClipboardFormatListener` registration failed | Recovery conservatively skipped (never falsely promised) |
| Copy fails | No search clear / hide / target consume; retry allowed |
| Invalid/closed/elevated target, or activation refused | No input injected; paste reported as failed; copy + recovery remain valid |
| Partial `SendInput` prefix | Reverse key-up cleanup once; report on failure |
| Delay setting → 0 while chain active | Cancel chain immediately |
| Non-Windows / null adapters | Graceful copy failure; no automatic input |

---

## 5. Good / Base / Bad Cases

- **Good:** hotkey from a chat app → select sticker → focus restored, paste
  sent, search cleared, recovery countdown shown, clipboard restored at
  expiry if unchanged.
- **Base:** `AutoPaste=false`, `delay=0`, `keep-open=false` → existing
  copy-and-hide, with the new requirement that search is cleared.
- **Bad:** injecting `Ctrl+V` without confirming `GetForegroundWindow ==
  target` — input may land in whatever window gained focus, including an
  elevated or unrelated app.

---

## 6. Tests Required

Core config (`ConfigStoreTests`):

- defaults for the three new fields
- round-trip
- delay clamped to `0..60`
- conflict normalization (both true → keep-open, autoPaste false) **and**
  that the normalized result is persisted on load
- old JSON without the new properties receives defaults

Selection coordinator (`SelectionCoordinatorTests`, with fake clipboard /
foreground / clock / window seams):

- Alt suppresses paste but focus restore still occurs
- copy failure preserves the target for retry
- keep-open is independent of target presence
- consecutive selection reuses the first snapshot and restarts countdown
- external invalidation cancels countdown
- delay disable cancels the chain
- recovery invalidated while a foreground call is pending does not restart

Platform adapters require **manual Windows verification** (cannot run on a
non-Windows host): text / HTML / RTF / file-list / empty / unsupported /
oversize snapshots; consecutive selection; external change cancellation;
independent marker-vs-sequence mismatch; invalid / closed / elevated target;
fault-injected partial `SendInput` cleanup; QQ/WeChat paste; settings scroll
at minimum window height; countdown appears/updates/restarts/disappears;
`win-x64` NativeAOT publish.

---

## 7. Wrong vs Correct

### Wrong — ViewModel calls Win32 directly

```csharp
// MainViewModel
OpenClipboard(IntPtr.Zero);            // NULL owner → SetClipboardData fails
SendInput(...);                        // no target verification
```

`HWND.NULL` as clipboard owner makes `SetClipboardData` fail after
`EmptyClipboard`. Unverified `SendInput` can paste into the wrong app.

### Correct — deep seams, verified target

```csharp
var copy = _clipboard.CopyImageFile(path, requestRecovery: delay > 0);
// adapter opens clipboard with its real hidden owner window,
// writes CF_HDROP + CF_DIB + registered marker, records sequence number
await _foreground.ConsumeTargetAsync(
    restoreFocus: autoPaste,
    sendPaste: autoPaste && !altHeld);
// adapter: SetForegroundWindow → confirm GetForegroundWindow == target → SendInput
```

### Wrong — datetime-based countdown

```csharp
_deadline = DateTimeOffset.UtcNow.AddSeconds(delay);   // drifts on wall-clock change
```

### Correct — monotonic deadline

```csharp
_deadlineTimestamp = checked(
    timeProvider.GetTimestamp()
    + timeProvider.TimestampFrequency * delay);
```
