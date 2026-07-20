# UX Optimizations Round 2

## Goal

Improve polish and ergonomics of the StickerPicker desktop client (Steam-style). Ten independently shippable UX changes. Each item is implemented and committed separately so progress is reviewable and bisectable.

## User-reported issues

| # | Issue | Desired behavior |
|---|-------|------------------|
| 1 | Ctrl+wheel zoom only fires when grid cannot scroll | Ctrl+wheel **always** zooms; plain wheel scrolls |
| 2 | Cannot quit via tray | Right-click tray → menu with **退出** (already exists); left-click toggles window |
| 3 | No per-sticker management | Right-click sticker → context menu: 编辑标签 / 移动到分类 / 删除 (file rename out of scope — no Core API) |
| 4 | Thumbnails blurry at high zoom | `Bitmap.DecodeToWidth(tileSize)` is one-shot; upscale beyond decode size blurs |
| 5 | No hover preview | New **设置项：预览**; when ON, hovering a sticker shows large image beside cursor (semi-transparent) |
| 6 | Settings panel has no animation | Add open/close fade+scale transition |
| 7 | No UI feedback when capturing hotkey | Capture box shows a distinct focused/listening state |
| 8 | Buttons always bordered (not Steam-like) | Toolbar/icon buttons borderless at rest; border only on hover/pressed |
| 9 | Two import buttons | Single **导入** button → split dropdown: 导入文件 / 导入文件夹 |
| 10 | Header bar redundant | Remove the in-app header bar (the dark strip showing "STICKERPICKER" + refresh/settings buttons); move refresh/settings buttons into the toolbar next to search/import |

## Out of scope

- New settings persistence format changes beyond adding `HoverPreview` flag
- Cross-platform tray behavior beyond existing `NativeMenu` (Windows-first)
- Replacing the Steam theme palette

## Acceptance (per item, committed individually)

1. **Zoom priority** — In the sticker grid, Ctrl+wheel changes thumbnail size regardless of scroll position; plain wheel scrolls. No accidental page scroll during zoom.
2. **Tray quit** — Right-click tray icon → menu appears with 显示 / 设置 / 退出; clicking 退出 fully exits the app. Left-click toggles window visibility. (Verify existing menu actually surfaces; fix if broken.)
3. **Sticker context menu** — Right-click a sticker opens a menu offering: 编辑标签, 移动到分类 (submenu), 删除. (File-level rename is out of scope: Core has no `RenameStickerFile` API and adding one is beyond this task.) Actions call existing library APIs; status text reflects result; no crash on virtual/null category.
4. **Sharp zoom** — Thumbnails stay crisp when zoomed up to the max (256px). Decode size no longer hard-capped at the initial tile size. Memory stays reasonable (decode cap ~ 512-768px or use full image when small enough).
5. **Hover preview** — New settings toggle `HoverPreview` (default ON). When ON, hovering a tile for ~250ms shows a large preview popup anchored near the cursor, semi-transparent (~0.9 opacity). Disappears on mouse leave immediately. Respects the toggle.
6. **Settings animation** — Settings overlay animates in (fade + slight scale) and out. Smooth, <200ms. No layout jank. Disabled/transitions-off environments still work.
7. **Hotkey capture feedback** — When HotkeyCaptureBox is focused/listening, it shows a clear visual state (accent border + pulse + "按下组合键…" text). Reverts when gesture captured or focus lost.
8. **Borderless-at-rest buttons** — Toolbar/icon/subtle buttons have no visible border at rest; border + subtle bg appear on hover; pressed uses accent dim. Primary action keeps its filled style. Text buttons in dense rows stay readable.
9. **Merged import** — A single **导入** button with a dropdown offering 导入文件 and 导入文件夹. Both flows reuse existing import logic. Keyboard accessible.
10. **No header bar** — The in-app header bar (dark strip with "STICKERPICKER" text + refresh/settings) is removed. Refresh + settings buttons move into the existing toolbar (right of search/import). The OS window title bar is untouched (still shows the app name and provides move/minimize/close via the OS chrome). Layout collapses cleanly with no empty gaps; status bar at the bottom is retained.

## Global acceptance (every commit)

- `dotnet build` green; `dotnet test` green
- `dotnet format --severity info --verify-no-changes` exit 0
- All `.cs` files < 400 lines
- No LSP info/hint diagnostics introduced (resolve at root, no `#pragma`)
- Light + dark theme both correct for any visual change
