# Type Safety

> Nullable and typing conventions for StickerPicker.

---

## Overview

All projects use `<Nullable>enable</Nullable>` and modern C# (`net10.0`). Prefer expressive nullability over `!`.

---

## Rules

1. **Enable nullable** on every project; do not disable for convenience.
2. **Avoid null-forgiving `!`** except at true platform boundaries (e.g. file picker local path after null check).
3. Prefer `string?`, early return, and `ArgumentNullException.ThrowIfNull` over silent defaults.
4. Core models should use required/init properties where values are always present after load.
5. Cross-seam DTOs (`ImportResult`, `Sticker`, `Category`) stay immutable-friendly.

---

## Forbidden Patterns

- `!` to silence analyzer noise without proving non-null
- Catch-all `catch` that swallows without user/log signal for user-facing ops
- Putting Avalonia types into Core “for convenience”

---

## File size

Keep each `.cs` file under **400 lines**. Split internal collaborators rather than growing god classes.
