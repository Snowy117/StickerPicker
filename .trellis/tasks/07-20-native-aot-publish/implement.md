# Implement — NativeAOT publishing

## 1. Source-generated persistence

1. Add the Core `JsonSerializerContext` with all persisted document roots and existing serializer options.
2. Move the bootstrap DTO to an internal top-level type accessible to the generator.
3. Change `AtomicJson` to require `JsonTypeInfo<T>` for load and save operations.
4. Update `ConfigStore`, `AppPaths`, and `LibraryIndexStore` call sites to pass generated metadata.
5. Add a test-only generated context and update atomic JSON tests.
6. Disable reflection serialization by default in Core and test project builds.

Validation gate:

```bash
dotnet build StickerPicker.slnx -c Release
dotnet test StickerPicker.slnx -c Release
```

## 2. Remove runtime view reflection

1. Remove `ViewLocator` registration from `App.axaml`.
2. Delete the unused reflection-based implementation.
3. Search for remaining `Type.GetType`, `Activator.CreateInstance`, and reflection serializer calls.

## 3. Durable AOT configuration

1. Enable `IsAotCompatible` on Core and executable projects.
2. Enable `PublishAot` and self-contained publication for Release executable builds without hard-coding a RID.
3. Configure built-in COM interop according to Avalonia NativeAOT guidance.
4. Keep normal globalization and existing dependencies.

## 4. Full validation

```bash
dotnet build StickerPicker.slnx -c Release
dotnet test StickerPicker.slnx -c Release
dotnet format --severity info --verify-no-changes | echo $?
dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r linux-x64 --self-contained true
file src/StickerPicker/bin/Release/net10.0/linux-x64/publish/StickerPicker
find src tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 | xargs -0 wc -l | awk '$1 >= 400 { print }'
```

Launch the native executable with a bounded timeout. Record whether it reaches Avalonia initialization; a headless display failure is expected on a host without X11.

Windows follow-up, on native Windows x64 with .NET 10 SDK and Visual Studio C++ Desktop tools:

```powershell
dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r win-x64 --self-contained true
```

Then smoke startup, rendering/image formats, tray/menu/shutdown, global hotkey, clipboard formats, settings persistence, and required native sidecars.

## Review and rollback gates

- Run a full `trellis-check` review against task artifacts and both backend/frontend quality specs.
- Fix all analyzer, LSP, test, formatting, and AOT publish findings before completion.
- If source generation changes persisted JSON, revert and correct metadata/options before proceeding.
- Do not include the pre-existing untracked crash dump in edits or commits.
