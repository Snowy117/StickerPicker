# Design — GUI Overhaul

## Boundaries

- `StickerPicker.Core` gains two persisted scalar fields on `AppConfig`
  (`PreviewOpacity`, `UseGpuRendering`). No new seam; existing
  `IConfigStore`/`ConfigStore` already persists `AppConfig`. The source-gen
  `CoreJsonContext` already lists `AppConfig` as a root, so the new scalar
  members are picked up automatically.
- `StickerPicker` (Avalonia host) owns all UI behavior changes:
  - `MainWindow` — status-bar hovered-name binding, global key routing,
    single-unit overlay animation, inline tag-editor overlay.
  - `MainViewModel` / `SettingsViewModel` — new observable settings and
    overlay state.
  - `StickerTile` — drop `ToolTip.Tip`; raise hover name events immediately.
  - `App` / `Program` — read `UseGpuRendering` from config at startup and
    build `Win32PlatformOptions` accordingly; remove env-var code path.
  - `AvaloniaWindowChromeService.ToggleVisible` — new semantic per spec.
- `IWindowChromeService` gains no new members; the semantic is implemented
  inside `ToggleVisible` using the existing `IsVisible` plus an
  "isActive/topmost" probe. Avalonia `Window.IsActive` + `Window.Topmost`
  are the probes.

## Per-point design

### 0. Visual-style spec

Add `.trellis/spec/frontend/visual-style.md` summarizing the supplied brief.
Reference it from `.trellis/spec/frontend/index.md` table. No code change.

### 1. Status-bar naming (no tooltip)

- `StickerTile.axaml`: remove `ToolTip.Tip="{Binding FileName}"`.
- `MainViewModel`: add `[ObservableProperty] string HoveredFileName` (empty
  when nothing hovered). Status bar shows it next to the count.
- Sticker hover event already bubbles to `MainWindow.OnStickerHover`. Extend
  it to set `vm.HoveredFileName = e.IsEnter ? e.Sticker.FileName : ""`.
- On `Hide()` / preview hide, clear it.

### 2. Instant preview

- `StickerTile.OnPointerEntered`: drop the 250ms `DispatcherTimer`; raise
  `RaiseHover(true)` synchronously.
- The async decode in `MainWindow.ShowHoverPreviewAsync` already gates on
  cancellation/version, so removing the delay is the only change.
- `_hoverTimer` field and its stop/null logic are removed.

### 3. Global type-to-search

- `MainWindow`: subscribe to `TextInputEvent` (tunnel) at the window level.
  Avalonia exposes `TextInputElement.TextInputEvent` as a bubbling routed
  event. We use `AddHandler(TextInputEvent, OnGlobalTextInput, RoutingStrategies.Tunnel)`
  so we see the text before the focused control.
- In the handler: if `SearchText` TextBox is focused, do nothing (let normal
  input flow). Otherwise, if the input is non-empty printable text, focus
  the search box and append to `vm.SearchText`. Mark `e.Handled = true` only
  when we actually rerouted.
- IME composition already surfaces as `TextInput` with the composed string,
  so CJK works without extra work; raw key-down during IME compose is left
  to the IME. We do **not** hook `KeyDownEvent` for printable detection
  because that would double-fire on IME.
- Ctrl+wheel thumbnail zoom is a `PointerWheelChanged` handler and is
  unaffected.
- Modifier-only / function / navigation keys are not `TextInput`, so they
  are naturally ignored.

### 4. Preview opacity setting

- `AppConfig`: `double PreviewOpacity { get; set; } = 0.92;` (matches the
  current hard-coded `0.92` in `EnsureHoverPreviewWindow`).
- `SettingsViewModel`: `PreviewOpacity` observable, persisted via existing
  `ApplyAndSave`.
- `MainWindow.EnsureHoverPreviewWindow` reads `vm.Settings.PreviewOpacity`
  when building the border. Also update an already-created preview border
  when the setting changes (subscribe to `Settings.PropertyChanged` or have
  the VM expose the value via a method). Simpler: bind the border `Opacity`
  through a property on the window kept in sync with the VM.
- Settings overlay: add a slider bound to
  `Settings.PreviewOpacity` (range 0.2 – 1.0, step 0.01).

### 5. Hotkey toggle semantics

Current `AvaloniaWindowChromeService.ToggleVisible`:

```csharp
if (IsVisible) Hide(); else { Show(); Activate(); }
```

New semantic:

```csharp
if (!_window.IsVisible || !_window.IsActive)
{
    Show(); Activate();
    // If AlwaysOnTop was requested, re-assert it (Activate may toggle it off).
}
else
{
    Hide();
}
```

`Window.IsActive` is true when the window has keyboard focus. Combining
"visible && active" covers "focused" and "topmost is implicitly active".
The user's phrasing "既没有聚焦也没有置顶" maps to: surface when not-active,
hide when active. (Topmost alone does not need a separate probe because
`IsActive` already reflects focus; if window is visible but unfocused we
must surface it.)

### 6. GPU rendering setting

- `AppConfig`: `bool UseGpuRendering { get; set; } = false;` (current
  default behavior is software).
- `Program.BuildAvaloniaApp`: read config **before** building options.
  Loading config is cheap (one JSON read in `LocalAppData`). Introduce a
  small helper in `Program` that reads `AppConfig` via `AppPaths` +
  `ConfigStore`. Because `AppPaths.Resolve()` is side-effect-free aside
  from directory creation, and `ConfigStore.Load()` is read-only on disk,
  this is safe to do in `Main` before Avalonia starts.
- Replace the `STICKERPICKER_USE_GPU` env-var check with the config value.
  On non-Windows hosts, ignore the flag (no `Win32PlatformOptions`).
- `SettingsViewModel.UseGpuRendering` observable with a note in the UI that
  it applies on next launch (changing rendering backend at runtime is not
  supported by Avalonia for an already-created window).
- Add a checkbox in the Settings overlay with helper text "重启后生效".

### 7. Inline tag editor overlay

- `MainViewModel`: add
  - `[ObservableProperty] bool IsTagEditorOpen`
  - `[ObservableProperty] StickerItemViewModel? TagEditorTarget`
  - `OpenTagEditor(StickerItemViewModel)` / `CloseTagEditor()` helpers.
- `MainWindow.axaml`: add a second overlay (`TagEditorMask` /
  `TagEditorCard`) structurally identical to the settings overlay. Its
  content is a `TagEditor` UserControl (new).
- `MainWindow.StickerActions.cs`: replace `EditTagsAsync`'s
  `PromptForNameAsync` call with `vm.OpenTagEditor(item)`. Delete/confirm
  keep using the simple confirm dialog (or can be moved into the overlay
  later — out of scope here).
- Both overlays share the same mask+card styling and the same fade
  animation. Only one of them should be open at a time; opening the tag
  editor while settings is open is allowed (settings closes first) but
  simpler: tag editor supersedes settings — closing one doesn't affect the
  other's state, but the mask `ZIndex` ensures the latest one wins.

### 8. Unified overlay animation

Investigation: each settings control inherits a `BrushTransition` from the
global `Button`/`TextBox`/`ListBoxItem` styles. When the overlay's parent
`Opacity` goes 0→1, the children's `Background`/`BorderBrush`/`Foreground`
*also* animate from their unset state (because they were never measured
while invisible), producing a perceived stagger.

Fix:

- Wrap the overlay card's child panel so that transitions on inner controls
  don't run during the fade. Two viable approaches:
  1. Drive the fade via `RenderTransform.Opacity` is not a thing — opacity
     is already one property; the stagger is from child brush transitions.
  2. Add a class to the card root and use a `Style` selector that disables
     `Transitions` on descendant `Button`/`TextBox`/etc. while the card
     has `Opacity < 1`. This requires the card to toggle a class during
     open/close. Simpler: set the card's `Opacity` via
     `OverlayCard.Opacity = 1` only after the card is visible AND defer
     setting it to next frame, AND during the close, set
     `OverlayCard.Opacity = 0` and only afterwards hide.
- Cleanest fix observed in Avalonia: the stagger is actually caused by the
  `Border.Transitions` declared on `OverlayCard` itself — it declares a
  `DoubleTransition` on `Opacity` (good) but the *children* have their own
  brush transitions and they re-evaluate when the card becomes `IsVisible`.
  Solution: set `OverlayMask.IsVisible = true` AND `OverlayCard.Opacity = 1`
  atomically via `Dispatcher.UIThread.Post` (already done), but ALSO ensure
  the inner StackPanel's children have stable brush values before the card
  becomes visible by forcing a measure pass. Practical and minimal fix that
  reliably removes the stagger: hoist the fade onto the **mask** (outer
  Border) instead of the card, and remove the per-control transitions on
  the card's subtree by adding a one-shot `Style` scoped to the overlay
  that disables transitions during the open/close window.

  Concretely:
  - Move `Opacity` animation to `OverlayMask` (fade the whole mask + card
    together).
  - Add `Style Selector="Border.overlay-root"` that sets `Transitions` to
    empty on its descendants via a transient class toggle
    (`overlay-animating`) applied during open/close.

  After implementation, the result must visually fade as one block.

### 9. Tag UX redesign

New `Controls/TagEditor.axaml` UserControl:

```
[ chip ] [ chip ] [ chip ]   [ + input box ]
                                (Enter adds; Backspace on empty removes last)
```

- Bindable properties: `Tags` (`IList<string>`),
  `AvailableSuggestions` (optional), and an `Apply`/`Cancel` command pair.
- Code-behind handles Enter/Backspace/Chip-click-to-remove.
- On Apply, calls `vm.EditStickerTagsCommand` with the final list, then
  closes the overlay.

Code-behind stays small; chips are rendered as `Border` + `Button` in a
`WrapPanel`; the input is a `TextBox` with a `KeyDown` handler.

## Data flow

```
AppConfig (persisted)
  ├── PreviewOpacity  → SettingsViewModel → MainWindow hover border Opacity
  └── UseGpuRendering → Program.BuildAvaloniaApp → Win32PlatformOptions

MainViewModel.IsTagEditorOpen + TagEditorTarget
  → MainWindow overlay visibility + TagEditor control
  → EditStickerTagsCommand → Core library SetTags → ApplyFilter

MainViewModel.HoveredFileName ← StickerTile hover (immediate) → status bar
```

## Compatibility / rollback

- `AppConfig` additions are backward-compatible (default values; old
  configs without these fields deserialize to defaults).
- Removing the env var is a breaking change only for users who set it;
  documented in the spec.
- Tag-editor overlay is purely additive; the old `PromptForNameAsync`
  method can be kept for create/rename category prompts (unaffected).
- Each point is independently revertable via its own commit.

## Risks

- Global `TextInput` routing could swallow text meant for the search box if
  the focus check is wrong — mitigated by early-return when search box is
  focused.
- Inline overlay adds XAML weight to `MainWindow.axaml`; the file is
  currently ~290 lines and the spec cap is 400 — must keep it under. May
  need to extract the tag editor into its own `.axaml` (planned).
- Per-child animation fix is empirical; if the class-toggle approach does
  not fully remove stagger, fallback is to disable brush transitions on
  the settings subtree entirely (acceptable trade-off, settings is not a
  perf-critical surface).
