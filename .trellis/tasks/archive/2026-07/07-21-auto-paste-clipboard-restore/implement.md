# 自动粘贴与剪贴板恢复：执行计划

## Ordered Implementation

1. Extend and normalize configuration.
   - Add the three persisted fields and clone support.
   - Clamp restore delay and normalize mutual exclusion during load/save; persist changed load results.
   - Add Core tests for defaults, partial old JSON, round-trip, range bounds, and conflicting booleans.
2. Deepen the clipboard seam and adapters.
   - Define structured copy/restore outcomes while keeping the single active recovery chain owned inside the deep clipboard module.
   - Implement all-or-nothing snapshot classification, checked 64/128 MiB limits, empty snapshots, private marker, sequence verification, consecutive-chain reuse, clipboard-update listener invalidation, and deterministic cleanup in the Windows adapter.
   - Update null adapter and composition without leaking Win32 types across the seam.
3. Add the foreground-input seam and adapters.
   - Capture/invalidate one-round targets around hotkey, tray, and startup entry paths.
   - Implement verified foreground restoration and balanced `SendInput` paste with bounded modifier-release handling.
4. Refactor selection orchestration.
   - Extract a pure selection/countdown coordinator with injectable clipboard, foreground-input, clock, and window-action seams; add a desktop test project if needed rather than moving Avalonia behavior into Core.
   - Pass `Alt` state from `StickerTile` to the selection command.
   - Clear search after every successful copy.
   - Apply keep-open independently from target presence.
   - Consume valid hotkey target, restore focus, conditionally paste, and schedule/restart/cancel exactly one restore chain.
   - Dispose timers/active clipboard chains during replacement, disabling, and shutdown; expose progress/status properties.
   - Add deterministic tests for target capture/consumption, copy-failure retry, Alt suppression, keep-open behavior, delay disable/cancellation, consecutive restart, and external clipboard invalidation.
5. Extend settings and status UI.
   - Add mutually exclusive checkbox behavior and `0..60` integer input.
   - Keep settings header fixed and make content vertically scrollable within constrained overlay height.
   - Add compact right-aligned countdown progress and remaining-time text with stable dimensions.
6. Review failure paths and file size.
   - Ensure copy failure does not clear/hide/consume target.
   - Ensure snapshot skip and paste failure remain non-fatal and visible.
   - Split platform collaborators and ViewModel partials so every C# file stays below 400 lines.

## Validation

Run after each coherent C# batch and resolve all warnings/info diagnostics without automatic formatting or broad pragmas:

```bash
dotnet build StickerPicker.slnx -c Release
dotnet test StickerPicker.slnx -c Release
dotnet format --severity info --verify-no-changes | echo $?
dotnet format --severity info --verify-no-changes
```

The piped command is required by the repository instructions; the direct invocation is the authoritative formatter gate because the pipeline does not preserve formatter output or exit status.

Before completion:

```bash
dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r linux-x64 --self-contained true
fdfind -e cs src tests -E obj -E bin -x wc -l | awk '$1 >= 400 { print }'
```

NativeAOT does not cross-compile across operating systems. Also publish `win-x64` on Windows before release; the Linux planning/implementation session cannot substitute for that gate.

Manual Windows matrix:

- Defaults: copy, clear search, and hide with all new features disabled.
- Hotkey auto-paste into a normal text/chat target; tray/startup entry never pastes.
- `Alt` skips paste only; target focus, hide, search clear, and restoration remain.
- Keep-open supports consecutive A/B selection and ultimately restores the original clipboard.
- Text, HTML/RTF, file list, and empty clipboard restore; unsupported/oversize content explicitly skips.
- External copy during countdown cancels without overwrite.
- Marker mismatch and sequence mismatch each cancel independently without mutation.
- Invalid/closed/elevated target does not receive or redirect input; status reports paste failure.
- A fault-injected partial `SendInput` prefix is followed only by the required key-up cleanup.
- Settings content scrolls at minimum window height while header/close remain fixed.
- Countdown progress appears, updates, restarts, and disappears on every terminal path.
- QQ/WeChat accept existing file-drop/bitmap paste behavior.

## Risk and Rollback Points

- Clipboard mutation is the highest-risk module. Keep it behind one seam and land its handle ownership rules with focused review before wiring UI orchestration.
- Foreground/input injection must never run without post-activation target equality. Remove automatic paste rather than weaken this invariant if Windows behavior is inconsistent.
- Config additions are backward-compatible. Rolling back code leaves unknown JSON properties harmless; no destructive migration is required.
- If complete clipboard restoration proves unreliable for a supported format during manual testing, move that format to the conservative skip list rather than restoring a partial snapshot.

## Review Gate

Do not run `task.py start` until the user reviews `prd.md`, `design.md`, and `implement.md`, and both sub-agent context manifests contain real spec/research entries.
