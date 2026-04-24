---
name: corvus-v4-migration
description: >
  Migrate code from Corvus.Json V4 to Corvus.Text.Json V5. Covers namespace and type
  renames, parsing pattern changes, the mutation model shift from functional With*()
  to imperative Set*() with workspace/builder, validation API changes, composition type
  changes, migration analyzer diagnostics (CVJ001-CVJ025), and the Copilot-assisted
  migration workflow. USE FOR: migrating V4 consumer code to V5, understanding API
  differences between V4 and V5, using migration analyzers and code fixes.
  DO NOT USE FOR: general V5 development (use other skills), modifying the V4
  engine itself.
---

# V4 to V5 Migration

## Overview

V5 is a ground-up rewrite. Key architectural changes:
- **Namespace**: `Corvus.Json` → `Corvus.Text.Json`
- **Types**: `JsonAny`/`JsonString`/`JsonNumber` removed → everything is `JsonElement`
- **Parsing**: `ParsedValue<T>.Instance` → `ParsedJsonDocument<T>.RootElement`
- **Mutation**: Functional `With*()` → imperative `Set*()` with `JsonWorkspace` + `JsonDocumentBuilder`
- **Validation**: `Validate(ValidationContext, ValidationLevel)` → `EvaluateSchema()` / `EvaluateSchema(collector)`
- **Type reduction**: V5 reduces ALL simple+format types to project-local globals (V4 only reduced some)

## Migration Workflow

1. Install the migration analyzer package:
   ```xml
   <PackageReference Include="Corvus.Text.Json.Migration.Analyzers" Version="*" />
   ```

2. Migrate file-by-file, building between each file

3. Follow the analyzer diagnostics (CVJ001-CVJ025) — each has a code fix

4. For AI-assisted migration, reference the transformation rules in
   `docs/copilot/CopilotMigrationInstructions.md` (optimized for AI consumption)

## Key Transformation Patterns

### Parsing
```csharp
// V4
using ParsedValue<MyType> parsed = ParsedValue<MyType>.Parse(json);
MyType value = parsed.Instance;

// V5
using ParsedJsonDocument<MyType> doc = ParsedJsonDocument<MyType>.Parse(json);
MyType value = doc.RootElement;
```

### Property Access
```csharp
// V4
JsonString name = person.Name;
string nameStr = (string)name;

// V5
JsonElement name = person.Name;
string nameStr = (string)name;
// Or: person.Name.GetString() for the string value
```

### Mutation
```csharp
// V4 (functional — returns new instance)
person = person.WithName("Alice");

// V5 (imperative — mutates in place)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = person.CreateBuilder(workspace);
builder.RootElement.Name = "Alice";
```

### Validation
```csharp
// V4
ValidationContext result = value.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);
bool isValid = result.IsValid;

// V5 — EvaluateSchema() returns bool directly
bool isValid = value.EvaluateSchema();

// V5 — with detailed results, pass a collector
var collector = new MyResultsCollector();
bool isValid = value.EvaluateSchema(collector);
```

## Migration Analyzers (CVJ001-CVJ025)

Key diagnostics:
- **CVJ001**: `Corvus.Json` namespace → `Corvus.Text.Json`
- **CVJ002**: Type renames (`JsonAny` → `JsonElement`, etc.)
- **CVJ003-005**: Parsing pattern changes
- **CVJ006-010**: Property access changes
- **CVJ011-015**: Validation API changes
- **CVJ016-020**: Mutation pattern changes
- **CVJ021-025**: Composition and enum changes

Full reference: `docs/MigrationAnalyzers.md`

## Detailed Transformation Rules

The file `docs/copilot/CopilotMigrationInstructions.md` contains comprehensive,
AI-optimized transformation rules organized by category:
- Namespace changes
- Type name mapping (all V4 types → V5 equivalents)
- Parsing patterns
- Property access patterns
- Object creation and mutation
- Array operations
- String operations
- Composition types (oneOf, anyOf, allOf)
- Enum types
- Validation patterns

Reference this file for the complete rule set — it covers every transformation scenario.

## Common Pitfalls

- **Don't migrate all at once**: Migrate file-by-file and build between each.
- **V5 mutation requires workspace**: You can't just rename `WithX` to `SetX` — the mutation model is fundamentally different.
- **V5 global types**: In V4, `JsonString` was a shared library type. In V5, each project generates its own local type. Don't try to reference V4-style shared types.

## Cross-References
- For the migration analyzer reference, see `docs/MigrationAnalyzers.md`
- For AI-optimized transformation rules, see `docs/copilot/CopilotMigrationInstructions.md`
- For the V5 mutation model, see `corvus-mutable-documents`
- For the V5 parsing model, see `corvus-parsed-documents-and-memory`
