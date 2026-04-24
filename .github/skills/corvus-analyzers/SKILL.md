---
name: corvus-analyzers
description: >
  Understand and work with the Roslyn analyzers shipped with Corvus.Text.Json.
  Covers 10 production diagnostics (CTJ001-CTJ010) for correct and performant V5 code,
  the CTJ-NAV refactoring for navigating from types to JSON Schema definitions, and
  the analyzer packaging convention. USE FOR: understanding what each analyzer checks,
  writing code that passes analyzer checks, packaging analyzer DLLs.
  DO NOT USE FOR: the 25 migration analyzers (use corvus-v4-migration for the workflow
  and CVJ001-CVJ025 reference).
---

# Corvus.Text.Json Analyzers

## Production Analyzers (CTJ001-CTJ010)

These analyzers ship with the main library to ensure correct and performant usage.

| ID | Summary | Severity |
|----|---------|----------|
| **CTJ001** | Prefer UTF-8 string literals (`"name"u8`) over string-based overloads | Warning |
| **CTJ002** | Remove unnecessary .NET type conversions | Warning |
| **CTJ003** | Use static lambdas in `Match<TOut>` for zero-allocation | Info |
| **CTJ004** | Missing `Dispose()` on `ParsedJsonDocument` | Warning |
| **CTJ005** | Missing `Dispose()` on `JsonWorkspace` | Warning |
| **CTJ006** | Missing `Dispose()` on `JsonDocumentBuilder` | Warning |
| **CTJ007** | Discarded `EvaluateSchema()` result | Warning |
| **CTJ008** | Prefer `NameEquals()` over `Name ==` for zero-allocation comparison | Info |
| **CTJ009** | Rent `Utf8JsonWriter` from workspace instead of creating new | Info |
| **CTJ010** | Prefer `ReadOnlyMemory`/`Span`-based `Parse` overloads | Info |

### Key Patterns

**CTJ001 — UTF-8 literals:**
```csharp
// ❌ Triggers CTJ001
element.GetProperty("name");

// ✅ Preferred
element.GetProperty("name"u8);
```

**CTJ004-006 — Dispose:**
```csharp
// ❌ Triggers CTJ004
var doc = ParsedJsonDocument<JsonElement>.Parse(json);

// ✅ Correct
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
```

**CTJ008 — NameEquals:**
```csharp
// ❌ Triggers CTJ008
if (property.Name == "foo") { ... }

// ✅ Preferred — zero-allocation UTF-8 comparison
if (property.NameEquals("foo"u8)) { ... }
```

## CTJ-NAV Refactoring

Navigate from a generated C# type to its source JSON Schema definition. This is a code action (refactoring), not a diagnostic — it appears in the lightbulb menu.

## Migration Analyzers (CVJ001-CVJ025)

These are in a separate package (`Corvus.Text.Json.Migration.Analyzers`) and detect V4→V5 migration patterns. See the `corvus-v4-migration` skill for the workflow, and `docs/MigrationAnalyzers.md` for the full reference.

## Analyzer Packaging Convention

Roslyn analyzer projects in this repo manually pack their DLL and dependency DLLs into `analyzers/dotnet/cs` instead of relying on normal build output:

```xml
<!-- In the .csproj -->
<Target Name="PackAnalyzer" AfterTargets="Build">
  <!-- Copies DLLs into analyzers/dotnet/cs for NuGet packaging -->
</Target>
```

This is the same pattern used by both the V5 source generator (`src/Corvus.Text.Json.SourceGenerator/`) and the V4 source generator (`src-v4/Corvus.Json.SourceGenerator/`).

## Suppressed Warnings

These warnings are suppressed project-wide — don't add `#pragma warning disable` for them:
- `JSON001`, `xUnit1031`, `xUnit2013`, `CS8500`, `IDE0065`, `IDE0290`

## Cross-References
- For migration workflow using CVJ analyzers, see `corvus-v4-migration`
- For dispose patterns (CTJ004-006), see `corvus-parsed-documents-and-memory` and `corvus-mutable-documents`
- Full reference: `docs/Analyzers.md`, `docs/MigrationAnalyzers.md`
