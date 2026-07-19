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
