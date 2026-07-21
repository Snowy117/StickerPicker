# 自动粘贴与剪贴板恢复：技术设计

## 1. Architecture

The feature remains a single cross-layer task because selection, platform clipboard mutation, foreground restoration, configuration, and countdown UI form one user-visible transaction. Splitting them into independently shipped child tasks would leave unsafe intermediate states.

Keep Win32 out of ViewModels by deepening two platform seams:

- `IClipboardImageService` owns the complete clipboard transaction and its single active recovery chain: determine whether the current contents are a reusable StickerPicker write, capture an all-or-nothing snapshot when needed, write the selected image plus a private marker, monitor invalidation, verify later ownership, restore, and release native resources.
- A new foreground-input seam owns hotkey-time target capture, target invalidation, foreground restoration, and paste injection. Its Windows adapter uses `GetForegroundWindow`, `SetForegroundWindow`, `GetAsyncKeyState`, and `SendInput`; its null adapter never produces a target or sends input.
- `MainViewModel` owns product orchestration only: select, clear search, decide hide/keep-open, request optional focus/paste, schedule one countdown, and expose countdown presentation state.
- `SettingsViewModel` owns immediate mutual exclusion while `ConfigStore` owns persisted normalization and range validation.

## 2. Configuration Contract

Extend `AppConfig` with:

- `AutoPaste` (`bool`, default `false`)
- `ClipboardRestoreDelaySeconds` (`int`, default `0`, normalized to `0..60`)
- `KeepWindowOpenAfterSelection` (`bool`, default `false`)

`Clone` must include all fields. `ConfigStore.MergeDefaults` normalizes conflicts: when both booleans are true, retain `KeepWindowOpenAfterSelection`, clear `AutoPaste`, and persist the normalized result on load. A load path that changes normalization must write once without recursively loading. Existing JSON remains compatible because omitted properties receive defaults; no reflection serialization is introduced.

The settings ViewModel updates the backing config and persists immediately. Enabling either mutually exclusive setting clears the other property in one guarded update, not via recursive property-change callbacks.

## 3. Selection Data Flow

### Hotkey and tray entry

1. On a global-hotkey press, if StickerPicker is not both visible and active, capture the current external foreground window immediately before showing/activating StickerPicker, then surface the picker.
2. If the hotkey hides an active picker, invalidate the target.
3. Tray show, tray settings, and initial startup invalidate any target before showing. A historical handle is never reused.

### Sticker selection

The tile passes whether `Alt` is held as part of the selection request; platform code does not query stale modifier state later.

1. Ask the clipboard module to copy the image with restoration requested only when the configured delay is nonzero.
2. On copy failure, leave search, window, and focus target unchanged so the user can retry. If clipboard mutation invalidated an older recovery chain, cancel that chain and its countdown; report the failure.
3. On success, clear search every time.
4. If an earlier restoration chain was reusable, retain its original snapshot; otherwise dispose the old chain and attempt a fresh complete snapshot before replacing clipboard contents.
5. Hide StickerPicker unless `KeepWindowOpenAfterSelection` is true.
6. Consume the one-round focus target. When `AutoPaste` is enabled and a valid hotkey target exists, request focus restoration even if `Alt` suppresses paste. Send paste only when `Alt` is not held and the intended target is confirmed as foreground. When `AutoPaste` is disabled, do not explicitly change focus.
7. If the copy result says a recovery chain is active, start/restart the complete delay. Otherwise stop countdown presentation and show the skip reason without turning copy into a failure.

The target is consumed by the first successful selection. Failed selection does not consume it, so the user can retry.

## 4. Clipboard Transaction Contract

### Result and active-chain ownership

The clipboard seam stays deep and stateful because the product permits exactly one active recovery chain. It exposes a small operation-oriented interface equivalent to:

- copy an image with restoration enabled/disabled and return a structured outcome;
- attempt to restore the internally owned active chain if unchanged;
- cancel/dispose the active chain.

The interface also exposes one recovery-state notification so the ViewModel can remove countdown presentation when an external clipboard update invalidates the internally owned chain. It does not expose raw sequence numbers, markers, handles, or snapshot data.

The copy outcome contains:

- copy success/failure
- whether a recovery chain is now active
- optional non-fatal reason why restoration was skipped

The adapter internally owns the original snapshot, operation marker value, and post-write sequence number. Callers cannot inspect or manufacture these values. Replacing, canceling, completing, disabling restoration, or shutting down disposes the chain. If the delay setting becomes zero while a chain is active, cancel it immediately; changing one nonzero delay to another affects subsequent selections rather than retroactively moving the current deadline.

### Snapshot eligibility

- Open the clipboard with bounded retry.
- If the active chain's marker and sequence number still match, reuse its original snapshot for a consecutive selection.
- Otherwise invalidate/dispose the old chain and enumerate the current clipboard from zero.
- Empty clipboard produces an eligible empty snapshot.
- Every advertised/materialized format must be copied successfully in enumeration order. Any unsupported ownership type, null/unrenderable handle, enumeration error, allocation error, or size-limit breach invalidates the entire snapshot.
- Maximum snapshot size is 64 MiB per format and 128 MiB total, using checked arithmetic.
- Explicitly classify standard formats. Copy only formats with a proven duplication/recreation contract; do not treat arbitrary handles as `HGLOBAL`. Registered formats (`0xC000..0xFFFF`) may use `HGLOBAL` after successful `GlobalSize`/`GlobalLock` validation. Unsupported owner-display, `CF_PRIVATE*`, `CF_GDIOBJ*`, delayed rendering, or unsafe GDI/metafile formats cause a conservative skip.

### Sticker write and verification

- Snapshot happens before `EmptyClipboard`, but snapshot ineligibility never blocks the sticker copy.
- Open the clipboard with a real hidden/native owner window rather than `HWND.NULL`; after `EmptyClipboard`, Windows must have a valid owner before `SetClipboardData` calls.
- Register the hidden owner window with `AddClipboardFormatListener`. On `WM_CLIPBOARDUPDATE`, an active chain whose expected post-write sequence no longer equals the current sequence is disposed immediately and emits the recovery-state notification. Notifications caused by StickerPicker's own completed write retain the chain because the sequence matches.
- Write `CF_HDROP`, available `CF_DIB`, and a registered StickerPicker marker containing a fresh cryptographic operation value.
- Count copy as success only if at least one consumer image format is successfully transferred. Ownership of each successfully transferred handle passes to Windows; failed handles are freed locally.
- Record `GetClipboardSequenceNumber` after all formats are written and the clipboard is closed. A zero sequence number makes restoration ineligible.

### Restore

At expiry, open the clipboard and require both:

1. current sequence number equals the active chain's post-write sequence number; and
2. the private marker exists and exactly matches the active chain's operation value.

Any mismatch cancels with no mutation. Before `EmptyClipboard`, prepare all native handles needed for the old snapshot. Then empty once and transfer every format in original order. An empty snapshot restores by emptying only. Always dispose the chain after success, mismatch, failure, explicit cancellation, replacement, or shutdown.

## 5. Foreground and Input Contract

- Capture only a non-null, valid external top-level window; never capture StickerPicker's own native window.
- A captured target is one-round state and is invalidated by non-hotkey entry paths.
- After selection, validate `IsWindow`, request `SetForegroundWindow`, and confirm `GetForegroundWindow` equals the target before input injection.
- Do not send input when activation fails; otherwise input could reach an unrelated application.
- Paste uses balanced `Ctrl` down, `V` down, `V` up, `Ctrl` up events. Before sending, wait for physical Ctrl/Shift/Alt/Win modifiers to be released only within a small bounded asynchronous window; never block the UI thread indefinitely.
- `SendInput` zero insertion and UIPI blocking are non-fatal paste failures. A partial return means a serial prefix was inserted: immediately send key-up cleanup for any synthetic keys left down by that prefix, in reverse order, without adding further key-down events. Physical modifiers were confirmed released before injection, so this cleanup cannot release a user-held modifier. Cleanup is bounded to one attempt and any failure is reported. Clipboard copy and restoration scheduling remain valid.

## 6. Countdown and UI

- Maintain exactly one cancellation source/timer corresponding to the clipboard module's active restore chain.
- Expose `IsClipboardRestorePending`, remaining seconds text/value, and progress fraction/maximum. Update at a short UI cadence while deriving remaining time from a monotonic deadline, not decrementing a counter per tick.
- The right side of the status bar shows a compact progress bar plus remaining seconds only while active. Cancellation, mismatch, failure, success, or shutdown removes it immediately.
- Make the settings overlay card height constrained to the current window with a stable margin. Keep its header docked; wrap only the content `StackPanel` in a vertical `ScrollViewer`, with horizontal scrolling disabled.
- Settings controls: checkboxes for the two booleans and an integer numeric control for `0..60`; `0` visibly means disabled through the value/label, without adding another boolean.

## 7. Failure and Compatibility

- Auto-paste is best effort due to Windows foreground policy and UIPI. Never compensate with unbounded delays, focus-stealing loops, or input sent before target verification.
- Snapshot refusal, size limits, and external clipboard changes are non-fatal. They never prevent the selected sticker from being copied.
- Clipboard restore failure after mutation is reported, but no partial-format promise is made; native handle preparation before emptying minimizes this case.
- Defaults (`AutoPaste=false`, delay `0`, keep-open `false`) preserve copy-and-hide behavior, with the intentional new requirement that successful selections clear search.
- Non-Windows null adapters retain graceful copy failure/no automatic input behavior.

## 8. Verification Strategy

Automated tests cover config defaults, round-trip, clamping, conflict normalization/persistence, and a pure desktop selection/countdown coordinator through fake clipboard, foreground-input, clock, and window-action seams. Coordinator cases include Alt suppression, target consumption, copy-failure retry, keep-open behavior, delay disable/cancellation, consecutive restart, and external invalidation. Platform adapters require Windows manual/integration verification with text, HTML/RTF, empty, unsupported/oversize, consecutive selection, external clipboard change, independent marker/sequence mismatch, invalid target, partial-input cleanup, elevated target, and common QQ/WeChat paste scenarios.

Release validation retains NativeAOT build/publish because new persisted fields and P/Invoke structs must remain analyzable.

