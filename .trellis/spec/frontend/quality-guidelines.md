# Quality Guidelines (Desktop UI)

> Standards for Avalonia UI project.

---

## Overview

UI is thin: bind ViewModels to Core seams; platform adapters implement OS behavior.

---

## Required

- Steam-like: **no corner radius**, restrained animation, dark palette inspired by Steam greys/blues.
- Click sticker → clipboard (file drop + bitmap when possible) → **hide** window.
- Tray residency; hotkey toggle show/hide (Windows).
- Settings persist theme, topmost, hotkey, data root via Core config/paths.
- C# files **&lt; 400 lines**.

---

## Forbidden

- Reimplementing library scan/import in ViewModels
- Calling Win32 from ViewModels (use `IHotkeyService` / `IClipboardImageService`)
- Exiting app on window close

---

## Known residual risks (document, don’t hide)

- Non-BMP DIB fallback is limited; chat apps primarily use file drop for GIF.
- Grid not virtualized yet — large libraries may need ItemsRepeater later.
- Manual Windows verification required for QQ/WeChat paste.
- Selection interaction now clears search and optionally restores clipboard; every successful selection must copy → clear search → decide hide → optionally restore focus / schedule recovery. See `clipboard-restore-contract.md`.

---

## Selection, clipboard restore, and auto-paste contract

The selection flow is one user-visible transaction spanning the tile click,
Core clipboard seam, platform foreground/input adapter, ViewModel countdown,
and status bar. The full executable contract lives in
`clipboard-restore-contract.md`. Read it before touching selection, the
clipboard service, the foreground-input service, settings, or the status
bar. Key invariants enforced there:

- `IClipboardImageService` owns **one** active recovery chain; callers see
  only `ClipboardCopyResult` plus a `RecoveryInvalidated` event — never raw
  sequence numbers, markers, handles, or snapshot data.
- `IForegroundInputService` holds a **one-round** target captured only at
  hotkey time; tray/startup paths invalidate it. Paste runs only after
  `SetForegroundWindow` + exact `GetForegroundWindow` equality.
- `SelectionCoordinator` is a pure, injectable (`TimeProvider`) seam shared
  between the host and Core tests; never put Avalonia or Win32 types in it.

---

## XAML runtime pitfalls (caused a startup crash — keep this list growing)

These are **not** caught by `dotnet build`. Verify by actually running the app,
or by mentally tracing how each Setter value is parsed at load time.

### BoxShadow (and other type-converted string properties)

`BoxShadow` setters are parsed at runtime via `BoxShadows.Parse`, which treats
the value string as a literal (offset/blur/spread/**color**). **Markup
extensions inside the string do NOT work**:

```xml
<!-- WRONG: throws FormatException 'Invalid color string: \'{DynamicResource\'' at startup -->
<Setter Property="BoxShadow" Value="0 0 0 2 {DynamicResource SteamAccentBrush}" />

<!-- RIGHT: literal color -->
<Setter Property="BoxShadow" Value="0 0 0 2 #66c0f4" />
```

Rule: any property whose value is a **type-converted string** (BoxShadow,
CornerRadius, Point, Vector, Matrix, GridLength, Thickness-of-non-brush)
MUST use literal values. If a themed color is needed, either hard-code a
theme-shared literal (Steam accent is `#66c0f4` in both themes) or set the
property from code-behind using the resolved `IBrush`. `BorderBrush` /
`Background` / `Foreground` setters are typed as `IBrush`, so they resolve
`{DynamicResource}` normally — only the parsed-string properties fail.

### MenuItem.Click / ContextMenu.Opening in XAML

Avalonia 12.x **rejects** XAML string handlers for `MenuItem.Click` (wants
`EventHandler<RoutedEventArgs>`, AVLN3000) and `ContextMenu.Opening`
(AVLN2000). `Button.Click` string handlers DO work. Wire the others in
code-behind: give the element `x:Name`, then `item.Click += Handler;` in the
ctor.

### Late DataContext

`App.axaml.cs` assigns `MainWindow.DataContext` **after** `new MainWindow()`.
Code-behind that reads `DataContext` in the ctor sees `null`. Subscribe to
`DataContextChanged` and (re)attach any VM event handlers there.

### TrayIcon bindings on a non-INPC Application

`TrayIcon` declared in `App.axaml` under `TrayIcon.Icons` resolves
`{Binding}` against `Application.DataContext` at XAML load time. `App` is not
`INotifyPropertyChanged`, and the bound properties (`TrayMenu`, commands)
are usually built later in `OnFrameworkInitializationCompleted`, so the
binding evaluates once against `null` and the tray right-click menu never
appears. Do NOT bind `Command`/`Menu` in XAML — assign them imperatively
after constructing the menu:

```csharp
var icon = TrayIcon.GetIcons(this)?.FirstOrDefault();
if (icon is not null) { icon.Command = ...; icon.Menu = ...; }
```

(Note: `x:Name` is NOT supported on `TrayIcon` — AVLN2000.)

### WindowDecorations replaces SystemDecorations

`Window.SystemDecorations` is obsolete in Avalonia 12.x. Use the static
`WindowDecorations` enum: `WindowDecorations = WindowDecorations.None`
(qualify with the type name — the members are static fields, not instance).

### TextInputEvent carries the IME-composed string

For "global type-to-search" behavior, hook the tunnel/bubble routed event
`InputElement.TextInputEvent` (NOT `KeyDown`):

```csharp
AddHandler(InputElement.TextInputEvent, OnGlobalTextInput,
           RoutingStrategies.Tunnel);
```

`TextInputEventArgs.Text` is the **final composed string**, so CJK IME
input Just Works without per-keystroke handling. Hooking `KeyDown`
instead would double-fire during IME composition and break CJK. Modifier-
only / function / navigation keys are not `TextInput` and are naturally
ignored — no need to filter them.

For the first input received while the search box is unfocused, update
`SearchBox.Text` before focusing it, then collapse `CaretIndex`,
`SelectionStart`, and `SelectionEnd` to the new text length. Do not set the
caret from the old text and then update only the ViewModel: the binding update
can leave the newly focused box at the old insertion point, so a sequence such
as `123` becomes `231`.

Global type-to-search must be suspended while modal UI is visible. This
includes inline overlay masks, visible owned dialog windows, and routed input
whose source belongs to another `TopLevel` (for example a context-menu popup).
Editable controls keep their native input behavior and must never be routed to
search.

### Window.IsActive covers topmost-but-unfocused

`Window.IsActive` is true only when the window has keyboard focus. A
`Topmost=true` window that has been shadowed by another app reports
`IsActive == false` even though it is visible. For hotkey toggle
semantics, treat `!IsVisible || !IsActive` as "needs to surface";
surfaceing logic that only checks `IsVisible` will hide a window the
user expected to come forward.

### Overlay fade stagger

Fading a nested `Border.Opacity` whose children carry their own
`BrushTransition`s (Buttons, TextBoxes, ListBoxItems — they inherit
them from `SteamStyles.axaml`) produces a **per-child stagger**: each
child's `Background`/`BorderBrush` re-evaluates its transition when
the card becomes visible, on top of the card's own fade.

Fix: put the `DoubleTransition` on the **outer mask** (the full-subtree
Border), not the inner card. `Opacity` is a single inherited property,
so the whole tree alpha-blends as one block and child brushes settle
exactly once. This is why `MainWindow.SettingsAnimation.cs` fades
`OverlayMask`, not `OverlayCard`.

### Transparent top-level preview windows

A transparent child visual does not make a native top-level window transparent.
For an Avalonia `Window` that must reveal other applications underneath, use
all of the following together:

```csharp
WindowDecorations = WindowDecorations.None,
Background = Brushes.Transparent,
TransparencyLevelHint = [WindowTransparencyLevel.Transparent],
Opacity = previewOpacity,
```

Apply the user-facing opacity to the top-level `Window`, not only to an
`Image` or `Border` child. Without `TransparencyLevelHint`, the Win32
software-rendering path can retain an opaque native surface and blend child
pixels against Avalonia's fallback background, producing a washed-out mask
instead of revealing the application behind it. This is a Windows runtime
behavior and requires a manual smoke test at opacity `1.0`, `0.5`, and the
minimum supported value after Avalonia or Windows upgrades.

### Top-level preview positioning uses physical working-area pixels

`Window.Position`, `PointToScreen`, `Screen.Bounds`, and `Screen.WorkingArea`
use physical pixels. Avalonia layout dimensions and spacing constants use DIP.
When positioning a preview window:

- Select the screen from the unshifted cursor point, so a candidate position
  across a monitor edge cannot select the wrong screen.
- Use `Screen.WorkingArea`, not `Bounds`, to exclude taskbars and reserved OS
  areas.
- Convert the preview's DIP size, cursor offset, and safety margin with the
  selected screen's `Scaling` before comparing them with the working area.
- Use `Bitmap.Size` for the image's DIP size; `PixelSize` is the decoded pixel
  count and can differ when image DPI metadata is not 96.
- Flip before crossing the right or bottom edge, then clamp both axes to the
  working area as a final guard for small work areas and unusual taskbar
  placement.

```csharp
var cursor = PointToScreen(windowRelative);
var screen = Screens.ScreenFromPoint(cursor);
var workArea = screen.WorkingArea;
var previewWidthPx = (int)Math.Ceiling(previewWidthDip * screen.Scaling);
```

Using DIP dimensions directly against `WorkingArea` delays the flip at scaling
above 100%, allowing a substantial part of the preview to enter the taskbar or
leave the monitor before its position changes.
