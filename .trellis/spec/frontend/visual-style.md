# Visual Style (Desktop UI)

> The canonical visual language for StickerPicker. All UI work must match
> this brief; deviations require a spec update.

---

## Style identity

StickerPicker's UI is a fusion of three reference modes:

- **Cyber-Industrial Dark Mode** — deep, low-saturation surfaces, mechanical
  precision, no decorative ornamentation.
- **Tactical High-Density UI** — compact spacing, maximum information per
  pixel, panels read as instrument clusters rather than marketing surfaces.
- **Gamer Glassmorphism** — subtle frosted depth, faint micro-gradient
  borders, restrained accent glows on active/selected states only.

The previous "Steam desktop" wording is a subset of this identity (Steam's
dark grey-blue palette and 2px corner radius are retained as the concrete
token values). This document generalizes it.

---

## Concrete tokens (dark mode is canonical)

| Token | Value | Use |
|---|---|---|
| `SteamDarkBg` | `#171a21` | App background, lowest surface |
| `SteamDarkPanel` / `SteamDarkHeader` | `#1b2838` | Panels, header bars |
| `SteamDarkPanelAlt` | `#2a475e` | Inset surfaces, chip backgrounds |
| `SteamDarkHover` | `#31618b` | Hover state fill |
| `SteamDarkBorder` / `SteamDarkBorderSoft` | `#3d4450` / `#2a3a4d` | Hard border lines (1px) |
| `SteamDarkText` / `SteamDarkTextBright` | `#c7d5e0` / `#ffffff` | Body / emphasized text |
| `SteamDarkMuted` | `#8f98a0` | Secondary, status bar, counts |
| `SteamAccent` (neon cyan) | `#66c0f4` | Selection, focus rings, section headers |
| `SteamAccentDim` | `#417a9b` | Pressed / inactive accent |
| `SteamPrimaryBrush` gradient | `#75b022` → `#588a1b` | Primary action (Steam green) |

These live in `Themes/SteamTheme.axaml` and are swapped at runtime by
`App.ApplySteamBrushes`. Light palette exists only as a legacy fallback;
dark is the supported target.

---

## Layout rules

- **Corner radius**: hard maximum **2px** on every control. Set via
  `ControlCornerRadius`, `OverlayCornerRadius`, `SteamTileCornerRadius`.
- **Borders**: 1px, hard-edged, `SteamBorderBrush` / `SteamBorderSoftBrush`.
  Fine and mechanical, never soft drop shadows as the primary separator.
- **Density**: compact padding (e.g. `10,5` on buttons, `10,3` on the status
  bar). Surfaces should look like an instrument cluster, not a mobile app.
- **Micro-gradient borders**: a single subtle vertical gradient
  (`SteamHeaderGradientBrush`, panel→bg) is the only decorative gradient
  allowed on chrome. No大面积彩虹渐变.
- **Frosted feel**: achieved via the dark panel-alt inset color and tight
  1px borders, not via blur effects. Do not add acrylic/blur — it conflicts
  with the software-rendering target on Windows.

---

## Accent & activation states

- **Default** state is muted: panel-alt background, soft border.
- **Hover** raises the fill to `SteamHoverBrush` and the border to
  `SteamAccentBrush`. A 150ms brush transition is allowed on hover.
- **Selected / focus** uses `SteamAccentDimBrush` fill with bright text, or
  `SteamAccentBrush` fill with dark text (`#0d1419`) for keyboard-focused
  selected items (maximum contrast for the active instrument).
- **Accent glow** is reserved for the hotkey-capture listening state
  (`HotkeyCaptureBox.listening` — animated `BoxShadow` `0 0 0 2..4 #66c0f4`).
  Do not add glow to ordinary hover/selected states; that dilutes the
  "activation" signal.

---

## Typography & iconography

- **Typeface**: `Segoe UI, Noto Sans CJK SC, sans-serif` (set on the
  `Window` style). Body 13px; section headers 11px SemiBold letter-spaced
  0.6; window header SemiBold letter-spaced 0.8.
- **Icons**: monochrome line glyphs only. Colored icons are forbidden.
  Activation state is conveyed by the accent color (text or border), never
  by recoloring the glyph itself.

---

## Animation discipline

- Brush transitions ≤150ms. Opacity fades on overlays ≤160ms.
- Overlays (settings, tag editor) fade **as a single unit** — child
  controls must not run their own transitions during the fade (see
  `quality-guidelines.md` → "Unified overlay animation"). Staggered
  per-child animation is a bug.
- No bounce, no spring physics, no parallax. Motion is mechanical.

---

## Forbidden

- Corner radius > 2px.
- Drop shadows as the primary surface separator (borders carry that load).
- Colored / filled icons.
- Large radial or rainbow gradients.
- Blur / acrylic effects (conflicts with software rendering).
- Staggered child animations on overlay open/close.
- Decorative whitespace that reduces information density.

---

## Validation

Visual changes must be checked by running the app (XAML runtime pitfalls
are not caught by `dotnet build`; see `quality-guidelines.md`). Codified
mechanical checks:

```bash
dotnet build StickerPicker.slnx -c Release
dotnet format --severity info --verify-no-changes | echo $?
find src tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' \
  | xargs wc -l | awk '$1 >= 400 { print }'
```

File-size and analyzer gates apply as usual.
