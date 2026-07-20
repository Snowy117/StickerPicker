# Design — UX Optimizations Round 2

Reference impl context (read first): `prd.md`. Codebase facts confirmed by inspection:

- Shell: `Views/MainWindow.axaml(.cs)` — DockPanel with header (刷新/设置), sidebar (categories), toolbar (search + 导入文件 + 导入文件夹), `ScrollViewer` + `ItemsControl`+`WrapPanel` of `StickerTile`.
- Thumbnail decode: `StickerItemViewModel.TryLoadThumbnail` uses `Bitmap.DecodeToWidth(stream, tileSize)` once at construction. `OnThumbnailSizeChanged` → `ResizeTiles` only mutates `TileSize` (no re-decode) → upscale blur.
- Zoom hook: `MainWindow.OnPointerWheelChanged` already calls `AdjustThumbnail` on Ctrl+wheel and sets `e.Handled = true`, BUT the handler is on the `Window`; when the inner `ScrollViewer` can still scroll, Avalonia's routed event reaches the ScrollViewer first because the ScrollViewer handles wheel in its own handler at the bubble stage before/instead of the Window-level handler. Confirmed by user report.
- Tray: `App.axaml` declares a `TrayIcon` with `Command` (left-click) and `Menu` (NativeMenu with 显示/设置/退出). Right-click should surface menu. Needs runtime verification; likely fine but item #2 is partly a verification + UX clarification (e.g. ensure left-click toggles rather than always shows).
- Library API (Core) already supports everything item #3 needs: `SetTags`, `MoveSticker`, `RenameCategory`, `DeleteCategory`, `Query`.
- Settings: `SettingsViewModel` + `AppConfig`. Add `HoverPreview` bool to `AppConfig` (default true), surface in `SettingsViewModel`.
- Chrome: `IWindowChromeService` / `AvaloniaWindowChromeService` wraps a single `Window`. `Shutdown()` calls `MainWindow.ForceClose()` + `desktop.Shutdown()`.

## Per-item design

### 1. Ctrl+wheel zoom priority

Problem: Window-level `OnPointerWheelChanged` is too late / ScrollViewer eats the event.

Approach: Move the Ctrl+wheel handling into a tunneled (`Preview`) handler, or attach `PointerWheelChanged` on the `ScrollViewer` itself (tunneling stage). Cleanest: handle it on the `ScrollViewer` via `Tunnel` — but Avalonia `PointerWheelChanged` is a bubbling routed event. Use `AddHandler(..., RoutingStrategies.Tunnel, handledEventsToo: true)` on the ScrollViewer (or the grid) so we intercept before the ScrollViewer's own handler. Set `e.Handled = true` when Ctrl is held.

Implementation point: in `MainWindow.axaml.cs` ctor, register a tunneled handler on the `ScrollViewer` (give it `x:Name="StickerScroll"`) for `PointerWheelChangedEvent` with `handledEventsToo: true`. Keep the existing Window-level handler removed or guarded to avoid double-handling.

Risk: `handledEventsToo` is needed because ScrollViewer marks wheel handled. Tunnel runs first so we get a chance to swallow it.

### 2. Tray quit

Likely already functional. Verification step + minor polish:
- Confirm right-click shows NativeMenu with 退出 on Windows.
- Change left-click behavior: currently `ShowWindowCommand` always Shows. Make left-click **toggle** visibility (call `_windowChrome.ToggleVisible()`), matching hotkey behavior, so the tray is a true toggle — feels more like Steam/Discord.
- Document the menu in the settings status hint if needed.

If right-click does not surface the menu, the fix is platform-specific (out of scope per PRD; fallback: ensure `TrayIcon.Menu` is set and `Command` is not null).

### 3. Sticker right-click context menu

`StickerTile.axaml`: add a `ContextMenu` on `Root` border with items bound to commands on `StickerItemViewModel`. To keep the VM thin and avoid bloating it, expose a single `OpenContextMenu` flow OR add relay commands on the VM that delegate to the parent `MainViewModel`.

Design choice: Add commands on `MainViewModel` (`EditStickerTagsCommand(sticker)`, `MoveStickerCommand(relativePath, targetCategoryId)`, `RenameStickerFileCommand(sticker)`, `DeleteStickerCommand(sticker)`), and have `StickerItemViewModel` hold a reference (already holds `SelectCommand`). Bind `ContextMenu` via `ContextMenu.Items` with `ItemSource` referencing main VM — simpler: use static menu items in `StickerTile.axaml` whose `Command`/`CommandParameter` bind through the tile's `DataContext` to a small set of `ICommand` properties exposed on `StickerItemViewModel` that forward to `MainViewModel`.

To avoid file >400 lines: keep dialog logic in `MainWindow.axaml.cs` (it already owns `PromptForNameAsync` / `ConfirmAsync`). The VM commands orchestrate; the View provides dialogs via events or a callback. Reuse the existing pattern: VM exposes command, View subscribes to a "request dialog" event OR the tile raises a bubbling RoutedEvent that the Window handles (like `HotkeyCaptureBox.GestureCaptured`).

Simplest contract:
- `StickerItemViewModel` exposes `RequestTagsEdit`, `RequestMove`, `RequestRename`, `RequestDelete` as `ICommand` whose execute raises a bubbling `RoutedEventArgs` (new `StickerActionEvent`) carrying the action kind + item.
- `MainWindow` handles `StickerActionEvent` in code-behind, shows the appropriate dialog, then calls the matching `MainViewModel` method.

### 4. Sharp thumbnails

Root cause: `Bitmap.DecodeToWidth(stream, tileSize)` decodes at initial size; upscaling interpolates.

Fix: decode at a higher fixed ceiling so zoom within [48, 256] never upscales beyond source resolution. Use `Math.Max(tileSize, 256)` decoded, OR decode at device-independent full target with a cap (e.g. 384). Also re-decode when tile size grows beyond current bitmap's decoded width × 1.5.

Design: add to `StickerItemViewModel`:
- `DecodeWidth` computed = `Clamp((int)Math.Ceiling(tileSize * 1.5), 64, 512)`.
- On `TileSize` change: if `TileSize > _lastDecodeWidth`, re-decode. Else keep (downscale is cheap/sharp).

Memory: cap decode at 512px (well within Steam tile range and memory budget for ~100 stickers ≈ a few MB).

Alternative considered: `Bitmap` retains full stream → decode on demand. `Bitmap.DecodeToWidth` is the right API; we just raise the ceiling and re-decode on growth.

### 5. Hover preview

New `AppConfig.HoverPreview` (bool, default true) + `SettingsViewModel.HoverPreview`.

UI: a `Popup` owned by `MainWindow` (or a `HoverPreviewOverlay` UserControl) that shows the full-res image. Trigger: `StickerTile` raises a bubbling `StickerHoverEvent` on `PointerEntered` (with delay via `DispatcherTimer` ~250ms) and `PointerExited` (cancels + hides).

Position: place the popup at `this.PointToClient(pointer.Position)` offset by +16,+16, clamped to window bounds. Semi-transparent: `Opacity=0.92`.

Respect setting: the Window-level handler checks `vm.Settings.HoverPreview` before showing.

Keep it cheap: load full image only for the hovered tile (single `Bitmap.DecodeToWidth(stream, 480)`). Dispose on hide.

### 6. Settings animation

The settings overlay is a `Border` with `IsVisible={Binding IsSettingsOpen}`. Add:
- Wrap content in a `Panel` that is always present; toggle visibility of an inner Border with animation.
- Use `Transitions` on `Opacity` and a `ScaleTransform` (via `RenderTransform`) — but Avalonia `IsVisible` change can't transition directly. Pattern: bind `IsVisible` of the **outer mask** to `IsSettingsOpen` for input blocking, and animate `Opacity`/`Scale` of the inner panel via a `DoubleTransition` driven by a `StyledProperty<double>` (`SettingsOverlayScale`) or by binding to `IsSettingsOpen` through a `MultiBinding`/converter.

Simpler robust approach: keep `Border` outer (`IsVisible=IsSettingsOpen`), inner `Border` with `Opacity` + `RenderTransform` bindings to `IsSettingsOpen` via a `BoolToOpacityConverter` (or a code-behind that animates on `IsSettingsOpen` change). Add `Transitions` so opacity/scale animate over ~150ms.

Close animation caveat: when `IsVisible` flips false immediately, no animation plays. To animate close, delay hiding: introduce `SettingsOverlayShown` (actual IsVisible) vs `IsSettingsOpen` (logical). On open: set shown=true then animate. On close: animate then set shown=false after 150ms via DispatcherTimer. Implement in `MainViewModel` or in code-behind.

Chosen: code-behind in `MainWindow` reacting to `vm.IsSettingsOpen` via PropertyChanged; manages two states `IsOverlayVisible` (panel IsVisible) and animates `OverlayOpacity`. Cleaner than pushing animation state into the VM.

### 7. Hotkey capture feedback

`HotkeyCaptureBox.axaml`: add visual states via pseudo-classes. Add `:focus` and a custom `:listening` class set from code-behind when focused & not yet captured.

- Add `BorderThickness=2` and `BorderBrush=accent` on `:focus`, plus a subtle pulsing `BoxShadow` via `:listening` class + `Animation` (Avalonia `Animation` with `KeyFrames` on `BoxShadow` opacity) — keep minimal (one short pulse loop).
- Text changes to "按下组合键…" while focused and Gesture is empty.

### 8. Borderless-at-rest buttons

Update `SteamStyles.axaml`:
- `Button.subtle` and `Button.icon`: `BorderThickness=0` at rest; `:pointerover` adds `BorderBrush=SteamBorderSoftBrush` + `Background=SteamPanelAltBrush`; `:pressed` keeps accent dim.
- `Button` default (the neutral buttons like 导入文件夹 currently): make default also borderless at rest to match Steam; keep `:pointerover` border.
- `Button.primary` unchanged (filled green).

Verify light theme: BorderSoft is visible on light bg.

### 9. Merged import button

Replace the two import buttons with a single `Button` + `ContextMenu`/`SplitButton`. Avalonia has `SplitButton`; use it: primary click = 导入文件 (most common), chevron opens menu with 导入文件夹.

If `SplitButton` styling is heavy, alternative: a `Button` "导入 ▾" with a `ContextMenu` containing both. Chosen: `Button` + `ContextMenu` for simplicity and consistent Steam look (one green-ish primary with a dropdown).

Both menu items reuse `OnImportFilesClick` / `OnImportFolderClick` (route via `Click` or `MenuItem.Command`).

### 10. Remove header bar

**Clarification:** The user means the *in-app* header bar — the dark strip (DockPanel.Dock="Top", Classes="headerbar") that currently shows "STICKERPICKER" text plus the 刷新/设置 buttons. NOT the OS window title bar.

Fix: delete that `Border` from `MainWindow.axaml`. The refresh + settings buttons already live in it; relocate them into the existing toolbar `Border` (the search + import row) — add them to the right cluster so the toolbar becomes: `[search ........] [刷新] [设置] [导入 ▾]`.

No window chrome changes: `ExtendClientAreaToDecorationsHint` is NOT used; `SystemDecorations` untouched; the OS title bar stays (provides move/minimize/close). No `BeginMoveDrag` needed.

Layout: the main content `DockPanel` simply loses its top-docked header; everything else (sidebar, toolbar, grid, status bar) remains. Verify sidebar/headerless layout still looks correct in both themes.

Risk: minimal — pure XAML restructuring.

## File-impact map

| File | Changes |
|------|---------|
| `Views/MainWindow.axaml` | merged import button, settings overlay animation structure, scrollviewer naming, **remove header bar + relocate refresh/settings into toolbar** |
| `Views/MainWindow.axaml.cs` | wheel tunnel handler, split-button click wiring, settings overlay animation code-behind, sticker action + hover handlers |
| `Controls/StickerTile.axaml(.cs)` | context menu, hover event raise, RoutedEvents |
| `Controls/HotkeyCaptureBox.axaml(.cs)` | focus/listening visual state |
| `Themes/SteamStyles.axaml` | borderless-at-rest, animation helpers |
| `ViewModels/MainViewModel.cs` (+Categories partial) | sticker action commands, hover config read |
| `ViewModels/StickerItemViewModel.cs` | sharp re-decode, action commands forwarders, hover payload |
| `ViewModels/SettingsViewModel.cs` | HoverPreview property |
| `Core/Models/AppConfig.cs` | HoverPreview field |
| `App.axaml(.cs)` | tray left-click toggle |

No Core library logic changes required (all APIs exist).

## Risks / rollback

- ExtendClientArea may break on some Linux WMs → gate behind `OperatingSystem.IsWindows()` if reported.
- Wheel tunnel handler with `handledEventsToo` could swallow legitimate scrolling if Ctrl accidentally held — but Ctrl+scroll currently does nothing useful, acceptable.
- Each item commits independently, so any regression can be reverted in isolation.
