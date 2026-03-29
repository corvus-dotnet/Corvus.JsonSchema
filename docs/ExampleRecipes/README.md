# Example Recipes — JSON Schema Patterns in .NET

This folder contains runnable examples demonstrating common JSON Schema patterns using the V5 code generator (`Corvus.Text.Json`).

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
| [001](001-DataObject/) | Data Object | Parsing, property access, string comparison, serialization, equality |
| [002](002-DataObjectValidation/) | Data Object Validation | Schema constraints, `EvaluateSchema()`, detailed validation results |
| [003](003-ReusingCommonTypes/) | Reusing Common Types | `$ref` / `$defs`, shared nested types |
| [004](004-OpenVersusClosedTypes/) | Open vs Closed Types | `additionalProperties`, mutable `RemoveProperty` / `SetProperty` |
| [005](005-ExtendingABaseType/) | Extending a Base Type | Schema inheritance via `$ref`, `From()` conversion |
| [006](006-ConstrainingABaseType/) | Constraining a Base Type | Tighter constraints on a base, validation differences |
| [007](007-CreatingAStronglyTypedArray/) | Strongly Typed Array | Array mutation via builder, indexing, enumeration |
| [008](008-CreatingAnArrayOfHigherRank/) | Higher Rank Array | 2D arrays, multi-index access |
| [009](009-WorkingWithTensors/) | Working with Tensors | 3D tensors, `TryGetNumericValues`, `Build`, `CreateBuilder`, `Rank`, `Dimension` |
| [010](010-CreatingTuples/) | Creating Tuples | `CreateTuple` via builder, `Item1`/`Item2`/`Item3` access |
| [011](011-InterfacesAndMixInTypes/) | Interfaces and Mix-In Types | `allOf` composition, `From()` conversion |
| [012](012-PatternMatching/) | Pattern Matching | `oneOf`, `Match` with named parameters |
| [013](013-PolymorphismWithDiscriminators/) | Polymorphism with Discriminators | `const` discriminator properties, `Match` |
| [014](014-StringEnumerations/) | String Enumerations | String `enum`, `Match` with state |
| [015](015-NumericEnumerations/) | Numeric Enumerations | Numeric `const`, `ConstInstance`, explicit conversion |
| [016](016-Maps/) | Maps | Typed map (object with `additionalProperties`), builder construction |
| [017](017-MappingInputAndOutputValues/) | Mapping Input/Output | Cross-model mapping, `From()`, mutable pipeline |

## Related documentation

- [Getting Started with Code Generation](../GettingStartedWithCodeGeneration.md)
- [Migrating from V4 to V5](../MigratingFromV4ToV5.md)
- [ParsedJsonDocument](../ParsedJsonDocument.md)
- [JsonDocumentBuilder](../JsonDocumentBuilder.md)
