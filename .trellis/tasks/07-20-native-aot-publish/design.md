# Design — NativeAOT publishing

## Boundaries

- `StickerPicker.Core` owns JSON persistence and source-generated metadata. It remains UI-framework free.
- `StickerPicker` owns Avalonia startup and executable publishing configuration.
- Tests provide a separate source-generated context for test-only documents so the public atomic-file helper remains independently testable.

## Source-generated JSON contract

Introduce one internal `JsonSerializerContext` in Core with roots for `AppConfig`, `MetadataDocument`, `HashesDocument`, and a top-level internal `BootstrapDocument`. The generated graph includes their nested/member types.

`AtomicJson` retains its deep atomic-storage API but receives `JsonTypeInfo<T>` explicitly:

```csharp
LoadOrCreate<T>(string path, Func<T> factory, JsonTypeInfo<T> typeInfo, Action<string, Exception>? onCorrupt = null)
Save<T>(string path, T value, JsonTypeInfo<T> typeInfo)
```

This keeps file replacement and recovery mechanics generic while forcing every serialization call site to select statically generated metadata. The serializer options live on the generated context and preserve the current disk format and permissive read behavior.

The bootstrap DTO moves from a private nested class to an internal top-level type so the central source generator can reference it. This is an implementation visibility change only; the JSON schema remains `{ "dataRoot": ... }`.

Tests define a test-only `JsonSerializerContext` for `SampleDoc` and pass its `JsonTypeInfo` into `AtomicJson`. The Core and test projects set `JsonSerializerIsReflectionEnabledByDefault=false`, turning accidental reflection fallback into an ordinary-build/runtime failure.

## Avalonia startup

Delete `ViewLocator.cs` and its `App.axaml` registration. The repository directly creates `MainWindow` and has no view-model navigation consumer; therefore an explicit replacement is unnecessary. Future navigation must use typed XAML data templates or a static switch/factory.

## Build and publish configuration

- Add `IsAotCompatible=true` to both production projects so trim, single-file, and AOT analyzers participate in normal builds.
- Add `PublishAot=true` to the executable for Release configuration. Debug/design-time builds remain ordinary CoreCLR builds to preserve development tooling.
- Set `SelfContained=true` for Release publishing. Do not hard-code `RuntimeIdentifier`; callers/CI pass `-r` per artifact.
- Set Avalonia's documented `BuiltInComInteropSupport=false` for NativeAOT-compatible behavior.
- Do not enable invariant globalization because the application exposes Chinese text and may process culture-sensitive paths/settings.
- Do not enable `VerifyReferenceAotCompatibility`; current third-party packages do not all declare .NET 10 AOT metadata, making that optional check noisy even when whole-program publish succeeds.

## Compatibility and validation

NativeAOT requires an OS-native linker. Linux validates `linux-x64` build/link/startup; native Windows must validate `win-x64`. Each artifact must include its RID-native SkiaSharp/HarfBuzz libraries.

The Linux runtime smoke launches the published executable under a bounded timeout. On a headless host, reaching Avalonia and failing at `XOpenDisplay` proves native loading/startup only. It does not certify rendering.

## Risks and rollback

- Generated metadata could alter the JSON contract if options differ; existing and expanded tests inspect serialized property names and recovery behavior.
- Setting `BuiltInComInteropSupport=false` requires Windows tray/accessibility smoke testing; rollback is the property change only, but a rollback must still satisfy NativeAOT constraints rather than suppress errors.
- Third-party native libraries remain external sidecars. Packaging validation must not assume NativeAOT means one physical file.
- If a dependency emits actionable warnings after app fixes, address or version-pin it explicitly; do not root entire assemblies.
