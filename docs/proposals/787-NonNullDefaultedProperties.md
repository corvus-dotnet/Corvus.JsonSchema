# Plan: non-nullable defaulted properties under `OptionalAsNullable` (issue #787)

Branch: `feature/787-non-nullable-defaulted-properties`

## Problem (issue #787)

When `OptionalAsNullable` is active, **every** optional property is generated as a
.NET nullable `T?`. The reporter expects an optional property that declares a JSON
Schema `default` to instead be non-nullable `T` — like a required property — because a
missing value is filled by the default, so the getter can never hand back "nothing".

This is correct: for a defaulted property the getter already materialises the default
(`...DefaultInstance`) when the property is absent, so the `T?` advertises a `null` the
implementation never produces for the absent case. Flipping `T?` → `T` is a breaking
change to generated code, so it must be **opt-in**.

## Decisions taken during review

1. **Surface the knob by extending the existing `OptionalAsNullable` enum** (not a
   separate switch), with a new value **`NullOrUndefinedExceptNonNullDefaulted`**.
2. **Cover both generators**: V5 (`src/Corvus.Text.Json.*`) and V4
   (`src-v4/Corvus.Json.*`).
3. **A `default: null` does NOT flip to non-nullable.** Within the new mode, only a
   **non-null** default makes a property non-nullable; a `default: null` stays a normal
   nullable optional. This is baked into the value's name.
4. **Flip-back logic lives ONLY in the new mode**, never in plain `NullOrUndefined`.
   This is what keeps V4 non-breaking (see "Breaking-change analysis").
5. **A pre-existing V5 bug is fixed in the same branch, as its own commit**: under
   `NullOrUndefined`, V5 fails to map an explicit JSON `null` to C# `null` (it returns a
   `Null`-kind `T`), contradicting both `docs/SourceGenerator.md` and V4. This is fixed
   and documented as a standalone breaking change.

## The new enum value

`OptionalAsNullable` goes from two values to three:

| Value | Meaning |
|---|---|
| `None` (default) | optional → `T` (unchanged) |
| `NullOrUndefined` | optional → `T?` (unchanged) |
| **`NullOrUndefinedExceptNonNullDefaulted`** *(new)* | like `NullOrUndefined`, except a property with a **non-null** default becomes non-nullable `T` |

## Behaviour matrices

### `NullOrUndefinedExceptNonNullDefaulted` (new mode; identical in V4 and V5 after the V5 fix)

For an **optional** property:

| schema `default` | type | absent | `"foo": null` | `"foo": "x"` |
|---|---|---|---|---|
| none | `T?` | C# `null` | C# `null` | `"x"` |
| non-null (`"bar"`) | **`T`** | `DefaultInstance` → `"bar"` | `Null`-kind `T` | `"x"` |
| JSON `null` | `T?` | C# `null` (flip-back) | C# `null` | `"x"` |

### Plain `NullOrUndefined` (unchanged, except the V5 contract fix)

| schema `default` | type | absent | `"foo": null` | `"foo": "x"` |
|---|---|---|---|---|
| none | `T?` | C# `null` | C# `null` *(V5: fixed; V4: already so)* | `"x"` |
| non-null | `T?` | `DefaultInstance` | C# `null` *(V5: fixed; V4: already so)* | `"x"` |
| JSON `null` | `T?` | `DefaultInstance` (a non-null `Null`-kind value) — **deliberately left as-is** | C# `null` | `"x"` |

> **Documented quirk**: in *plain* `NullOrUndefined`, a missing optional with
> `default: null` returns a non-null `Null`-kind value rather than C# `null`. We
> deliberately do **not** "fix" this, to keep V4 non-breaking; callers wanting the
> cleaner behaviour should use `NullOrUndefinedExceptNonNullDefaulted`. State this
> explicitly in `docs/SourceGenerator.md`.

## Controlling logic (codegen)

```csharp
bool hasDefault    = DefaultValue().ValueKind != JsonValueKind.Undefined;   // V4: HasDefaultValue()
bool defaultIsNull = DefaultValue().ValueKind == JsonValueKind.Null;
bool nonNullable   = excludeNonNullDefaulted && hasDefault && !defaultIsNull; // the "make it T" trigger
bool isNullable    = OptionalAsNullable() && optional && !nonNullable;
```

- `nonNullable` → type `T`, **suppress** the JSON-null→C#-null collapse, absent returns `DefaultInstance`.
- otherwise (`isNullable`) → type `T?`, **apply** the collapse.
- Absent-path return, **only inside the new mode**: `nonNullable ? DefaultInstance : default`
  (so a `null`-default optional returns C# `null` when absent — the flip-back).
- Plain `None` / `NullOrUndefined` keep the existing
  `hasDefault ? DefaultInstance : default` absent path untouched (a test locks in
  "missing defaulted → the default, not null" for `NullOrUndefined`).

Internal threading: keep the existing `bool OptionalAsNullable` (meaning "any nullable
mode on") and add a correlated `bool ExcludeNonNullDefaulted`, so the many call-sites
that only ask "is nullable mode on at all" don't change.

## Breaking-change analysis

### V4 — **zero breaking changes** (purely additive)

| V4 path | status |
|---|---|
| `None` mode | untouched |
| `NullOrUndefined` — no default | untouched |
| `NullOrUndefined` — non-null default | untouched |
| `NullOrUndefined` — `default: null` | untouched (flip-back scoped to new mode) |
| Thread-1 null-collapse | V4 already does it → no-op |
| `NullOrUndefinedExceptNonNullDefaulted` (new) | opt-in, additive |

### V5 — exactly **one** breaking change (independent of #787)

The V5 contract bug fix: under `NullOrUndefined`, an explicit JSON `null` now maps to
C# `null` (was a `Null`-kind `T`). Affects every optional property, even for users who
never adopt the new mode. Everything else in V5 (the new mode, the flip-back) is
opt-in/additive.

## Implementation steps

### Thread 1 — V5 null-collapse bug fix (own commit; breaking)

- `src/Corvus.Text.Json.CodeGeneration/CodeGeneratorExtensions.Object.cs:730-738`: add,
  gated on `isNullable`, a `value.ValueKind == JsonValueKind.Null → return default;`
  branch so JSON `null` maps to C# `null`.
- Tighten the now-stale doc remark at `:1604`/`:1595-1620`.
- Test: `"foo": null` under `NullOrUndefined` → C# `null`.
- VERSIONHISTORY: standalone breaking-change bullet.

### Thread 2 — new `NullOrUndefinedExceptNonNullDefaulted` mode (V5 then V4)

Plumbing (mirror the existing `OptionalAsNullable` wiring), V5:

| Layer | File | Change |
|---|---|---|
| CLI enum | `src/Corvus.Json.Cli.Core/OptionalAsNullable.cs` | add `NullOrUndefinedExceptNonNullDefaulted = 2` |
| Config model | `Corvus.Json.Cli.Core/Model/GeneratorConfig.OptionalAsNullable*.cs` | add enum string; regenerate (don't hand-edit) |
| CLI description | `Corvus.Json.Cli.Core/GenerateCommand.cs` | mention new value |
| Build prop (read) | `Corvus.Text.Json.SourceGenerator/IncrementalSourceGenerator.cs:~126` | map `"NullOrUndefinedExceptNonNullDefaulted"` → `optionalAsNullable=true, excludeNonNullDefaulted=true` |
| Options flow | same file `GlobalOptions` + `CSharpLanguageProvider.Options` (`CSharpLanguageProvider.cs:~863/898`) | new ctor param + property |
| Metadata | `Corvus.Text.Json.CodeGeneration/TypeDeclarationExtensions.cs` | new key + `ExcludeNonNullDefaulted()` accessor; set in `SetCSharpOptions` (~`:596`) |

Codegen (V5): apply the controlling logic at `Object.cs:720` (type + absent path) and
the XML-doc branch at `:1604`.

V4 mirror — plumbing: build prop `CorvusJsonSchemaOptionalAsNullable`
(`src-v4/Corvus.Json.SourceGenerator/IncrementalSourceGenerator.cs:211` + carriers at
`:419/:442/:469`), `CSharpLanguageProvider.cs:699`,
`TypeDeclarationExtensions.cs:23/38/161`.

V4 codegen — apply the `nonNullable` guard at **all** nullability/null-handling sites,
kept in lockstep:
- `Object.cs:200` — property type nullability
- `Object.cs:253` & `:291` — suppress the JSON-null→default conversion for non-null-defaulted props
- `Object.cs:809` — `TryAs…` dependent-schema `out` param nullability
- `Object.cs:1299/1304` — additional accessor nullability
- `Object.cs:1705` — XML doc remark

### Thread 3 — flip-back for `default: null` (part of the new mode only)

Implemented via `!defaultIsNull` in the `nonNullable` term plus the new-mode absent path
returning C# `null` for a `null` default. No edits to the plain `NullOrUndefined` path.

## Tests

- **V5**: extend `tests/Corvus.Text.Json.Tests/GeneratedNullableOptionalTests.cs` and the
  `…GeneratedModels.OptionalAsNullable` fixture. Cover all three new-mode rows
  (no default, non-null default, `null` default) + the existing-mode invariants +
  the thread-1 explicit-null fix.
- **V4**: mirror, with explicit attention to the `"foo": null` cases (Null-kind vs
  C# null) and the `default: null` flip-back.

## Docs / VERSIONHISTORY

- `docs/SourceGenerator.md`: document the third value for both
  `CorvusTextJsonOptionalAsNullable` and `CorvusJsonSchemaOptionalAsNullable`, including
  the deliberately-retained plain-`NullOrUndefined` `default: null` quirk.
- `VERSIONHISTORY.md`: two clearly-scoped entries —
  1. *(V5 only, breaking)* `NullOrUndefined` now maps explicit JSON `null` to C# `null`,
     matching the documented contract and V4.
  2. *(V4 + V5, additive)* new `NullOrUndefinedExceptNonNullDefaulted` value: optional
     properties with a non-null default generate as non-nullable `T`.

## Commit sequencing

1. V5 thread-1 bug fix + test + VERSIONHISTORY (standalone, breaking).
2. V5 plumbing for the new enum value (no behaviour change yet).
3. V5 new-mode codegen + flip-back + tests; regenerate V5 fixtures.
4. V4 plumbing.
5. V4 new-mode codegen (incl. null-conversion suppression) + tests; regenerate V4 fixtures.
6. Docs + VERSIONHISTORY additive entry.
