# Implement — GUI Overhaul

Each step is one commit. After every step, run the validation gate.

## Validation gate (after each step)

```bash
dotnet build StickerPicker.slnx -c Release
dotnet format --severity info --verify-no-changes | echo $?
# LSP diagnostics clean (call lsp_diagnostics on changed files)
# every modified .cs file < 400 lines
```

For steps that change persisted config or library behavior, also:

```bash
dotnet test StickerPicker.slnx -c Release
```

## Step 0 — Visual-style spec (commit: docs(spec): record visual style brief)

1. Create `.trellis/spec/frontend/visual-style.md` from the user's brief,
   condensed into project voice (English, table-friendly).
2. Append a row to `.trellis/spec/frontend/index.md` "Guidelines Index".
3. Commit.

No code change. No build needed (skip gate, but still verify markdown
renders / no broken links).

## Step 1 — Status-bar naming (commit: feat(ui): surface sticker name in status bar)

1. `src/StickerPicker/Controls/StickerTile.axaml`: remove
   `ToolTip.Tip="{Binding FileName}"`.
2. `src/StickerPicker/ViewModels/MainViewModel.cs`: add
   `[ObservableProperty] public partial string HoveredFileName { get; set; } = "";`
3. `src/StickerPicker/Views/MainWindow.axaml` status bar: show
   `{Binding HoveredFileName}` (e.g. prefix with the count, or its own
   column). Update the layout so hovered name does not collide with error
   message.
4. `src/StickerPicker/Views/MainWindow.HoverPreview.cs`:
   `OnStickerHover` sets `vm.HoveredFileName = e.IsEnter ? e.Sticker.FileName : ""`.
   `HideHoverPreview` clears it.
5. Build, run gate, commit.

## Step 2 — Instant preview (commit: feat(ui): show hover preview without delay)

1. `src/StickerPicker/Controls/StickerTile.axaml.cs`: in
   `OnPointerEntered`, remove the `DispatcherTimer` logic; call
   `RaiseHover(isEnter: true)` directly. Remove the `_hoverTimer` field and
   `HoverDelayMs` constant. Keep `OnPointerExited` raising
   `RaiseHover(false)`.
2. Build, run gate, commit.

## Step 3 — Global type-to-search (commit: feat(ui): route typing to search box)

1. `src/StickerPicker/Views/MainWindow.axaml`: give the search `TextBox` an
   `x:Name` (e.g. `SearchBox`).
2. `src/StickerPicker/Views/MainWindow.axaml.cs` ctor:
   `AddHandler(TextInputElement.TextInputEvent, OnGlobalTextInput,
              RoutingStrategies.Tunnel, handledEventsToo: false);`
3. Add `OnGlobalTextInput`:
   - If `SearchBox.FocusState != null` (i.e. it's focused) → return.
   - If `DataContext` is not `MainViewModel` → return.
   - If `e.Text` is null/empty/whitespace → return.
   - If any modifier (Ctrl/Alt) is down → return (so Ctrl+wheel / Ctrl+C
     still work; `TextInput` normally only fires without modifiers but be
     defensive).
   - `SearchBox.Focus();`
   - `vm.SearchText += e.Text;`
   - `e.Handled = true;`
4. Build, run gate, commit.

## Step 4 — Preview opacity setting (commit: feat(settings): adjustable preview opacity)

1. `src/StickerPicker.Core/Models/AppConfig.cs`: add
   `public double PreviewOpacity { get; set; } = 0.92;` and update `Clone()`.
2. `src/StickerPicker/ViewModels/SettingsViewModel.cs`: add observable
   `PreviewOpacity`, wire into `ApplyAndSave` (cases include
   `nameof(PreviewOpacity)`).
3. `src/StickerPicker/Views/MainWindow.axaml` settings overlay: add a
   slider (0.20–1.00, step 0.01) bound two-way to
   `Settings.PreviewOpacity`.
4. `src/StickerPicker/Views/MainWindow.HoverPreview.cs`:
   - In `EnsureHoverPreviewWindow`, read `vm.Settings.PreviewOpacity` and
     set the inner border `Opacity`.
   - Subscribe to `vm.Settings.PropertyChanged`; when `PreviewOpacity`
     changes, update the border opacity live.
5. Build + test (config round-trip), run gate, commit.

## Step 5 — Hotkey toggle semantics (commit: fix: hotkey surfaces inactive window)

1. `src/StickerPicker/Services/AvaloniaWindowChromeService.cs`:
   `ToggleVisible`:
   ```csharp
   RunOnUi(() =>
   {
       if (_window is null) return;
       if (!_window.IsVisible || !_window.IsActive)
       {
           Show(); Activate();
       }
       else
       {
           Hide();
       }
   });
   ```
   Inline the existing helpers as needed; `Show`/`Hide`/`Activate` already
   `RunOnUi` internally — refactor so we don't nest dispatches. Simplest:
   implement the whole body inside one `RunOnUi` block.
2. Build, run gate, commit.

## Step 6 — GPU rendering setting (commit: feat(settings): GPU rendering toggle)

1. `AppConfig`: add `public bool UseGpuRendering { get; set; } = false;`
   and update `Clone()`.
2. `Program.cs`: introduce `LoadStartupConfig()` that constructs `AppPaths`,
   calls `Resolve()`, returns `ConfigStore(paths).Load()`. Replace the env
   var branch with `config.UseGpuRendering`.
3. `SettingsViewModel`: add observable `UseGpuRendering`, persist via
   `ApplyAndSave`.
4. Settings overlay: add checkbox with helper text "重启后生效".
5. Build + test, run gate, commit.

## Step 7 — Unified overlay animation (commit: fix(ui): fade settings overlay as single unit)

1. Investigate the actual stagger source in `MainWindow.axaml` overlay:
   - The `OverlayCard` declares `DoubleTransition` on `Opacity` (good).
   - Child `Button`/`TextBox`/`ListBoxItem` styles declare brush
     transitions. When the card becomes visible, the brushes "snap in" via
     their own transitions, producing stagger.
2. Fix: move the opacity animation to `OverlayMask` (the outer Border) and
   add a transient class `overlay-animating` on the card root during the
   open/close window. Add a style:
   ```xml
   <Style Selector="Border.overlay-root.overlay-animating Button">
     <Setter Property="Transitions" Value="(null)" />
   </Style>
   ```
   (Repeat for TextBox / ListBoxItem / Border.panel.) Or simpler and more
   robust: scope the fade to the mask only and remove the per-control
   `Transitions` from the settings subtree by introducing a
   `.settings-panel` class whose style overrides `Transitions` to null.
3. `MainWindow.SettingsAnimation.cs`:
   - `OpenOverlay`: `OverlayMask.IsVisible = true`; add
     `overlay-animating` class; `Dispatcher.UIThread.Post` → set
     `OverlayMask.Opacity = 1`; after the transition duration, remove the
     class.
   - `CloseOverlay`: add class, set opacity 0, after duration hide + remove
     class.
4. Verify visually that the whole card fades as one unit.
5. Build, run gate, commit.

## Step 8 — Inline tag editor overlay + new tag UX (commit: feat(ui): inline tag editor with chip ux)

This step is larger; consider splitting into two commits if it gets big:

8a. **Tag-editor control + VM state** — commit
   `feat(ui): inline tag editor overlay`.

8b. **Chip-based tag UX** — commit `feat(ui): chip-based tag editor ux`.

### 8a. Inline overlay

1. `MainViewModel.cs`: add
   - `[ObservableProperty] bool IsTagEditorOpen`
   - `[ObservableProperty] StickerItemViewModel? TagEditorTarget`
   - `public void OpenTagEditor(StickerItemViewModel item) { TagEditorTarget = item; IsTagEditorOpen = true; }`
   - `public void CloseTagEditor() { IsTagEditorOpen = false; TagEditorTarget = null; }`
2. New `src/StickerPicker/Controls/TagEditor.axaml` + `.axaml.cs`:
   - UserControl bound to `StickerItemViewModel` via a `TagEditorHost`
     ViewModel or directly to the sticker.
   - Properties: `Tags` (ObservableCollection<string>),
     `ApplyCommand`, `CancelCommand`.
3. `MainWindow.axaml`: add `TagEditorMask` / `TagEditorCard` mirrors of
   the settings overlay, with `<controls:TagEditor />` inside. Bindings
   reach `TagEditorTarget`.
4. `MainWindow.SettingsAnimation.cs` (rename to
   `MainWindow.OverlayAnimation.cs` or add a second partial): replicate the
   open/close logic for the tag editor. Refactor common open/close into a
   helper that takes `(Border mask, Border card)`.
5. `MainWindow.StickerActions.cs`: `EditTagsAsync` → call
   `vm.OpenTagEditor(item)` instead of `PromptForNameAsync`. The actual
   persistence is done by the `TagEditor` control via
   `EditStickerTagsCommand`.
6. Build, run gate, commit 8a.

### 8b. Chip UX

1. `TagEditor.axaml`:
   ```
   <WrapPanel> chips... <TextBox x:Name="TagInput" /> </WrapPanel>
   ```
   Chip = `Border` with the tag text + a small `Button` "×".
2. `TagEditor.axaml.cs`:
   - `TagInput.KeyDown`: Enter → add trimmed text to `Tags`, clear input;
     Backspace with empty input → remove last tag.
   - `RemoveTagCommand` (or click handler) → remove the chip's tag.
   - Apply button → invoke `MainViewModel.EditStickerTagsCommand.ExecuteAsync((Target, Tags.ToArray()))`
     then close the overlay.
   - Cancel button → close without saving.
3. Initial `Tags` populated from `Target.Sticker.Tags`.
4. Build + test, run gate, commit 8b.

## Final verification

```bash
dotnet build StickerPicker.slnx -c Release
dotnet test StickerPicker.slnx -c Release
dotnet format --severity info --verify-no-changes | echo $?
find src tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 \
  | xargs -0 wc -l | awk '$1 >= 400 { print }'
```

LSP diagnostics clean across all changed files.

## Review and rollback gates

- Run a full `trellis-check` review against task artifacts and frontend
  quality spec before finishing.
- If any step regresses existing behavior (preview, hotkey, settings
  persistence, AOT/trim analyzer), revert that commit and re-do.
- Manual Windows smoke (per existing spec) remains required for
  hotkey/clipboard; that is not waived by this task.
