# NativeAOT compatibility research

Date: 2026-07-20  
Scope: repository inspection and non-production publish/build experiments for .NET 10, Avalonia 12.1, and SkiaSharp 3.119.4.

## Executive conclusion

The application is close enough to NativeAOT that a same-host `linux-x64` publish emits a native executable, but it is **not currently AOT-clean** and should not be considered releasable yet. There are two actionable code blockers:

1. reflection-based `System.Text.Json` in `AtomicJson` produces four IL2026/IL3050 diagnostics; and
2. the registered reflection-based `ViewLocator` produces an additional linker IL2026 diagnostic.

The project also has no persistent NativeAOT/RID settings. A Linux machine cannot produce or certify the intended Windows binary: NativeAOT does not support cross-OS compilation. Each shipping OS therefore needs a native-host publish and runtime smoke test, including its native Skia/HarfBuzz payload.

## Findings

### Blocker — reflection-based JSON is incompatible with trim/AOT analysis

**Evidence**

- `src/StickerPicker.Core/Json/AtomicJson.cs:34` calls `JsonSerializer.Deserialize<T>(string, JsonSerializerOptions)`.
- `src/StickerPicker.Core/Json/AtomicJson.cs:55` calls `JsonSerializer.Serialize<T>(T, JsonSerializerOptions)`.
- With `IsAotCompatible=true`, the repository-wide `TreatWarningsAsErrors=true` from `Directory.Build.props` turns these into four errors:
  - line 34: IL2026 and IL3050;
  - line 55: IL2026 and IL3050.
- A full same-host `linux-x64` NativeAOT publish repeats the same four whole-program warnings.
- Concrete serialized roots are:
  - `AppConfig` / `WindowGeometry`: `src/StickerPicker.Core/Models/AppConfig.cs`, called by `src/StickerPicker.Core/Config/ConfigStore.cs:14,26`;
  - `MetadataDocument` / `StickerMetadataEntry` and `HashesDocument`: `src/StickerPicker.Core/Library/LibraryDocuments.cs`, called by `src/StickerPicker.Core/Library/LibraryIndexStore.cs:16-23`;
  - private nested `AppPaths.BootstrapDocument`: `src/StickerPicker.Core/Paths/AppPaths.cs:56,95,113-116`.

**Required implementation**

- Add a source-generated `JsonSerializerContext` in `StickerPicker.Core`, with `[JsonSerializable]` roots for `AppConfig`, `MetadataDocument`, `HashesDocument`, and `BootstrapDocument`; referenced member types such as `WindowGeometry` and `StickerMetadataEntry` are then included transitively.
- Preserve the current options (`camelCase`, case-insensitive reads, omit null, comments, trailing commas, indented output) via `JsonSourceGenerationOptions` or a context constructed with equivalent options.
- Change `AtomicJson.LoadOrCreate` and `Save` to accept/use `JsonTypeInfo<T>` (or another statically resolved context contract), and pass the corresponding generated metadata at every call site. Merely annotating `AtomicJson` with warning suppressions or trimmer roots does not remove the runtime-code-generation dependency.
- `BootstrapDocument` is private and nested. A central context cannot name it as currently declared, so either make it an internal top-level model or place the relevant generated context where the type is accessible.
- Add round-trip/corrupt-file tests through the source-generated path for all four document roots. Also set `JsonSerializerIsReflectionEnabledByDefault=false` during validation so accidental regression to reflection fails under ordinary CoreCLR tests rather than only after AOT publish.

Microsoft's source-generation guidance explicitly says basic reflection serialization can break NativeAOT, recommends generated `JsonTypeInfo`/`JsonSerializerContext`, and documents `JsonSerializerIsReflectionEnabledByDefault=false`: <https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation>.

### Blocker — registered `ViewLocator` is explicitly not NativeAOT compatible

**Evidence**

- `src/StickerPicker/ViewLocator.cs:11-13` is decorated with `[RequiresUnreferencedCode]`.
- `src/StickerPicker/ViewLocator.cs:23-28` derives a type name, calls `Type.GetType`, then `Activator.CreateInstance`.
- `src/StickerPicker/App.axaml:9-11` always registers it.
- Whole-program NativeAOT publish reports IL2026 at `src/StickerPicker/App.axaml(10)` from generated `App.!XamlIlPopulate` calling the annotated constructor.
- Repository search found no content-bound view-model navigation that needs it. `src/StickerPicker/App.axaml.cs:59-69` directly constructs `MainWindow` and assigns `MainViewModel`; the only view is `src/StickerPicker/Views/MainWindow.axaml`. The naming convention would also map `StickerPicker.ViewModels.MainViewModel` to `StickerPicker.Views.MainView`, but the actual type is `MainWindow`, so it could not resolve that pair anyway.

**Required implementation**

- Preferred minimal fix: remove the unused registration from `App.axaml` and delete the unused `ViewLocator`.
- If view-model navigation is introduced, use explicit pattern matching or typed XAML `DataTemplate` mappings. Do not suppress IL2026 or root the whole assembly unless genuinely dynamic creation is a product requirement.

Avalonia's current documentation states that the default reflection locator is not NativeAOT compatible and recommends pattern matching or XAML data templates: <https://docs.avaloniaui.net/docs/data-templates/view-locator>.

### High — NativeAOT publishing is not configured persistently

**Evidence**

- `src/StickerPicker/StickerPicker.csproj:2-9` targets `net10.0` but has no `PublishAot`, `IsAotCompatible`, `RuntimeIdentifier(s)`, or explicit NativeAOT release profile.
- `src/StickerPicker.Core/StickerPicker.Core.csproj:3-7` has no `IsAotCompatible`.
- Evaluated current properties are: `PublishAot=""`, `IsAotCompatible=""`, `PublishTrimmed=""`, `SelfContained=false`, and no RID. Consequently, normal Release build succeeds with zero diagnostics because AOT/trim analyzers are not active.
- Passing `PublishAot=true` enables `PublishTrimmed=true`, `EnableAotAnalyzer=true`, and `EnableTrimAnalyzer=true`, but the settings currently exist only on the command line.

**Required implementation**

- Put `PublishAot=true` on the executable project (or a dedicated release publish profile checked into source), rather than relying only on CLI switches. NativeAOT is inherently RID-specific and self-contained; make the release command/profile explicit about `-r <RID>` and self-contained intent.
- Put `IsAotCompatible=true` on both repository projects so ordinary builds continuously run trim, single-file, and AOT analyzers. With the existing warnings-as-errors policy this becomes an effective regression gate once the JSON and locator blockers are fixed.
- Avoid hard-coding one `RuntimeIdentifier` in shared build settings if multiple artifacts are intended; drive a RID matrix from CI or separate profiles.
- Do not add `InvariantGlobalization` merely for size: this Chinese-language desktop app should retain normal globalization unless behavior is specifically tested.
- `VerifyReferenceAotCompatibility=true` is optional, not a practical zero-warning gate today. The experiment emitted IL3058 for Avalonia, CommunityToolkit.Mvvm, SkiaSharp, HarfBuzzSharp, MicroCom, and Tmds.DBus because several dependencies do not carry .NET 10 AOT-compatibility metadata. Microsoft documents this check as opt-in and potentially noisy; successful whole-program publish analysis plus runtime tests remains necessary.

Microsoft recommends putting `PublishAot` in the project because it also controls build/editor analysis and documents `IsAotCompatible`: <https://learn.microsoft.com/dotnet/core/deploying/native-aot/>.

### Medium — Avalonia/compiled XAML is generally suitable, but package/settings details need cleanup

**Evidence**

- Avalonia packages are primarily 12.1.0 in `src/StickerPicker/StickerPicker.csproj:17-21`; Avalonia 12.1 evaluates `AvaloniaUseCompiledBindingsByDefault=true`.
- The bindable roots and item templates declare `x:DataType`: `src/StickerPicker/App.axaml:5`, `src/StickerPicker/Views/MainWindow.axaml:9,76,147`, and `src/StickerPicker/Controls/StickerTile.axaml:5`. No explicit `ReflectionBinding` was found.
- Assets are already `AvaloniaResource` in `src/StickerPicker/StickerPicker.csproj:13`, and app/theme resources use `avares` or resource paths.
- `Avalonia.Controls.ItemsRepeater` is 12.0.0 while the remaining Avalonia packages are 12.1.0 (`StickerPicker.csproj:18`). The Linux AOT link completed, so this is not a demonstrated blocker, but aligning the Avalonia package family removes a compatibility variable before release.
- `BuiltInComInteropSupport=true` is explicitly set at `StickerPicker.csproj:8`. Current Avalonia 12 documentation says the old `false` workaround was necessary only before Avalonia 12, while .NET NativeAOT itself lists built-in COM as unsupported on Windows. Treat this explicit `true` as a target-host validation item: remove it if unnecessary or prove that the Windows publish and accessibility paths work; do not assume a Linux link certifies Windows COM behavior.

Avalonia's NativeAOT guidance calls for compiled XAML/bindings, static resources, `PublishAot`, and `IsAotCompatible`: <https://docs.avaloniaui.net/docs/deployment/native-aot>.

### Medium — SkiaSharp/native interop publishes, but requires RID-specific payload validation

**Evidence**

- Direct SkiaSharp use is in `src/StickerPicker/Services/BoundedImageDecoder.cs:2,54`; the project references `SkiaSharp` 3.119.4 at `StickerPicker.csproj:27`.
- Dependency resolution includes RID-specific `SkiaSharp.NativeAssets.{Linux,Win32,macOS}` and HarfBuzz native-asset packages.
- The successful `linux-x64` AOT output contains a 23 MB ELF plus separate `libSkiaSharp.so` (about 11 MB), `libHarfBuzzSharp.so` (about 2.7 MB), and native debug symbols. `ldd` resolves system `libm` and `libc`. Thus “NativeAOT” does not mean every third-party native library is folded into one physical file.
- The native executable reached Avalonia startup and failed only because the research environment deliberately had no X display (`XOpenDisplay failed`). This proves executable startup/link loading on this host, not rendering correctness.

**Required validation**

- For each shipping RID, inspect that the correct Skia/HarfBuzz libraries are present, start the app on a GUI host, decode representative PNG/JPEG/GIF/WebP inputs, exercise thumbnail/hover rendering, and package every required native file.
- Linux binaries are baseline-sensitive: Microsoft states a NativeAOT binary produced on Linux generally runs on the same or newer distro version. Building on Ubuntu 26.04 is therefore unsuitable evidence for older supported distros; build in the oldest supported baseline/container.

### Medium — Windows platform interop needs native Windows runtime testing

**Evidence**

- `src/StickerPicker/Platform/Windows/WindowsHotkeyService.cs:180-186,276-298` uses Win32 P/Invoke and a delegate function pointer (`Marshal.GetFunctionPointerForDelegate`).
- `src/StickerPicker/Platform/Windows/WindowsClipboardImageService.cs:175-201` uses user32/kernel32 P/Invoke and manual unmanaged memory.
- `src/StickerPicker/Services/ServiceFactory.cs:7-24` roots Windows implementations only under `OperatingSystem.IsWindows()` and uses null implementations elsewhere. Linux success therefore does not execute or certify the hotkey/clipboard code.
- NativeAOT supports P/Invoke, but target native libraries/entry points must be available. Runtime behavior, callback lifetime, struct marshalling, tray behavior, and clipboard ownership remain target-host concerns.

**Required validation**

On `win-x64` (and separately `win-arm64` if shipped), test startup, tray icon/menu, repeated register/unregister of the global hotkey, hide/show behavior, CF_HDROP and bitmap clipboard paths in intended chat clients, image decoding, settings persistence, and clean shutdown. Keep crash symbols (`.pdb`) with release artifacts for dump analysis.

## Cross-RID build and validation constraints

NativeAOT uses OS-native linkers and SDK libraries. Microsoft explicitly states that cross-OS compilation is unsupported; cross-architecture compilation is only conditionally supported when the target toolchain and libraries are installed: <https://learn.microsoft.com/dotnet/core/deploying/native-aot/cross-compile>.

Observed here:

- `linux-x64` on Ubuntu x64: publish completed with five app warnings and emitted an ELF/native sidecars.
- `win-x64` on Ubuntu x64: compilation surfaced the four JSON analyzer warnings, then failed with `Cross-OS native compilation is not supported.` It did not reach Windows whole-program link/runtime certification.
- Headless Linux launch: native executable loaded and reached Avalonia initialization, then aborted with expected `XOpenDisplay failed`; no UI feature was validated.

Recommended minimum matrix:

| Artifact | Build host/toolchain | Static gate | Runtime gate |
|---|---|---|---|
| `win-x64` (primary, given `WinExe` and Windows integrations) | x64 Windows with .NET 10 SDK and VS 2022 Desktop C++ tools | Release AOT publish with zero actionable IL2026/IL3050 and warnings-as-errors | Startup/rendering, tray, hotkey, clipboard, persistence, image formats, shutdown |
| `win-arm64` (only if promised) | Windows with ARM64 C++ build tools; cross-architecture is possible but not equivalent to execution | Separate RID publish | Real ARM64 device/VM smoke; native Skia/HarfBuzz payload |
| `linux-x64` (only if promised) | Oldest supported glibc distro/container with clang and zlib development files | Separate RID publish | X11/desktop session smoke; note hotkey/clipboard currently degrade to null services |
| `osx-x64` / `osx-arm64` (only if promised) | macOS with Xcode command-line tools | Separate publish per architecture | Real matching Mac; bundle/native libraries; optionally combine architectures with `lipo` |

Do not claim a cross-RID artifact supported from restore/build success alone. A release gate should archive the exact publish log, reject actionable trim/AOT warnings, inspect artifact/native dependencies, and execute the binary on the target OS/architecture.

## Suggested implementation order

1. Introduce source-generated JSON metadata and source-gen round-trip tests; enable reflection-disabled JSON validation.
2. Remove the unused reflection `ViewLocator` registration/class (or replace it with explicit mappings).
3. Enable `IsAotCompatible` on both projects and make normal Release build clean under existing warnings-as-errors.
4. Add durable executable NativeAOT publish settings/profile and align Avalonia ItemsRepeater to the chosen 12.1 patch family.
5. Publish natively on each promised RID with no actionable IL2026/IL3050; do not enable blanket suppressions or whole-assembly trimmer roots for the two known issues.
6. Run the target-host GUI/platform smoke matrix and retain native symbols.

## Residual risks after the known warnings are fixed

- A clean analyzer/linker run cannot prove native GUI, Skia/HarfBuzz, tray, callback marshalling, hotkey, or clipboard behavior.
- Third-party dependency AOT metadata is incomplete (the optional .NET 10 IL3058 verification is noisy), so whole-program target-RID publishing remains mandatory.
- The Linux publish performed here used Ubuntu 26.04 and is not evidence of compatibility with older glibc distributions.
- No Windows or macOS native link/runtime test was possible from this Linux session.
- The current 12.0.0/12.1.0 Avalonia package mix and explicit built-in COM setting should be resolved or explicitly certified on Windows.
