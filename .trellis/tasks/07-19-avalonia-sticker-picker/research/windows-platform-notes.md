# Research: Windows platform notes for Avalonia StickerPicker

Date: 2026-07-19  
Scope: MVP-A global hotkey + tray + clipboard dual-format paste for chat apps.

## Avalonia baseline

- Template: `avalonia.mvvm`, framework `net10.0`, MVVM `CommunityToolkit`, Avalonia package default **12.1.0**.
- Tray: first-class `TrayIcon` in Avalonia (see Avalonia docs / `TrayIcon` API).
- Global hotkeys: **not** built into Avalonia. Community consensus (Avalonia discussions #8823, #16266): use Win32 `RegisterHotKey` + window proc hook, or a library (e.g. SharpHook, GlobalHotKeys.Windows).

## Recommended hotkey approach (MVP)

1. Resolve Win32 HWND for the main Avalonia window after it is created.
2. `RegisterHotKey(hwnd, id, MOD_CONTROL | MOD_SHIFT, 0x45 /* E */)`.
3. Hook `WM_HOTKEY` via Avalonia Win32 WndProc callback (`Win32Properties` / platform-specific API for current Avalonia 12).
4. Marshal to UI thread; toggle show/hide + `Activate`.
5. On settings change: `UnregisterHotKey` then re-register.
6. On failure (conflict): surface error; keep previous binding if re-register fails.

Alternative: message-only hidden window if main window HWND is unreliable when hidden — evaluate during spike.

## Recommended clipboard approach (MVP)

SuzuEmojy reference (`services/clipboard.py`):

- `QMimeData.setUrls([fileUrl])` + `setImageData(QImage)` so chat clients prefer original file (GIF/WebP animation).

Avalonia managed clipboard APIs may not expose full `CF_HDROP` file lists with bitmap simultaneously. Plan:

1. Abstract `IClipboardImageService.CopyImageFile(string absolutePath)`.
2. Windows implementation via P/Invoke / OLE clipboard:
   - File drop list pointing at the library file under `images/`
   - DIB/Bitmap fallback (static frame)
3. Validate manually against QQ and WeChat on Windows 10/11.

Do **not** depend on simulating Ctrl+V into the previous app for MVP; copy + hide is enough (user pastes). Auto-paste is out of scope unless trivial later.

## Theme

- Use stock `FluentTheme` as control templates source.
- Override resources: `CornerRadius` → 0, custom brushes for Steam-like dark (`#1b2838` / `#171a21` family) and light.
- Avoid FluentAvalonia / Semi for MVP to reduce dependency risk.

## Data paths

- Default: `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/StickerPicker`
- Bootstrap pointer file in the **default** LocalAppData app folder so custom roots remain discoverable after restart.

## Open implementation checks (spike)

- [ ] Exact Avalonia 12 API for WndProc hook name/namespace
- [ ] Whether window Hide unregisters hotkey (must not)
- [ ] GIF paste success with file-only vs file+bitmap
