# Upstream dotnet/runtime Review

This document tracks periodic reviews of changes in `dotnet/runtime`'s `System.Text.Json` and related areas (`Common/`, `System.Private.CoreLib/`) that may need porting to Corvus.Text.Json.

Corvus forks or derives code from dotnet/runtime for these components:

| Corvus component | Upstream source |
|---|---|
| `Corvus.Text.Json.Reader` (Utf8JsonReader) | `src/libraries/System.Text.Json/src/System/Text/Json/Reader/` |
| `Corvus.Text.Json.Writer` (Utf8JsonWriter) | `src/libraries/System.Text.Json/src/System/Text/Json/Writer/` |
| `ParsedJsonDocument` / `JsonElement` | `src/libraries/System.Text.Json/src/System/Text/Json/Document/` |
| `JsonEncodedText` | `src/libraries/System.Text.Json/src/System/Text/Json/JsonEncodedText.cs` |
| `BitStack` | `src/libraries/System.Text.Json/src/System/Text/Json/BitStack.cs` |
| `ValueListBuilder<T>` | `src/libraries/Common/src/System/Collections/Generic/ValueListBuilder.cs` |
| `JsonHelpers` (date parsing, number helpers) | `src/libraries/System.Text.Json/src/System/Text/Json/JsonHelpers*.cs` |
| `JsonConstants` | `src/libraries/System.Text.Json/src/System/Text/Json/JsonConstants.cs` |
| `ThrowHelper` | `src/libraries/System.Text.Json/src/System/Text/Json/ThrowHelper.cs` |
| Polyfills (`CallerArgumentExpression`, `Index`, etc.) | `System.Private.CoreLib/src/` |

Code from the serializer, source generator, `JsonNode`, and `JsonConverter` infrastructure is **not** forked — changes in those areas are always irrelevant.

## Review Process

1. List commits touching `src/libraries/System.Text.Json` since the last review date
2. Filter out VMR codeflow and dependency-bump commits
3. For each real commit, check which files were changed
4. Classify as ADOPT (bug fix or improvement to port), CONSIDER (worth evaluating), or SKIP (irrelevant)
5. Record findings in the table below
6. Create issues or PRs for ADOPT items

## Review: 2026-05-11 (covering 2026-01-01 to 2026-05-11)

43 commits in the period. 24 were serializer/source-generator-only (SKIP). 19 were evaluated in detail.

### ADOPT — Port these changes

| SHA | Summary | Upstream files | Status |
|---|---|---|---|
| `aba46e33ea` | Fix string buffer size in Utf8JsonWriter.WriteStringValue | `JsonConstants.cs`, `Writer/Utf8JsonWriter.WriteValues.String.cs` | ✅ **Ported** — precomputes `maxRequiredBytes` from original length × expansion factor in `WriteStringByOptions`, `WriteStringMinimized`, and `WriteStringIndented` for `ReadOnlySpan<char>` overloads. |
| `2ec033b0ce` | Fix excessive capacity allocation in ValueListBuilder\<T\> | `Common/.../ValueListBuilder.cs` | ✅ **Ported** — `Grow` parameter renamed to `additionalCapacityBeyondPos`, computation changed from `_span.Length + additional` to `_pos + additional`. Callers simplified to pass `source.Length` directly. `Dispose` updated to use `this = default` pattern. |
| `cde2bcd237` | Support ISO 8601 24:00 (end-of-day) in DateTime parsing | `JsonHelpers.Date.cs`, `Utf8Parser.Date.Helpers.cs` | ✅ **Ported** — `TryCreateDateTime` now accepts hour 24 when minute/second/fraction are zero, advancing to next day's 00:00:00. Test data updated: `1997-07-16T24:00` moved from invalid to valid (expected: `1997-07-17T00:00`). |
| `7010dbd3f0` | Move polyfills to Common/src/Polyfills | `JsonHelpers.cs`, `JsonDocument.MetadataDb.cs`, `JsonElement.cs`, `Utf8JsonReader.TryGet.cs`, `JsonWriterHelper.cs` | ⏭️ **Deferred** — large refactor (50+ files) with low bug-fix value. Corvus still targets netstandard2.0/net481 where most `#if NET` blocks are still necessary. |
| `85b61ab1ce` | Include propertyName in KeyNotFoundException from JsonElement.GetProperty | `Document/JsonElement.cs` | ✅ **Ported** — 6 throw sites updated (3 in `JsonElement.cs`, 3 in `JsonElement.Mutable.cs`) to include property name via `Arg_KeyNotFoundWithKey` resource string. |

### CONSIDER — Evaluate but lower priority

| SHA | Summary | Upstream files | Notes |
|---|---|---|---|
| `93578ff5a2` | Add Utf8JsonWriter.Reset overloads accepting JsonWriterOptions | `Writer/Utf8JsonWriter.cs`, `Writer/Utf8JsonWriterCache.cs` | New API: `Reset(IBufferWriter<byte>, JsonWriterOptions)` and `Reset(Stream, JsonWriterOptions)`. Useful if Corvus's writer needs to change options on reset. |
| `033df020f7` | Use ThrowHelper in JsonElement.GetByte/GetSByte/GetInt16/GetUInt16 | `Document/JsonElement.cs` | Replaces inline `throw new FormatException()` with `ThrowHelper.ThrowFormatException` in 4 methods. Improves JIT inlining. Minor. |
| `c24c76a497` | Consolidate downlevel polyfills under Common/Polyfills | `System.Text.Json.csproj` | Removes STJ's local `StackExtensions.netstandard.cs` in favor of shared polyfill. Check if forked code depends on `StackExtensions`. |
| `55d55301e2` | Remove no-op SR.Format calls with single argument | `ThrowHelper.cs` | Minor cleanup: `SR.Format(SR.X)` → `SR.X` when no format args. |
| `db40985896` | Convert \[GeneratedRegex\] from method to property syntax | `JsonHelpers.cs` | C# syntax modernization. Apply if Corvus has same `[GeneratedRegex]` usage. |

### SKIP — Not relevant to Corvus fork

| SHA | Summary | Reason |
|---|---|---|
| `055b99b765` | Fix scoped Utf8JsonReader position | Serializer infrastructure only (`JsonSerializer.Read.Utf8JsonReader.cs`) |
| `7d36bed974` | Replace MemoryMarshal Read/Write with BitConverter | Touches `PropertyRef.cs` (serializer metadata) and polyfill not used by Corvus |
| `c24c76a497` | Consolidate polyfills (also listed in CONSIDER) | Corvus has its own polyfill approach |
| `12d6350043` | Polyfill span.Contains + string.Contains(char) | Corvus already has equivalent polyfills |
| `fd3eb4f084` | Fix SR.Format argument mismatches | Single trivial line in STJ's ThrowHelper |
| `313982dff3` | Remove NET9_0/NET10_0_OR_GREATER ifdefs | Corvus still needs these for netstandard2.0/net481 targeting |
| `7e4d8cea9f` | Fix JsonNode.GetPath() escaping | JsonNode not forked |
| `1ab6d1d02a` | Fix GetValue\<object\>() on JsonValueOfJsonPrimitive | JsonNode not forked |
| `4c78a1406c` | Optimize Version deserialization | Serializer converter |
| `03d7b4c02a` | Verbatim → raw string literals in tests | Cosmetic test-only |
| `da9be99152` | JSONL serialization support | Serializer feature |
| `7f710d35c4` | Enable trim/AOT analyzers on tests | Serializer test infrastructure |
| `39153964f8` | Fix JsonException message property type info | Serializer exception handling |
| `6fb2237c0f` | STJ source gen type parameter constraints | Source generator |
| `c51e9beeb4` | STJ Source gen UnsafeAccessor/reflection | Source generator |
| `3e90285515` | F# discriminated unions support | Serializer converter |
| `5794faa952` | JsonNamingPolicy.PascalCase | Serializer naming policy |
| `9096b69be2` | Byref parameters in constructors | Serializer deserialization |
| `86e3d03d39` | JsonNamingPolicyAttribute | Serializer attribute |
| `7c0f7cf2f7` | Generic converters on generic types | Serializer converter |
| `c88b2b6325` | Type-level JsonIgnoreCondition | Serializer attribute |
| `2c7ee1937a` | Fix source gen diagnostics for pragma | Source generator |
| `27cda88429` | Renames to IsMultithreadingSupported | Runtime internal |
| `848b99baf6` | Revert source generator changes | Source generator |
| `5709f35ed3` | Improve JsonSourceGenerator incrementality | Source generator |
| `741b715747` | Add GetTypeInfo\<T\>() | Serializer options |
| `7bfd8c4927` | Fix STJ source gen for partial class contexts | Source generator |
| `23b6046b8b` | Fix primitive converters JsonNumberHandling | Serializer converter |
| `3fa6e439dc` | Browser coreCLR interpreter | Runtime infrastructure |
| `b1d2e4aa8a` | Add IReadOnlySet support | Serializer converter |
| `34c3feeb72` | Fix Serialize for JsonExtensionData | Serializer |
| `b463e772f9` | Fix StackOverflow dictionary converter | Serializer converter |
| `4c765bf371` | JsonSerializerDefaults.Strict to source gen | Source generator |
| `8a201fa8f4` | Fix source gen verbatim identifier | Source generator |
