# Quality Guidelines (Core)

> Standards for StickerPicker.Core.

---

## Overview

Core must stay deep, testable, and free of UI frameworks.

---

## Required

- **Deep modules**: small public interfaces (`IStickerLibrary`, `IConfigStore`, `IAppPaths`); hide scan/hash/JSON details.
- **Atomic JSON**: write temp + replace; on corrupt load, backup + recreate empty structure.
- **Folder categories**: filesystem is source of truth under `library/`; Refresh rescans.
- **Dedupe**: SHA-256 of file bytes; skip duplicates on import.
- **Tests**: unit + temp-dir integration tests in `StickerPicker.Core.Tests` (xunit).
- **File size**: every `.cs` file **&lt; 400 lines**.

---

## Forbidden

- Avalonia / UI references in Core
- ViewModels writing `metadata.json` directly
- Large multi-service surface for UI when one deep library can own the workflow

---

## Validation

```bash
dotnet build StickerPicker.slnx
dotnet test StickerPicker.slnx
find src tests -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' | xargs wc -l | awk '$1>=400 {print}'
```
