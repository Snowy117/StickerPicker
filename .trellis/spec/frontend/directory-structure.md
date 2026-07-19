# Directory Structure (Desktop UI)

> How Avalonia UI code is organized in StickerPicker.

---

## Overview

`src/StickerPicker` is the Avalonia Desktop host. It depends on `StickerPicker.Core` and must not reimplement library IO.

---

## Directory Layout

```text
src/StickerPicker/
├── App.axaml(.cs)         # DI/composition, tray, lifecycle
├── Program.cs
├── Views/                 # MainWindow shell
├── ViewModels/            # CommunityToolkit MVVM
├── Controls/              # Custom controls (e.g. StickerTile)
├── Themes/                # Steam-like (CornerRadius=0) styles
├── Platform/Windows/      # Win32 adapters (clipboard, hotkey)
└── Services/              # AvaloniaWindowChrome + null stubs
```

Solution file: **`StickerPicker.slnx`** (not classic `.sln`).

---

## Module Organization

| Layer | Depends on |
|-------|------------|
| Views | ViewModels, Controls, Themes |
| ViewModels | Core abstractions only |
| Platform/Windows | Core seams (`IClipboardImageService`, `IHotkeyService`) |

**Hard rules**

- Every C# file **&lt; 400 lines** (split collaborators before growing `MainViewModel`).
- OS-specific code stays under `Platform/` or null stubs in `Services/`.
- Close main window = **Hide**; exit only via tray/explicit shutdown.

---

## Naming Conventions

- `*ViewModel`, `*Service`, Windows types prefixed `Windows*`
- Product / window title / AppData folder: `StickerPicker`

---

## Examples

- Composition root: `Services/ServiceFactory.cs`, `App.axaml.cs`
- Steam styles: `Themes/SteamStyles.axaml`
