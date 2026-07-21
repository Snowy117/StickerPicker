# GUI Overhaul

## Goal

Polish StickerPicker's desktop GUI across 10 interrelated points (spec update,
status bar naming, instant preview, global-type-to-search, preview opacity,
hotkey toggle semantics, in-settings GPU rendering, overlay tag editor, unified
overlay animation, and a redesigned tag-edit UX). Each point lands as its own
commit on a dedicated feature branch.

## Background

The current UI is Steam-flavored but has these rough edges:

- Tile names rely on a `ToolTip` on the tile border; status bar shows only
  counts.
- Hover preview waits 250ms then decodes — user perceives lag.
- Keyboard input outside the search `TextBox` does nothing; users must click it.
- Hotkey toggle simply flips visibility, which (combined with AlwaysOnTop)
  hides a window the user expects to surface.
- GPU rendering is controlled by an env var (`STICKERPICKER_USE_GPU`), not a
  user-visible setting.
- The edit-tags flow opens a separate modal `Window` (via `PromptForNameAsync`),
  inconsistent with the inline Settings overlay.
- The Settings overlay animates child-by-child because each control has brush
  transitions plus the container opacity fade — looks like a stagger effect.
- The tag edit dialog itself is a single comma-separated text field; painful
  for many tags.

There is no spec capturing the intended visual style; the user supplied a
"Cyber-Industrial / Tactical High-Density / Gamer Glassmorphism" brief that
should be persisted.

## Requirements

0. **Style spec** — If no trellis spec captures the visual language, add a
   concise spec (frontend) describing: Cyber-Industrial Dark / Tactical
   High-Density / Gamer Glassmorphism; deep grey-blue base in dark mode;
   neon cyan/green accents; subtle glowing micro-gradient borders;
   high-density cards; ≤2px corner radius; sans-serif; monochrome line icons
   with small accent-color activation states; micro-gradient frosted feel;
   fine hard border lines.
1. **Status bar naming** — Do not use a `ToolTip` to show a sticker's file
   name. Instead surface the hovered sticker's file name in the status bar.
2. **Instant preview** — Show the hover preview immediately on pointer enter
   (no 250ms delay). Decode may still be async; the window just appears as
   soon as the bitmap is ready.
3. **Global type-to-search** — When the main window has focus and the search
   `TextBox` is not already focused, any printable key (English or CJK IME
   composition) should route the character to the search box and focus it,
   without interfering with modifier-only / navigation / IME hotkeys.
4. **Preview opacity setting** — Add a slider in settings to control the
   hover preview window's opacity (a persisted `PreviewOpacity` field).
5. **Hotkey toggle semantics** — Hotkey behavior becomes: if the window is
   not visible OR (visible but neither activated nor topmost), bring to
   front; otherwise hide to tray. `IWindowChromeService.ToggleVisible` is
   the implementation seam; preserve platform neutrality.
6. **GPU rendering setting** — Move GPU-vs-software rendering choice from
   the env var to a persisted setting (`UseGpuRendering`) selected in
   Settings; the Avalonia `Win32PlatformOptions.RenderingMode` must honor it
   at app startup.
7. **Inline tag editor** — Stop opening a separate `Window` for tag editing.
   Render the tag editor as a second overlay card (same look as the settings
   overlay). Move/delete/confirm dialogs may remain simple, but tag editing
   becomes an inline overlay.
8. **Unified overlay animation** — Settings (and now tag editor) overlays
   must fade in/out as a single unit. No child-by-child stagger. Investigate
   and fix the stagger cause.
9. **Redesigned tag UX** — Replace the single comma-separated text field
   with a proper tag UX: chips/tags that can be added via a small input and
   removed individually, supporting Enter to add and Backspace-to-delete
   when the input is empty. Persist via the existing `EditStickerTagsCommand`.

## Out of Scope

- Cross-platform GPU toggle for non-Windows hosts (Linux/macOS stay default).
- Replacing SkiaSharp, Avalonia, or ItemsRepeater.
- Changing persisted JSON schema beyond adding the two new config fields.
- Multi-window / tab navigation architecture.
- Theme palette redesign (the spec addition is documentation only).

## Acceptance Criteria

- [ ] Visual-style spec committed under `.trellis/spec/frontend/`.
- [ ] Hovered sticker file name appears in status bar; no `ToolTip.Tip` on
      tile.
- [ ] Pointer enter shows the preview window as soon as the decode completes
      (no fixed delay before decode starts).
- [ ] Typing a printable key with the window focused routes the character
      into the search box and focuses it, without swallowing IME compose or
      breaking Ctrl+wheel thumbnail zoom.
- [ ] New `PreviewOpacity` setting persisted in `AppConfig`, honored by the
      hover preview, adjustable via a Settings slider.
- [ ] Hotkey surfaces the window when not-active/not-topmost and hides only
      when currently active+visible.
- [ ] New `UseGpuRendering` setting persisted in `AppConfig`; Windows
      startup picks `Win32PlatformOptions.RenderingMode` accordingly;
      `STICKERPICKER_USE_GPU` env var no longer drives behavior.
- [ ] Tag editing happens inside an overlay card, not a separate `Window`;
      card shares the settings overlay styling.
- [ ] Settings overlay fades as a single unit; no per-child stagger.
- [ ] Tag editor UX supports chip add/remove (Enter to add,
      Backspace-when-empty to delete last), no comma-joined text field.
- [ ] All existing tests + `dotnet build StickerPicker.slnx -c Release` and
      `dotnet format --severity info --verify-no-changes` remain green.
- [ ] Each modified `.cs` file stays under 400 lines; LSP info/hint
      diagnostics remain clean.
- [ ] Each point lands as its own commit on `feat/gui-overhaul`.

## Notes

- Existing overlay mask/animation lives in
  `src/StickerPicker/Views/MainWindow.axaml` (`OverlayMask` / `OverlayCard`)
  and `MainWindow.SettingsAnimation.cs`.
- `MainViewModel.IsSettingsOpen` toggles the overlay; a parallel
  `IsTagEditorOpen` + a sticker payload is the natural extension.
- `AppConfig` is source-generated for JSON via
  `StickerPicker.Core.Json.CoreJsonContext`; new persisted fields must be
  added to that context graph or to `AppConfig` (already a root).
- AOT/trim analyzers are enabled (`IsAotCompatible=true`), so no reflection
  fallback is permitted anywhere.
