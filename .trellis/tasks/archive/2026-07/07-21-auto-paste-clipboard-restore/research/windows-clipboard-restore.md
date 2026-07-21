# Windows Clipboard Restore Research

## Relevant Win32 contracts

- `EnumClipboardFormats` enumerates currently available formats while the clipboard is open. The order is meaningful, and enumeration may include formats synthesized by Windows.
- `GetClipboardData` returns clipboard-owned handles. A consumer must copy the data immediately and must not keep or free the returned handle.
- `GetClipboardSequenceNumber` changes when clipboard contents change or are emptied. It can be used to reject a restore after another operation changes the clipboard. Delayed rendering can defer a sequence-number increment until data is rendered.
- `SetClipboardData` transfers ownership of the supplied handle to the system. Most memory-backed formats require `GMEM_MOVEABLE`, but several standard formats use specialized GDI or metafile handles instead.
- A null `SetClipboardData` handle represents delayed rendering. Reproducing such a format requires owning a window and handling `WM_RENDERFORMAT` / `WM_RENDERALLFORMATS`; it cannot be faithfully snapshotted as an ordinary byte buffer.

## Design implications

1. Snapshot data before `EmptyClipboard`; copying only format identifiers or retaining handles is invalid.
2. Use both an operation-specific registered clipboard marker and the post-write sequence number when deciding whether the selected sticker is still current. If either check fails, cancel without modifying the clipboard.
3. Treat snapshot and restore as best-effort platform operations with explicit outcomes. The ViewModel should schedule only a restorable snapshot and should clear countdown UI on cancellation, mismatch, or failure.
4. Generic byte copying can cover common `HGLOBAL`-backed text, HTML, rich text, file-drop, and many application-specific formats. `CF_BITMAP`, `CF_PALETTE`, `CF_METAFILEPICT`, `CF_ENHMETAFILE`, owner-display formats, and delayed-rendered data need special handling or a conservative refusal.
5. Restoring only a subset can silently degrade the user's old clipboard. The conservative default is therefore all-or-nothing: if every advertised format cannot be captured reliably, still copy the sticker but do not promise or schedule restoration.
6. Empty pre-selection clipboard is a valid restorable state: restoration should empty the selected sticker and leave the clipboard empty.
7. Clipboard formats are not uniformly `HGLOBAL`. Known memory-backed standard formats and registered formats (`0xC000..0xFFFF`, documented as requiring `HGLOBAL`) may be copied as bytes. Although `CF_GDIOBJFIRST..CF_GDIOBJLAST` are described as `GMEM_MOVEABLE`, Microsoft documentation is not sufficiently consistent about their destruction convention for this feature's all-or-nothing guarantee, so reject that range conservatively. `CF_BITMAP` / `CF_DSPBITMAP`, `CF_PALETTE`, `CF_METAFILEPICT` / display variants, and `CF_ENHMETAFILE` / display variants require format-specific duplication and recreation. `CF_OWNERDISPLAY`, `CF_PRIVATEFIRST..CF_PRIVATELAST`, a null/unrenderable handle, or an unrecognized ownership contract must make the whole snapshot ineligible.
8. Enumerated synthesized formats may be materialized by `GetClipboardData`; snapshot order and every materialized format must be preserved. A failed enumeration or failed copy of any format invalidates the whole snapshot.
9. Build or duplicate every restore handle before `EmptyClipboard` at restore time. This keeps allocation failures from turning the selected sticker into a partial old snapshot. Unexpected `SetClipboardData` failures remain reportable platform failures.
10. Clipboard data is untrusted and can be very large. Enforce checked per-format and aggregate snapshot limits before allocating; exceeding a limit skips restoration without blocking the sticker copy.
11. `AddClipboardFormatListener` posts `WM_CLIPBOARDUPDATE` whenever clipboard contents change. Register the adapter's hidden owner window as a listener; while a recovery chain is active, compare the current sequence number with the chain's expected post-write value. A mismatch invalidates and disposes the chain immediately and notifies the countdown owner. This avoids polling and releases large snapshots before the original deadline.

## Sources

- Microsoft Learn, `GetClipboardData`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getclipboarddata
- Microsoft Learn, `EnumClipboardFormats`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumclipboardformats
- Microsoft Learn, `GetClipboardSequenceNumber`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getclipboardsequencenumber
- Microsoft Learn, `SetClipboardData`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-setclipboarddata
- Microsoft Learn, Clipboard Operations: https://learn.microsoft.com/windows/win32/dataxchg/clipboard-operations
- Microsoft Learn, `WM_RENDERALLFORMATS`: https://learn.microsoft.com/windows/win32/dataxchg/wm-renderallformats
- Microsoft Learn, `RegisterClipboardFormatW`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-registerclipboardformatw
- Microsoft Learn, Standard Clipboard Formats: https://learn.microsoft.com/windows/win32/dataxchg/standard-clipboard-formats
- Microsoft Learn, `AddClipboardFormatListener`: https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-addclipboardformatlistener
- Microsoft Learn, `WM_CLIPBOARDUPDATE`: https://learn.microsoft.com/windows/win32/dataxchg/wm-clipboardupdate
