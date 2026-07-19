# Error Handling (Core)

> Persistence and import failure contracts.

---

## Overview

Library and config IO must not leave empty/corrupt files as the only copy of truth.

---

## Patterns

### Atomic JSON write

1. Serialize to `{path}.tmp` (or unique temp beside target)
2. Flush
3. Replace/move over target

### Corrupt JSON on load

1. Move/copy bad file to `*.bak-{timestamp}` (or similar)
2. Recreate empty default document
3. Continue; do not crash app startup if recoverable

### Import batch

- Per-file failures increment `Failed` and continue
- Duplicates (hash hit) increment `Duplicates` and skip copy
- Return `ImportResult` with counts; UI shows short Chinese status

### Category delete

- Policy enforced in library: non-empty delete requires explicit `deleteFiles` confirmation from UI

---

## Validation matrix

| Case | Expected |
|------|----------|
| Good import | files under `library/{cat}/`, metadata+hashes updated |
| Duplicate bytes | skipped, hashes unchanged for new id |
| Corrupt metadata.json | backup + empty, Refresh still lists FS files |
| Invalid category name | throw/reject before creating path |

---

## Tests required

- AtomicJson round-trip + corrupt recovery
- Import duplicate / collision suffix
- Explorer move + Refresh integration
