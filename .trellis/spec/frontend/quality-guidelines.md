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
