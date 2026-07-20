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

## NativeAOT publish

Release publishing uses NativeAOT. Pass a runtime identifier for the target
platform and build on that platform; NativeAOT does not support cross-OS
compilation.

Windows x64 (primary release target, run on Windows with the C++ build tools):

```powershell
dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r win-x64 --self-contained true
```

Linux x64 (run on Linux with the native compiler toolchain):

```bash
dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r linux-x64 --self-contained true
```

The publish directory contains the native executable and required native
SkiaSharp/HarfBuzz sidecars. Validate GUI and platform integrations on the
matching target OS before shipping.

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
