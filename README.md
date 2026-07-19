# StickerPicker

Local sticker library and quick-send tool built with **Avalonia 12 + .NET 10**.

## Run

```bash
dotnet run --project src/StickerPicker
```

Build / test:

```bash
dotnet build StickerPicker.slnx
dotnet test StickerPicker.slnx
```

## Defaults

| Item | Value |
|------|--------|
| Window / tray title | `StickerPicker` |
| Default data root | `%LocalAppData%/StickerPicker/` (Linux: `~/.local/share/StickerPicker`) |
| Global hotkey | `Ctrl+Shift+E` (Windows; changeable in Settings) |

## Data layout

```text
<data-root>/
  library/<CategoryName>/<sticker files>
  metadata.json
  hashes.json
  config.json
```

Custom data roots are remembered via:

```text
%LocalAppData%/StickerPicker/bootstrap.json
```

Categories are **folders** under `library/`. Reorganize in Explorer and press **刷新**.

## MVP features

- Import images (png/jpg/gif/webp) with SHA-256 dedupe
- Category sidebar + search
- Click sticker → clipboard (Windows: file drop + optional DIB) → hide window
- Tray stay resident (close hides; exit from tray)
- Theme (system/dark/light), always-on-top, data directory switch

## Platform notes

- Global hotkey and dual-format clipboard are implemented for **Windows**.
- On Linux/macOS the UI and library work; hotkey/clipboard degrade to stubs.
