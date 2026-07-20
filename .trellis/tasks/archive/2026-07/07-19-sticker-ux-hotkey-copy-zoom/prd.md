# UX: hotkey capture, click copy fix, zoom perf

## Goal

Fix three post-MVP UX bugs in StickerPicker:

1. Hotkey settings: capture key chords instead of free-text entry
2. Click sticker: copy to clipboard + hide window (broken)
3. Ctrl+wheel thumbnail zoom: reduce lag

## Root causes (confirmed)

| Issue | Cause |
|-------|--------|
| Click no-op | `StickerTile` calls `SelectCommand.Execute(null)`; `SelectSticker` returns when item is null |
| Zoom lag | `OnThumbnailSizeChanged` → full `ApplyFilter()` recreates all tiles + re-decodes every image |
| Hotkey UX | Settings uses plain `TextBox` for gesture string |

## Acceptance

- [x] Settings: focus hotkey control, press combo (e.g. Ctrl+Shift+E), display updates; Save registers and persists
- [x] Click sticker → clipboard success (or error status) → main window hides on success
- [x] Ctrl+wheel changes tile size without full library rebuild; feels responsive for ~100 stickers
- [x] `dotnet build` / `dotnet test` green; `dotnet format --severity info --verify-no-changes` exit 0
- [x] All `.cs` files &lt; 400 lines

## Implemented

- `StickerTile`: `SelectCommand.Execute(item)` (was null)
- `MainViewModel.OnThumbnailSizeChanged`: `ResizeTiles` + debounced save (no `ApplyFilter`)
- `HotkeyCaptureBox` + `HotkeyGestureFormatter`; capture auto-applies via `SaveHotkey`
- Clipboard: `TryOpenClipboard` with short retries
