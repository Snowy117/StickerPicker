# Directory Structure (Core)

> How Core / domain code is organized in StickerPicker.

---

## Overview

`StickerPicker.Core` is a **UI-free** class library (`net10.0`, nullable enabled). It owns paths, config, and the sticker library. Avalonia lives only in `src/StickerPicker`.

---

## Directory Layout

```text
src/StickerPicker.Core/
├── Abstractions/          # Small public seams (IStickerLibrary, IAppPaths, …)
├── Models/                # DTOs / domain records (Sticker, Category, AppConfig)
├── Paths/                 # Default LocalAppData + bootstrap.json pointer
├── Config/                # config.json load/save
├── Library/               # Deep IStickerLibrary adapter + internal collaborators
│   ├── FolderStickerLibrary.cs   # Public coordinator
│   ├── LibraryScanner.cs
│   ├── LibraryImporter.cs
│   ├── LibraryIndexStore.cs
│   └── LibraryPathRules.cs
├── Json/                  # AtomicJson (temp + replace, corrupt backup)
└── Properties/            # InternalsVisibleTo for tests
```

---

## Module Organization

| Concern | Location | Notes |
|---------|----------|-------|
| External seam for stickers | `IStickerLibrary` | Scan, category CRUD, import, move, query |
| Implementation depth | `Library/*` collaborators | Keep each `.cs` **under 400 lines** |
| Settings | `IConfigStore` + `AppConfig` | Defaults merge on load |
| Data root | `IAppPaths` | `bootstrap.json` in default LocalAppData folder |

**Forbidden**: Avalonia or UI types in Core. ViewModels must not open raw library JSON files.

---

## Naming Conventions

- Interfaces: `I{Capability}` in `Abstractions/`
- Folder categories: physical dirs under `{DataRoot}/library/{CategoryName}/`
- Virtual category id for “全部”: `Category.AllId` / `__all__` (not a directory)

---

## Examples

- Deep library: `Library/FolderStickerLibrary.cs`
- Tests as second adapter surface: `tests/StickerPicker.Core.Tests/`
