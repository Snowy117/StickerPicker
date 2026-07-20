# Implement — UX Optimizations Round 2

Execution order chosen for minimal merge friction and so each commit is independently verifiable. Each step ends with: `dotnet build` + `dotnet test` + `dotnet format --severity info --verify-no-changes` (exit 0) + LSP diagnostics clean, then `git commit`.

Shared validation command (run after every step before commit):

```bash
dotnet build StickerPicker.slnx -c Debug 2>&1 | tail -5
dotnet test StickerPicker.slnx -c Debug 2>&1 | tail -10
dotnet format --severity info --verify-no-changes | echo "exit=$?"
```

Global: every `.cs` file < 400 lines. Split before growing.

---

## Step 1 — Borderless-at-rest buttons (item 8)

**Why first:** pure XAML style change, zero logic risk, establishes the visual baseline that later steps (merged import button, title-bar removal) build on.

- Edit `Themes/SteamStyles.axaml`:
  - `Button` default: `BorderThickness=0` at rest; add `:pointerover /template/ ContentPresenter` `BorderBrush=SteamBorderSoftBrush` (note: border lives on template, set via `BorderBrush` on the Button + ensure template shows it — verify by test).
  - `Button.subtle`: already transparent bg; set `BorderThickness=0` at rest, restore border on `:pointerover`.
  - `Button.icon`: `BorderThickness=0` at rest.
  - `Button.primary` / `Button.danger`: leave as-is (filled).
- Verify both themes: light border (`SteamLightBorderSoft`) must be visible on hover.

**Commit:** `style(ui): borderless buttons at rest (Steam style)`

---

## Step 2 — Hotkey capture visual feedback (item 7)

- `Controls/HotkeyCaptureBox.axaml`: add pseudo-class styling for `:focus`. Border → accent + 2px; text → "按下组合键…" when focused & gesture empty (handle in code-behind `OnGotFocus`/`OnLostFocus` + `UpdateDisplay`).
- `Controls/HotkeyCaptureBox.axaml.cs`: override `OnGotFocus`/`OnLostFocus` to refresh display + add a subtle pulse via Avalonia `Animation` on the border `BoxShadow` (optional; if too heavy, just accent border).
- Keep `OnKeyDown` behavior identical.

**Commit:** `feat(ui): hotkey capture shows focused/listening state`

---

## Step 3 — Tray left-click toggle + verify quit menu (item 2)

- `App.axaml.cs` `WireTrayCommands`: change `ShowWindowCommand` to call `_windowChrome?.ToggleVisible()` (was `.Show()`). Rename command intent comment.
- Runtime-verify: right-click tray shows menu with 退出; clicking 退出 calls `Shutdown()`. If menu missing on Windows, investigate `NativeMenu` binding (likely fine).
- No Core change.

**Commit:** `fix(tray): left-click toggles window; verify quit menu works`

---

## Step 4 — Ctrl+wheel zoom priority (item 1)

- `Views/MainWindow.axaml`: name the ScrollViewer `x:Name="StickerScroll"`.
- `Views/MainWindow.axaml.cs`:
  - Remove the Window-level `PointerWheelChanged="OnPointerWheelChanged"` attribute (or keep but no-op when source is the scroll area).
  - In ctor, after `InitializeComponent`: `StickerScroll.AddHandler(PointerWheelChangedEvent, OnStickerScrollWheel, RoutingStrategies.Tunnel, handledEventsToo: true);`
  - `OnStickerScrollWheel`: if Ctrl held → `AdjustThumbnail`, `e.Handled = true`; else let ScrollViewer scroll (do nothing).
- Keep `AdjustThumbnail` clamp range as-is.

**Commit:** `fix(ui): Ctrl+wheel always zooms sticker grid (priority over scroll)`

---

## Step 5 — Sharp thumbnails on zoom (item 4)

- `ViewModels/StickerItemViewModel.cs`:
  - Track `_decodedWidth`.
  - Compute `DecodeWidthFor(double size) => Math.Clamp((int)Math.Ceiling(size * 1.5), 64, 512)`.
  - Constructor decodes at `DecodeWidthFor(tileSize)`.
  - `partial void OnTileSizeChanged(double value)`: if `value > _decodedWidth` (or decode width would increase by >1.25×), re-decode from disk; else keep.
- Verify: zoom to 256 stays sharp; memory acceptable.

**Commit:** `fix(ui): re-decode thumbnails when zoomed past decode size (no blur)`

---

## Step 6 — Sticker right-click context menu (item 3)

- Define bubbling RoutedEvents in a new static class `StickerActionRoutedEvents` (or inside `StickerTile`): `EditTagsEvent`, `MoveStickerEvent`, `RenameEvent`, `DeleteEvent` — or one `StickerActionEvent` with an enum payload. Chosen: one event + `StickerActionKind` enum + custom `EventArgs` carrying the `StickerItemViewModel`.
- `Controls/StickerTile.axaml`: add `<ContextMenu>` on `Root` with `MenuItem`s whose `Click` raises the events (or bind `Command` to VM forwarder commands that raise the event). Simplest: `ContextMenu` MenuItems with `Click` handlers in code-behind that raise the routed event.
- `Controls/StickerTile.axaml.cs`: `OnPointerPressed` only handles left-click; ensure right-click reaches ContextMenu (Avalonia shows ContextMenu on right-click by default when `ContextMenu` is set — verify).
- `Views/MainWindow.axaml.cs`: add handlers for the routed events → show existing dialogs (`PromptForNameAsync` for rename/tags-input, category-picker for move, `ConfirmAsync` for delete) → call new `MainViewModel` commands.
- `ViewModels/MainViewModel.cs`: add `EditStickerTags(StickerItemViewModel, IEnumerable<string>)`, `MoveSticker(StickerItemViewModel, string targetCategoryId)`, `RenameStickerFile(StickerItemViewModel, string)`, `DeleteSticker(StickerItemViewModel)` commands delegating to `_library`.
  - Rename file: Core has no `RenameSticker`; implement by MoveSticker to same category with new name OR add a thin method. Check Core — `MoveSticker` changes category, not filename. **Decision:** skip filename rename to avoid Core churn; instead the menu offers 重命名 only when it renames via tags metadata OR omit rename. Re-scope: menu = 编辑标签 / 移动到分类 / 删除. (Rename file is a Core change — out of scope per PRD "no Core library logic changes".) Update PRD item #3 to drop "重命名 (file rename)".
- Keep file <400 lines: dialog code in View, command code in VM partial if needed.

**Commit:** `feat(ui): sticker right-click context menu (tags/move/delete)`

---

## Step 7 — Hover preview (item 5)

- `Core/Models/AppConfig.cs`: add `public bool HoverPreview { get; set; } = true;` + include in `Clone()`.
- `ViewModels/SettingsViewModel.cs`: add `HoverPreview` observable property + wire into `ApplyAndSave`.
- `Views/MainWindow.axaml`: add settings checkbox "鼠标悬停时显示大图".
- Define `StickerHoverEvent` (bubbling) carrying the `StickerItemViewModel`.
- `Controls/StickerTile.axaml.cs`: `PointerEntered` → start `DispatcherTimer` 250ms → raise event with item. `PointerExited` → stop timer + raise hide event (or reuse one event with `IsEnter` flag).
- `Views/MainWindow.axaml.cs`: handle event → if `vm.Settings.HoverPreview` → position a `Popup` (or a `Border` in the root `Panel`) at cursor +16,+16, load full image `Bitmap.DecodeToWidth(stream, 480)`, `Opacity=0.92`. On hide → clear `Source`, dispose bitmap.
- Clamp position to window bounds.

**Commit:** `feat(ui): hover preview popup (gated by new HoverPreview setting)`

---

## Step 8 — Settings overlay animation (item 6)

- `Views/MainWindow.axaml`: restructure overlay:
  - Outer `Border` `x:Name="OverlayMask"` `IsVisible` bound to a new code-behind property `IsOverlayVisible`.
  - Inner `Border` `x:Name="OverlayCard"` with `Opacity` bound to `OverlayOpacity` and `RenderTransform` = `ScaleTransform` bound to `OverlayScale`. Add `Transitions` (DoubleTransition 150ms on Opacity + Scale).
- `Views/MainWindow.axaml.cs`: subscribe to `vm.PropertyChanged`; on `IsSettingsOpen`:
  - Open: `IsOverlayVisible=true`, then on next frame set `OverlayOpacity=1`, `OverlayScale=1`.
  - Close: set `OverlayOpacity=0`, `OverlayScale=0.96`; start `DispatcherTimer` 160ms → `IsOverlayVisible=false`.
- Both initial values: opacity 0, scale 0.96 (animates to 1 on open).

**Commit:** `feat(ui): animate settings overlay open/close (fade+scale)`

---

## Step 9 — Merged import button (item 9)

- `Views/MainWindow.axaml`: replace the two import `Button`s with one `Button` "导入 ▾" with a `ContextMenu` (or `SplitButton`). Chosen: `Button` + `ContextMenu` with two `MenuItem`s (导入文件 / 导入文件夹).
- `Views/MainWindow.axaml.cs`: wire the menu items to existing `OnImportFilesClick` / `OnImportFolderClick` (or open the context menu on click: `ImportButton.ContextMenu.Open(this)`).
- Ensure keyboard accessible (Tab to button, Enter opens menu, arrows select).

**Commit:** `feat(ui): merge import file/folder into single dropdown button`

---

## Step 10 — Remove header bar (item 10)

**Clarification:** Remove the *in-app* header bar (dark strip with "STICKERPICKER" text + refresh/settings), NOT the OS window title bar.

- `Views/MainWindow.axaml`: delete the top-docked `<Border DockPanel.Dock="Top" Classes="headerbar" ...>` block that contains the STICKERPICKER title + refresh/settings buttons.
- Relocate refresh + settings buttons into the existing toolbar `Border` (search + import row). New toolbar layout: `[search .........] [刷新] [设置] [导入 ▾]` (right cluster).
- Do NOT touch window chrome (`ExtendClientAreaToDecorationsHint`, `SystemDecorations`, `BeginMoveDrag`). The OS title bar stays as-is and provides move/minimize/close.
- Verify layout collapses cleanly (no empty gap where the header was); sidebar top edge now meets the toolbar; status bar at the bottom is retained.

**Commit:** `feat(ui): remove redundant in-app header bar; move refresh/settings into toolbar`

---

## Final checks (after step 10)

- Full `dotnet build` / `dotnet test` / `dotnet format --verify-no-changes`.
- Manual smoke: all 10 items behave per PRD in both themes.
- Update journal `.trellis/workspace/Snowy117/journal-1.md`.
- Spec update (Phase 3.3): record any new conventions (routed-event pattern for sticker actions, overlay animation code-behind pattern, ExtendClientArea usage) into `.trellis/spec/frontend/`.
- Final commit (if spec changes): `docs(spec): record UX round 2 conventions`.
