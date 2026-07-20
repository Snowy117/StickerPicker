# NativeAOT publishing

## Goal

Make StickerPicker publish successfully with .NET 10 NativeAOT without suppressing actionable trim/AOT diagnostics or regressing existing configuration, library, and UI behavior.

The primary shipping target is `win-x64`. Development-host verification in this task uses `linux-x64`; Windows publishing and runtime certification must run on a native Windows host because NativeAOT does not support cross-OS compilation.

## Background

- `dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r linux-x64 -p:PublishAot=true` currently reports `IL2026` and `IL3050` for reflection-based `System.Text.Json` calls in `AtomicJson`.
- `App.axaml` registers a reflection-based `ViewLocator` whose implementation is explicitly annotated as unsafe for trimming.
- The executable has no durable NativeAOT publish configuration, so normal builds do not continuously detect these regressions.
- Avalonia, SkiaSharp, Windows hotkey/clipboard interop, and Chinese-language behavior must be retained.

## Requirements

1. Replace reflection-based JSON serialization with `System.Text.Json` source-generated metadata for every persisted document root:
   - `AppConfig` / `WindowGeometry`
   - `MetadataDocument` / `StickerMetadataEntry`
   - `HashesDocument`
   - bootstrap data-root document
2. Preserve the existing JSON contract: camel-case names, case-insensitive reads, indented writes, null omission, comments, trailing commas, atomic replacement, corrupt-file backup, and recreate-on-corruption behavior.
3. Make accidental fallback to reflection-based JSON fail during ordinary builds/tests rather than only during NativeAOT publishing.
4. Remove the unused reflection-based Avalonia view locator. If view-model navigation is later needed, it must use explicit typed mappings instead of runtime type-name lookup.
5. Enable AOT compatibility analysis for both production projects and durable NativeAOT publishing for the executable without hard-coding one RID.
6. Keep globalization enabled and retain Avalonia, ItemsRepeater, SkiaSharp, tray, hotkey, and clipboard functionality.
7. Do not add blanket trim roots, warning suppressions, `DynamicDependency`, or `#pragma` directives to hide compatibility diagnostics.
8. All existing tests and formatting/analyzer gates remain green; every `.cs` file remains below 400 lines.

## Out of Scope

- Replacing Avalonia, SkiaSharp, or ItemsRepeater.
- Producing a Windows executable from Linux; cross-OS NativeAOT compilation is unsupported.
- Adding new application features or changing persisted JSON file formats.
- Claiming Windows runtime certification without a real Windows x64 host.

## Acceptance Criteria

- [x] `dotnet build StickerPicker.slnx -c Release` succeeds with AOT/trim analyzers enabled and no warning suppression.
- [x] `dotnet test StickerPicker.slnx -c Release` succeeds.
- [x] `dotnet format --severity info --verify-no-changes` exits 0.
- [x] JSON round-trip, missing-file creation, corrupt backup/recovery, and atomic overwrite tests exercise source-generated metadata with reflection serialization disabled.
- [x] `dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r linux-x64 --self-contained true` succeeds and produces a native ELF executable plus required native Skia/HarfBuzz payloads.
- [x] The native Linux executable reaches Avalonia initialization; a headless `XOpenDisplay` failure is acceptable evidence only for startup/link loading, not UI certification.
- [x] Repository search finds no reflection-based `JsonSerializer` overloads and no `Type.GetType`/`Activator.CreateInstance` view locator.
- [x] A documented `win-x64` command is ready to run on native Windows, where publication plus startup/tray/hotkey/clipboard/image-decoding smoke tests remain required.
- [x] No modified `.cs` file reaches 400 lines and LSP diagnostics contain no unresolved info/hint/warning/error findings.

## Notes

- Detailed compatibility evidence is recorded in `research/native-aot.md`.
- Linux validation on 2026-07-20: Release build produced 0 warnings/errors;
  68/68 tests passed; format and LSP diagnostics were clean; NativeAOT emitted
  a 64-bit ELF executable with SkiaSharp/HarfBuzz sidecars and reached Avalonia
  initialization before the expected headless `XOpenDisplay` failure.
- `win-x64` still requires native Windows publication and runtime smoke testing;
  Linux cannot provide cross-OS NativeAOT certification.
