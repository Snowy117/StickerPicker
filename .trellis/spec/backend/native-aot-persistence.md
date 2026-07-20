# NativeAOT Persistence and Publishing

## 1. Scope / Trigger

This contract applies whenever code changes a persisted JSON document, the
`AtomicJson` API, application startup/type creation, or NativeAOT project
properties. It prevents reflection-only code from passing ordinary tests and
then failing with `IL2026`/`IL3050` during native publication.

## 2. Signatures

Every atomic persistence call supplies generated metadata explicitly:

```csharp
T AtomicJson.LoadOrCreate<T>(
    string path,
    Func<T> factory,
    JsonTypeInfo<T> typeInfo,
    Action<string, Exception>? onCorrupt = null)
    where T : class;

void AtomicJson.Save<T>(string path, T value, JsonTypeInfo<T> typeInfo);
```

Production metadata roots belong in `CoreJsonContext`. New persisted root
types must be accessible to that context and annotated with
`[JsonSerializable(typeof(TheDocument))]`.

## 3. Contracts

- JSON uses camel-case names and case-insensitive reads.
- Writes are indented and omit null properties.
- Reads accept comments and trailing commas.
- Writes use a sibling `.tmp` file and atomically replace/move it.
- Corrupt JSON is copied to `<file>.corrupt-<UTC timestamp>`, then recreated
  from the supplied factory.
- `JsonSerializerIsReflectionEnabledByDefault` remains `false` in Core and
  Core tests.
- Both production projects retain `IsAotCompatible=true`.
- Release executable publication retains `PublishAot=true` and
  `SelfContained=true`; callers pass `-r <RID>` rather than hard-coding one.
- Keep globalization enabled. Native SkiaSharp/HarfBuzz files are release
  sidecars, not optional build debris.
- Build each OS artifact on that OS; NativeAOT cannot cross-compile across
  operating systems.

## 4. Validation & Error Matrix

| Condition | Required outcome |
|---|---|
| Missing JSON file | Create parent directory/file from factory metadata |
| Valid compatible JSON | Deserialize with generated `JsonTypeInfo<T>` |
| JSON with comments/trailing comma/mixed case | Read successfully |
| Corrupt JSON | Notify callback, back up original, recreate document |
| Reflection serializer overload added | Build/test/AOT analysis must fail; replace with generated metadata |
| Reflection view/type discovery added | Use typed XAML template or explicit static factory |
| `linux-x64` release requested | Publish on Linux and inspect native sidecars |
| `win-x64` release requested | Publish and smoke-test on native Windows x64 |

## 5. Good / Base / Bad Cases

- **Good:** add a document to `CoreJsonContext`, pass
  `CoreJsonContext.Default.TheDocument`, and test round-trip plus corruption.
- **Base:** existing generated roots keep their on-disk schema while internal
  implementation types can move.
- **Bad:** call `JsonSerializer.Serialize(value, options)`, suppress IL warnings,
  or root an entire assembly to make NativeAOT publication appear clean.

## 6. Tests Required

For every production persisted root:

1. Round-trip through `AtomicJson` using its generated `JsonTypeInfo<T>`.
2. Assert representative nested data and camel-case property names.
3. Feed invalid JSON and assert one backup preserves the corrupt bytes.
4. Assert the replacement document is valid and factory-derived.
5. Keep a test asserting `JsonSerializer.IsReflectionEnabledByDefault` is
   `false`.

Repository gates:

```bash
dotnet build StickerPicker.slnx -c Release
dotnet test StickerPicker.slnx -c Release
dotnet format --severity info --verify-no-changes
dotnet publish src/StickerPicker/StickerPicker.csproj -c Release -r <host-rid> --self-contained true
```

## 7. Wrong vs Correct

### Wrong

```csharp
var json = JsonSerializer.Serialize(value, serializerOptions);
return JsonSerializer.Deserialize<T>(json, serializerOptions);
```

These overloads may require runtime reflection/code generation and are not
NativeAOT-safe.

### Correct

```csharp
AtomicJson.Save(path, config, CoreJsonContext.Default.AppConfig);
var loaded = AtomicJson.LoadOrCreate(
    path,
    static () => new AppConfig(),
    CoreJsonContext.Default.AppConfig);
```

Generated metadata makes the serialization graph analyzable and keeps the
atomic file/recovery behavior centralized.
