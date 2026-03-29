---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Example Recipes — JSON Schema Patterns in .NET"
---
This section contains runnable examples demonstrating common JSON Schema patterns using the V5 code generator (`Corvus.Text.Json`).

Each recipe is a standalone console application with its own JSON Schema, generated model types, and example code. The recipes are ordered by increasing complexity and build on each other conceptually.

## Prerequisites

- .NET 10.0 SDK (or later)
- The solution references the source generator and runtime library via local project references, so no NuGet packages need to be installed separately.

## Running a recipe

```bash
# From the repo root
dotnet run --project docs/ExampleRecipes/001-DataObject/DataObject.csproj

# Or from the recipe directory
cd docs/ExampleRecipes/001-DataObject
dotnet run
```

## Recipes

| # | Recipe | Key Concepts |
|---|---|---|
| [001](/examples/data-object.html) | Data Object | Parsing, property access, string comparison, serialization, equality |
| [002](/examples/data-object-validation.html) | Data Object Validation | Schema constraints, `EvaluateSchema()`, detailed validation results |
| [003](/examples/reusing-common-types.html) | Reusing Common Types | `$ref` / `$defs`, shared nested types |
| [004](/examples/open-versus-closed-types.html) | Open vs Closed Types | `additionalProperties`, mutable `RemoveProperty` / `SetProperty` |
| [005](/examples/extending-a-base-type.html) | Extending a Base Type | Schema inheritance via `$ref`, `From()` conversion |
| [006](/examples/constraining-a-base-type.html) | Constraining a Base Type | Tighter constraints on a base, validation differences |
| [007](/examples/strongly-typed-array.html) | Strongly Typed Array | Array mutation via builder, indexing, enumeration |
| [008](/examples/higher-rank-array.html) | Higher Rank Array | 2D arrays, multi-index access |
| [009](/examples/working-with-tensors.html) | Working with Tensors | 3D tensors, `TryGetNumericValues`, `Build`, `CreateBuilder`, `Rank`, `Dimension` |
| [010](/examples/creating-tuples.html) | Creating Tuples | `CreateTuple` via builder, `Item1`/`Item2`/`Item3` access |
| [011](/examples/interfaces-and-mixins.html) | Interfaces and Mix-In Types | `allOf` composition, `From()` conversion |
| [012](/examples/pattern-matching.html) | Pattern Matching | `oneOf`, `Match` with named parameters |
| [013](/examples/polymorphism-with-discriminators.html) | Polymorphism with Discriminators | `const` discriminator properties, `Match` |
| [014](/examples/string-enumerations.html) | String Enumerations | String `enum`, `Match` with state |
| [015](/examples/numeric-enumerations.html) | Numeric Enumerations | Numeric `const`, `ConstInstance`, explicit conversion |
| [016](/examples/maps.html) | Maps | Typed map (object with `additionalProperties`), builder construction |
| [017](/examples/mapping-input-output.html) | Mapping Input/Output | Cross-model mapping, `From()`, mutable pipeline |

## Related documentation

- [Getting Started with Code Generation](/getting-started/index.html)
- [ParsedJsonDocument](/docs/parsed-json-document.html)
- [JsonDocumentBuilder](/docs/json-document-builder.html)
- [Migrating from V4 to V5](/docs/migrating-from-v4.html)
