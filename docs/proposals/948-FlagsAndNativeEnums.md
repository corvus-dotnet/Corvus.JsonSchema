# Plan: native C# enums for string enums and boolean-object flags (issue #948)

Branch: `feature/948-flags-enums`

## Problem (issue #948)

There is no ergonomic way to work with flag-like schemas from C#. The reporter models
flags either as an array of string-enum values (construction and `HasFlag`-style
assessment are tedious) or as an object of boolean properties (better, but creation is
multi-line and assessment does not read like a flags enum). The maintainer direction on
the issue: detect these shapes and emit real C# enums with conversions, rather than
grafting a flags API onto the JSON struct types.

## Decisions taken during review

1. **Two features, both additive.**
   - **A. Pure string enums** get a real nested C# `enum` plus conversions.
   - **B. Objects whose declared properties are all boolean** are detected as a flags
     shape and get a real nested `[Flags]` enum plus conversions.
   The reporter's array-of-enums `HasFlag`/`|` API is out of scope. Feature A's
   `Source` conversion already improves array construction
   (`builder.AddItem(Color.KnownValues.Red)`); an array flags API can follow if
   demanded.
2. **On by default**, with a new `nativeEnums` option as the off switch
   (`None` | `StringEnums` | `FlagsObjects` | `All`, default `All`).
3. **Nested types named `KnownValues` (A) and `Flags` (B).** Names are allocated
   through the member-scope machinery so a colliding user member falls back to a
   suffixed name. Whether the names also join the global reserved-name list in
   `Formatting.cs` is settled at implementation time by grepping tracked generated
   output: reserve only if no existing member would be renamed, because a global
   reservation renames colliding members on **every** generated type, not just the
   ones gaining the feature.
4. **Struct-to-enum conversion is implicit and throws on out-of-enum data**, paired
   with a non-throwing `TryGet` form. Out-of-enum values parse successfully today and
   only fail at `EvaluateSchema()`, so the safe form must exist.
5. **V5 only** (`src/Corvus.Text.Json.CodeGeneration/`). V4 is legacy and is not
   touched. No validation changes anywhere, so the standalone evaluator and
   conformance results are unaffected. The TypeScript provider is also unaffected;
   its design pins enums as literal unions, and all naming work here stays in the
   C# layer (`CSharpMemberName`), not the shared `MemberName` machinery.
6. **Ordinal and bit stability is documented, not enforced.** String-enum ordinals
   follow schema declaration order; inserting a value mid-array renumbers them. Flag
   bits follow the alphabetical order of the JSON property names (the model does not
   preserve property declaration order), so reordering properties is stable but adding
   or renaming one can reassign bits. The wire format (strings, property names) is
   unaffected. Anyone persisting the raw integer values is warned in the docs.

## Feature A: pure string enum

### Detection

`AnyOfConstantValues()` yields two or more constants and **every** constant across all
contributing keywords is `JsonValueKind.String`. This matches the existing `Match`
emission gate (`constantValues.Length > 1`), and `const` single values are excluded
naturally. Iteration order and member naming mirror `AppendEnumValuesClass` exactly, so
the enum members correspond one-for-one with the existing `EnumValues` properties,
including the case-insensitive collapse (`"Microsoft"`/`"microsoft"` produce one
member; the first occurrence provides the canonical JSON string).

### Emitted surface

For `{ "type": "string", "enum": ["red", "green", "blue"] }` generating `Color`, the
following is added to today's output:

```csharp
public enum KnownValues
{
    /// <summary>Corresponds to the JSON string "red".</summary>
    Red = 0,
    Green = 1,
    Blue = 2,
}

public static implicit operator Color(KnownValues value);       // switch onto EnumValues statics, allocation free
public static implicit operator KnownValues(Color value);       // bytes-native ValueEquals; throws on out-of-enum
public bool TryGetKnownValue(out KnownValues value);            // non-throwing form

// inside the Source ref struct:
public static implicit operator Source(KnownValues value);      // maps to the prebaked JSON constants
```

The `.Mutable` variant mirrors the struct-to-enum operator and `TryGetKnownValue`.

- Enum declaration and struct conversions: core partial, sibling of
  `AppendEnumValuesClass` (`CodeGeneratorExtensions.Validation.cs:563`), reusing the
  `BeginEnum` primitive (`CodeGeneratorExtensions.cs:619`).
- `Source` operator: `AppendSourceConversionOperators`
  (`CodeGeneratorExtensions.Builder.cs:4545`).
- Mutable mirror: `MutableCorePartial`.

A later cleanup candidate: `GenerationDriverV5.cs:236` can consume
`GeneratorConfig.OptionalAsNullable.KnownValues.NullOrUndefined` instead of comparing
against the `EnumValues` constants.

## Feature B: object of booleans as flags

### Detection

A new memoized predicate in `TypeDeclarationExtensions` (modeled on the discriminator
metadata precedent) requiring **all** of:

- object core type only;
- two to thirty-one declared properties (int backing; a `ulong` extension for 32 to 64
  is a possible follow-up);
- every declared property's reduced type is boolean core only (allOf-merged boolean
  properties qualify; `required` entries are permitted);
- no `patternProperties`;
- `additionalProperties` absent or `false`.

Deliberately conservative; loosening later is non-breaking. Disjoint from
`IsMapObject` (which requires no declared properties), and only adds members, so no
existing object emission is reordered.

### Emitted surface

For the issue's shape (`option1`, `option2` boolean properties) generating
`FlagsEnumEntity`:

```csharp
[Flags]
public enum Flags
{
    None = 0,
    Option1 = 1 << 0,
    Option2 = 1 << 1,
}

public static implicit operator Flags(FlagsEnumEntity value);   // per-property prebaked UTF-8 lookup
public bool TryGetFlags(out Flags value);                       // non-throwing form

// inside the Source ref struct:
public static implicit operator Source(Flags value);            // builds the object, static Build<TContext>, no closure
```

The `.Mutable` variant mirrors the struct-to-enum operator and `TryGetFlags`.

### Semantics

- Struct to `Flags`: a property whose value is `True` sets the bit; absent or `false`
  leaves it clear; extra properties are ignored; a present property with a
  non-boolean value counts as clear (schema validity is `EvaluateSchema()`'s job).
  A non-object value (including `Undefined` and `Null`) throws from the implicit
  operator; `TryGetFlags` returns `false`. The read goes through the value's
  `JsonElement` conversion, because the generated `TryGetProperty` surface is not
  emitted for `additionalProperties: false` objects.
- `Flags` to `Source`: delegates to the property-parameter `Build(...)` overload,
  writing every declared property explicitly `true`/`false`. Emit-all keeps the
  output deterministic and valid even when properties are `required`. The operator
  is therefore gated on that overload existing (`EmitsCreateParamsBuild`); a
  `buildParametersThreshold` low enough to suppress it also suppresses the operator.
- `None` is always emitted first as `0`. A schema property that mangles to `None`
  gets a suffixed member via the scope machinery.
- Bits are assigned in the alphabetical JSON-name order of `PropertyDeclarations`
  (declaration order is not preserved by the model); see decision 6.

### Usage after the change

```csharp
var flags = FlagsEnumEntity.Flags.Option1 | FlagsEnumEntity.Flags.Option2;
using var doc = Parent.Create(new Parent.Source(flagsEnum: flags, ...));

FlagsEnumEntity.Flags current = parent.FlagsEnum;
bool option1 = current.HasFlag(FlagsEnumEntity.Flags.Option1);
```

Root-level use still needs document ownership (`Create` + `RootElement`); that is
inherent to the memory model. The issue's disposal complaint concerns nesting sites,
which the `Source` conversion addresses.

## The `nativeEnums` option

| Value | Meaning |
|---|---|
| `None` | neither feature emits |
| `StringEnums` | feature A only |
| `FlagsObjects` | feature B only |
| `All` (default) | both |

Internal threading is two booleans (`emitNativeStringEnums`,
`emitNativeFlagsEnums`), following the `OptionalAsNullable` precedent. Plumbing:

| Layer | File | Change |
|---|---|---|
| Config schema | `src/Corvus.Json.Cli.Core/generator-config.json` | new `nativeEnums` string-enum property (config model regenerates at build) |
| CLI mapping | `src/Corvus.Json.Cli.Core/GenerationDriverV5.cs` (`MapGeneratorConfigToOptions`) | map value to the two booleans |
| CLI option | `src/Corvus.Json.Cli.Core/GenerateCommand.cs` | new `--nativeEnums` option |
| Options | `src/Corvus.Text.Json.CodeGeneration/CSharpLanguageProvider.cs:864` | two ctor params + properties |
| Metadata | `src/Corvus.Text.Json.CodeGeneration/TypeDeclarationExtensions.cs` | two keys + accessors; set in `SetCSharpOptions` |
| Build prop | `src/Corvus.Text.Json.SourceGenerator/IncrementalSourceGenerator.cs`, `IGlobalOptions.cs`, `.props` | `CorvusTextJsonNativeEnums` + `CompilerVisibleProperty` |

## Breaking-change analysis

Purely additive API on generated types; no validation or existing-member changes.
Because the default is `All`, every tracked `Generated/` directory containing
qualifying types churns on regeneration (new members only). The one potential rename
risk is the reserved-name question in decision 3, resolved by grep before choosing the
mechanism. Patch-level release.

## Tests

- **Feature A**: member naming (dashes, leading digits, reserved words,
  case-collapse consistent with `CaseInsensitiveEnumTests`), ordinal order, conversion
  round-trips through all four operators, out-of-enum throw plus `TryGetKnownValue`,
  `Source` conversion used inside a builder and `AddItem`.
- **Feature B**: detection positives, and one negative apiece for: a non-boolean
  property, `patternProperties`, an `additionalProperties` schema, more than 31
  properties, a single property, a map object. Round-trips, absent-versus-false,
  extra properties ignored, non-object throw plus `TryGetFlags`, `None`, combined
  flags, a property literally named `none`, a `required` boolean property.
- Negative detection asserts the generated type lacks the nested enum (reflection).
- New schemas live beside the existing generated-model test fixtures; test projects
  with `EnableDefaultCompileItems=false` need explicit `<Compile>` entries.

## Docs

- New example recipe (flags enums) plus updates to
  `docs/ExampleRecipes/014-StringEnumerations/README.md`,
  `docs/CodeGenerationPatternDiscovery.md`, `docs/ConsumingGeneratedTypes.md`, and the
  `corvus-codegen` skill (new option), each followed by the code-sample catalog gate.
- The ordinal/bit stability caveat (decision 6) is stated wherever the enums are
  documented.
- `VERSIONHISTORY.md` is updated at PR time per the repo gate, as a single additive
  entry.

## Commit sequencing

1. This proposal document.
2. `nativeEnums` option plumbing end to end (inert until emission lands).
3. Feature A: emission, conversions, tests.
4. Feature B: detection, emission, conversions, tests.
5. Docs, recipes (`regenerate-examples.ps1 -Force` after a Release CLI build), catalog.
6. Regenerate tracked `Generated/` directories; verify the AsyncAPI playground.
   Benchmark `C/` regeneration runs separately (never `B/`).
