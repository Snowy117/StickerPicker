# Windows Foreground Restore and Paste Input Research

## Relevant Win32 contracts

- `GetForegroundWindow` returns the window with which the user is currently working, but can temporarily return null.
- `SetForegroundWindow` requests foreground activation. Windows restricts foreground changes and may refuse the request even when an application appears eligible.
- `SendInput` serially inserts keyboard events into the system input stream. It is subject to UIPI and can inject only into applications at an equal or lower integrity level.
- Existing physical modifier state can interfere with injected input. A paste sequence must contain balanced key-down/key-up events and must not be sent unless the intended target has actually become the foreground window.

## Design implications

1. Capture a fresh foreground window only when a global hotkey is about to surface StickerPicker from an external application.
2. Invalidate the captured target when the hotkey hides an already active StickerPicker, or when tray/startup paths show the window.
3. After selection, hide StickerPicker as configured, validate the captured handle, request foreground activation, and confirm that the same handle became foreground before injecting `Ctrl+V`.
4. If the target is invalid or foreground activation is refused, do not inject input into whichever window happens to be foreground. Copy and clipboard-restoration behavior remain successful independently.
5. `SendInput` can still fail against an elevated target due to UIPI; expose a non-fatal status/error outcome rather than treating copy as failed.

## Sources

- Microsoft Learn, `GetForegroundWindow`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getforegroundwindow
- Microsoft Learn, `SetForegroundWindow`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setforegroundwindow
- Microsoft Learn, `SendInput`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-sendinput
- Microsoft Learn, `KEYBDINPUT`: https://learn.microsoft.com/windows/win32/api/winuser/ns-winuser-keybdinput
