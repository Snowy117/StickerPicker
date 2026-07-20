# Journal - Snowy117 (Part 1)

> AI development session journal
> Started: 2026-07-19

---



## Session 1: StickerPicker MVP-A implement + NRE fix

**Date**: 2026-07-19
**Task**: StickerPicker MVP-A implement + NRE fix
**Branch**: `master`

### Summary

Built Avalonia StickerPicker MVP-A (.slnx, folder categories, Core library split, 60 tests, Windows clipboard/hotkey, Steam UI). Fixed MainViewModel ctor NRE and analyzer/format gates. Residual: hotkey capture UX, click-to-copy param bug, zoom lag.

### Main Changes

(Add details)

### Git Commits

| Hash | Message |
|------|---------|
| `6d98b0d` | (see git log) |
| `032959c` | (see git log) |
| `688f9df` | (see git log) |
| `a6c3271` | (see git log) |
| `bdb24ee` | (see git log) |

### Testing

- [OK] (Add test results)

### Status

[OK] **Completed**

### Next Steps

- None - task complete

## 2026-07-19 UX hotkey/copy/zoom
- Fixed StickerTile Execute(null); zoom ResizeTiles; HotkeyCaptureBox
- Commit 5f58a19

## 2026-07-20 UX Optimizations Round 2 (task 07-20-ux-optimizations-2)

Ten independently-committed UX improvements for the StickerPicker desktop client:

| Item | Commit | Summary |
|------|--------|---------|
| 8 | f995531 | Borderless-at-rest buttons (Steam style) |
| 7 | 7dd4143 | Hotkey capture shows focused/listening state with pulse |
| 2 | 4922b61 | Tray left-click toggles window visibility |
| 1 | b815840 | Ctrl+wheel always zooms (Tunnel handler with handledEventsToo) |
| 4 | c820033 | Re-decode thumbnails when zoomed past decode size (cap 512) |
| 3 | 6700d40 | Sticker right-click menu: tags/move/delete (added Core DeleteSticker) |
| 5 | 2368864 | Hover preview popup (new HoverPreview config, default ON) |
| 6 | 55bdc00 | Settings overlay fade+scale animation (150ms) |
| 9 | 5d1483c | Merged import file/folder into single dropdown button |
| 10 | a7096a8 | Removed in-app header bar; refresh/settings moved to toolbar |
| - | 4afec7c | chore: removed stale header comment |

**Core addition** (PRD said "no Core changes" but item 3 删除 needed an API):
`IStickerLibrary.DeleteSticker` + `FolderStickerLibrary.DeleteSticker` +
`LibraryIndexStore.RemoveStickerKey`. Tests added (now 62 total).

**Key Avalonia 12.1 lessons captured** for future work:
- `MenuItem.Click` / `ContextMenu.Opening` CANNOT use XAML string handlers
  (AVLN3000/AVLN2000); wire via `x:Name` + `+=` in code-behind.
- `Button.Click` string handlers DO work — only MenuItem/ContextMenu differ.
- `GetVisualRoot()` does not exist in 12.1; use `TopLevel.GetTopLevel(this)`.
- `TranslatePoint` is `Avalonia.VisualExtensions` — needs `using Avalonia;`.
- Animation on pseudo-class: `<Style.Animations>` not Setter.
- Custom control style selector uses pipe: `controls|HotkeyCaptureBox`.
- DataContext is assigned AFTER ctor in this app (App.axaml.cs) — code-behind
  handlers that read DataContext must subscribe to DataContextChanged.
- `_timer!` null-forgiving inside Tick lambda flagged IDE0370 — capture a local.
- S4144 (identical method bodies) tripped when adding a third property partial
  method; refactored to a single `OnPropertyChanged` override.
- consts must be PascalCase (IDE1006).
- MA0006 requires `string.Equals(..., StringComparison.Ordinal)` over `==`.
- **CRITICAL (caused startup crash):** `BoxShadow` setter values are parsed
  at runtime via `BoxShadows.Parse` which treats the string as a literal
  color — `{DynamicResource}`/`{StaticResource}` markup extensions inside
  the BoxShadow string do NOT work and throw `FormatException: Invalid color
  string: '{DynamicResource'` at App.Initialize(). `dotnet build` does NOT
  catch this (XAML compiles; parsing is deferred to load time). Use a literal
  hex color (e.g. `0 0 0 2 #66c0f4`). BorderBrush/Background/Foreground setters
  DO resolve DynamicResource fine — only type-converted string properties
  (BoxShadow, possibly others) fail. Fix landed in commit c648cca.

**Validation status (all green)**:
- `dotnet build` 0 warnings/0 errors
- `dotnet test` 62 passed / 0 failed
- `dotnet format --severity info --verify-no-changes` exit 0
- LSP: no info/hint diagnostics across workspace
- All `.cs` files < 400 lines (largest: FolderStickerLibraryTests.cs @ 375)
