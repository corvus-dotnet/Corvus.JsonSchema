# Copilot Migration

This guide shows you how to use GitHub Copilot (in VS Code, Visual Studio, or the CLI) to assist with migrating code from Corvus.Json (V4) to Corvus.Text.Json (V5).

Copilot can handle the bulk of mechanical transformations — namespace changes, API renames, pattern rewrites — but you'll need to guide it and verify the results. Think of Copilot as a knowledgeable assistant that needs a clear brief and a human reviewer.

## Prerequisites

- GitHub Copilot enabled in your editor (VS Code, Visual Studio, or JetBrains)
- This repository cloned locally (the `.github/copilot-instructions.md` file is picked up automatically)
- V5 types already generated from your JSON Schema files (run the code generator first)

---

## Step 1: Generate Your V5 Types First

Before migrating any code, generate the V5 types so Copilot can see the actual type names:

```bash
# Using the CLI tool
generatejsonschematypes --rootNamespace MyApp.Models --outputPath generated/ schema.json

# Or using the source generator — add the attribute and build
# [JsonSchemaTypeGenerator("schema.json")]
dotnet build
```

This matters because V5 may rename types. V4 reduced unformatted simple types to framework globals from `Corvus.Json.ExtendedTypes` (`Corvus.Json.JsonString`, `Corvus.Json.JsonBoolean`, etc.) and `"type": "number"` + format to globals (`Corvus.Json.JsonInt32`, etc.). However, V4 did **not** reduce `"type": "integer"` + format — those became custom entity types (e.g. `OneOf1Entity`). V5 generates project-local global types for **all** of these cases (`JsonString`, `JsonInt32`, `JsonBoolean`, etc.). So unformatted types keep the same name (different provenance), while `"type":"integer"` + format variants get renamed from entity types to globals. Namespace references will need updating. Copilot can only suggest the right names if it can see the generated output.

---

## Step 2: Understand the Reference Documents

This repository includes three migration documents:

| Document | Purpose | Audience |
|---|---|---|
| [`MigratingFromV4ToV5.md`](MigratingFromV4ToV5.md) | Comprehensive migration guide with explanations and examples | Developers |
| [`MigrationAnalyzers.md`](MigrationAnalyzers.md) | Reference for all Roslyn diagnostics with before/after code examples | Developers |
| [`CopilotMigrationInstructions.md`](copilot/CopilotMigrationInstructions.md) | Structured transformation rules optimised for AI consumption | Copilot |

> **Tip:** Install the `Corvus.Text.Json.Migration.Analyzers` NuGet package *before* starting your Copilot-assisted migration. The Tier 2 guidance diagnostics serve as a checklist of patterns that Copilot can help transform — just point Copilot at the warnings.

You don't need to memorise any of these. `CopilotMigrationInstructions.md` is designed to be attached to Copilot prompts so it has the full context of every V4 → V5 transformation.

---

## Step 3: Attach Context When Prompting

The single most important thing you can do is **give Copilot the migration instructions as context**. Without them, it will guess — and frequently guess wrong.

### In VS Code / Visual Studio Chat

Use `#file` to attach the migration instructions:

```
#file:docs/copilot/CopilotMigrationInstructions.md

Migrate this file from Corvus.Json V4 to Corvus.Text.Json V5.
Follow the transformation rules in the attached document.
```

### In Copilot CLI (ghcs)

Reference the file in your prompt:

```
Migrate the file src/MyService/PersonHandler.cs from Corvus.Json V4 to
Corvus.Text.Json V5. Use the transformation rules in
docs/copilot/CopilotMigrationInstructions.md as your reference.
```

### In GitHub Copilot Workspace

If using Copilot Workspace, include a link or paste the relevant sections of `CopilotMigrationInstructions.md` into the task description.

---

## Step 4: Migrate File by File

Migration works best one file at a time. This keeps the scope manageable and makes review easier.

### Recommended workflow

1. **Pick a file** — start with simpler files (models, DTOs) before tackling files with complex mutation or validation logic.

2. **Ask Copilot to migrate it:**

   ```
   #file:docs/copilot/CopilotMigrationInstructions.md
   #file:src/MyService/PersonHandler.cs

   Migrate PersonHandler.cs from Corvus.Json V4 to Corvus.Text.Json V5.
   Follow the transformation rules in CopilotMigrationInstructions.md.
   ```

3. **Review the diff** — Copilot handles mechanical changes well, but always check:
   - Are `using` statements correctly changed to `using var` with disposal for `ParsedJsonDocument`?
   - Did mutation code get the `JsonWorkspace` + `CreateBuilder` pattern right?
   - Are the generated type names correct (check against your generated V5 output)?

4. **Build and test** — verify the file compiles and tests pass before moving on.

5. **Commit** — small, file-by-file commits make it easy to bisect if something goes wrong.

### Example prompts for common scenarios

**Simple model usage (parsing, property access):**
```
#file:docs/copilot/CopilotMigrationInstructions.md
#file:src/MyService/PersonHandler.cs

Migrate this file from V4 to V5. It mostly does parsing and property
access — focus on the ParsedValue → ParsedJsonDocument change and
ensure disposal is correct.
```

**Mutation-heavy code (With* → Set*):**
```
#file:docs/copilot/CopilotMigrationInstructions.md
#file:src/MyService/OrderBuilder.cs

Migrate this file. It uses With*() chains extensively to build up
objects. Convert to the JsonWorkspace + CreateBuilder + Set*() pattern.
Ensure the workspace is created in a using block.
```

**Validation code:**
```
#file:docs/copilot/CopilotMigrationInstructions.md
#file:src/MyService/SchemaValidator.cs

Migrate this file. It uses Validate(ValidationContext, ValidationLevel).
Convert to EvaluateSchema() for simple boolean checks, or
EvaluateSchema(collector) where detailed results are needed.
```

**Union/composition code (Match, TryGetAs):**
```
#file:docs/copilot/CopilotMigrationInstructions.md
#file:src/MyService/ShapeProcessor.cs

Migrate this file. It uses AsString, AsNumber, Match(), and TryGetAs*().
Note: V5 emits value accessors directly on multi-core-type types, so
AsString/AsNumber/AsBoolean are no longer needed. Check the generated V5
types to confirm the correct Match() and TryGetAs*() type names — simple
variants often become global types like JsonString, JsonInt32, etc.
```

---

## Step 5: Handle the Tricky Parts

Some transformations need more guidance. Here's how to handle them.

### Mutation (With* → JsonWorkspace)

This is the biggest conceptual change. V4's functional `With*()` chains become imperative `Set*()` calls on a mutable builder. Tell Copilot explicitly:

```
The V4 code uses functional With*() chains:
  var updated = person.WithName("Alice").WithAge(30);

Convert this to V5's imperative pattern:
  using var workspace = JsonWorkspace.Create();
  using var builder = person.CreateBuilder(workspace);
  var mutable = builder.RootElement;
  mutable.SetName("Alice");
  mutable.SetAge(30);
  // Use mutable.ToString() or mutable.Freeze() to get an immutable result
```

### Generated Type Names

V5's code generator uses different naming heuristics than V4. Both versions use "Value" and "Entity" suffixes for disambiguation, but V5 has different name reservations. This means some properties and types change names.

**Always cross-reference against the actual generated output.** If Copilot suggests a type name that doesn't exist, check what the generator actually produced.

Common changes:
- `CountValue` → `Count` (V5 doesn't reserve "Count")
- `Corvus.Json.JsonString` → project-local `JsonString` (same name, different provenance — unformatted types keep their names)
- V4 `OneOf1Entity` (for `"type":"integer","format":"int32"`) → project-local `JsonInt32` (V5 reduces integer + format to globals; V4 did not)

### AsString / AsNumber / AsBoolean

These existed in V4 because multi-core-type types (e.g. a union of string and integer) didn't emit value accessors directly. V5 emits all accessors on the type, so they're no longer needed.

Tell Copilot:
```
Replace v4.AsString / v4.AsNumber / v4.AsBoolean with direct access:
- (string)v5, v5.GetString(), v5.TryGetValue(out string?)
- (int)v5, v5.GetInt32(), v5.TryGetValue(out int)
- (bool)v5, v5.TryGetValue(out bool)

For object/array access, V5 also emits accessors directly:
- v5.EnumerateObject(), v5.TryGetProperty(), v5.GetPropertyCount()
- v5.EnumerateArray(), v5[index], v5.GetArrayLength()
```

### Span-Based String Access

V4's `TryGetValue<TState>(state, callback)` delegate pattern for accessing string content as `ReadOnlySpan<byte>` or `ReadOnlySpan<char>` doesn't exist in V5. Tell Copilot:

```
V4's TryGetValue(state, callback) span pattern becomes:
- v5.GetUtf8String().Span  — for ReadOnlySpan<byte>
- v5.GetUtf16String().Span — for ReadOnlySpan<char>
```

---

## Step 6: Batch Repetitive Changes

For large codebases, use Copilot to handle repetitive patterns across multiple files.

### Namespace-only changes

If many files just need namespace updates:
```
Find all files that import Corvus.Json and update the using directives
to Corvus.Text.Json. Also update any Corvus.Json.JsonAny references
to Corvus.Text.Json.JsonElement.
```

### ParsedValue → ParsedJsonDocument

If many files use the parse pattern:
```
#file:docs/copilot/CopilotMigrationInstructions.md

Find all usages of ParsedValue<T> and convert to
ParsedJsonDocument<T>. Remember:
- .Instance → .RootElement
- ParsedJsonDocument must be disposed (add using var)
- T.Parse(json) → ParsedJsonDocument<T>.Parse(json)
```

---

## Step 7: Verify

After migrating a group of files:

1. **Build the solution:**
   ```bash
   dotnet build
   ```

2. **Run tests:**
   ```bash
   dotnet test --filter "category!=failing&category!=outerloop"
   ```

3. **If tests fail**, give Copilot the error output:
   ```
   #file:docs/copilot/CopilotMigrationInstructions.md

   I'm getting this build error after migrating to V5:

   error CS1061: 'Person' does not contain a definition for 'AsString'

   How should this be fixed?
   ```

   The "Common Migration Errors" section at the end of `CopilotMigrationInstructions.md` covers the most frequent errors.

---

## Tips for Better Results

### Do

- **Attach `CopilotMigrationInstructions.md` every time.** Copilot doesn't remember between conversations. The reference document is the single biggest factor in getting correct migrations.
- **Show Copilot the generated V5 types** when migrating union/composition code. Attach the generated `.g.cs` file so it can see the actual type names.
- **Migrate incrementally** — one file at a time, build between each.
- **Use specific prompts** — "Migrate the parsing code in lines 20-45" beats "Migrate this file".
- **Tell Copilot what the code does** — "This file handles order validation using schema validation and union matching" gives Copilot context to make better decisions.

### Don't

- **Don't trust namespace changes blindly** — `System.Text.Json.Utf8JsonWriter` → `Corvus.Text.Json.Utf8JsonWriter` is a real change (different type), not just a rename.
- **Don't assume type names are the same** — V5's naming heuristics differ from V4. Always verify against generated output.
- **Don't migrate the whole project in one prompt** — Copilot works best with focused, file-level tasks.
- **Don't skip the build step** — `TreatWarningsAsErrors=true` means even warnings will fail the build. Catch issues early.

---

## Quick Reference: What Copilot Handles Well vs. What Needs Human Review

| Transformation | Copilot Reliability | Human Review Needed |
|---|---|---|
| Namespace changes (`Corvus.Json` → `Corvus.Text.Json`) | ✅ High | Minimal |
| `ParsedValue` → `ParsedJsonDocument` | ✅ High | Check disposal |
| `.Instance` → `.RootElement` | ✅ High | Minimal |
| Property access (unchanged patterns) | ✅ High | Minimal |
| `With*()` → `JsonWorkspace` + `Set*()` | ⚠️ Medium | Verify workspace/builder lifecycle |
| `AsString`/`AsNumber` removal | ⚠️ Medium | Verify correct accessor used |
| `Match()` / `TryGetAs*()` type names | ⚠️ Medium | Cross-check generated types |
| Generated property/type name changes | ❌ Low | Must check generated output |
| Complex validation migration | ⚠️ Medium | Verify collector usage |
| Tuple/tensor/numeric array creation | ⚠️ Medium | Verify correct overload |

---

## Troubleshooting

### Copilot suggests V4 patterns

It may have been trained on V4 examples. Re-attach `CopilotMigrationInstructions.md` and be explicit:

```
Use V5 patterns only. Do NOT use With*(), ParsedValue, AsString,
or any V4 APIs. Refer to the attached migration instructions.
```

### Copilot uses wrong type names

Show it the generated types:

```
#file:generated/Person.g.cs

Use the actual type names from this generated file. For example, V4's
framework type Corvus.Json.JsonString is now a project-local JsonString,
and V4 custom entity types like OneOf1Entity (for "type":"integer" + format)
become project-local globals like JsonInt32.
```

### Copilot doesn't add disposal

Be explicit about the resource management pattern:

```
ParsedJsonDocument and JsonDocumentBuilder are disposable. Always use
'using var' or a using block. JsonWorkspace must also be disposed.
```
