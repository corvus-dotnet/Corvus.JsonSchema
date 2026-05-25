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
| [018](018-CreatingAndMutatingObjects/) | Creating and Mutating Objects | Parse immutable JSON, create mutable builder, read/set/remove properties |
| [019](019-CloneAndFreeze/) | Clone and Freeze | `Clone()` vs `Freeze()`, workspace-scoped vs standalone objects, nested freeze |
| [020](020-ConditionalSchemas/) | Conditional Schemas | `if`/`then`/`else`, `EvaluateSchema` for alternate object shapes |
| [021](021-DefaultValues/) | Default Values | Schema defaults via property getters, `DefaultInstance`, explicit values override |
| [022](022-FormatValidation/) | Format Validation | Format-aware parsing: date-time, date, duration, uuid, email, uri/iri |
| [023](023-JsonPatch/) | JSON Patch | Build/apply RFC 6902 patches, `TryAdd`/`TryReplace`/`TryRemove`, fluent builder |
| [024](024-JsonLogic/) | JsonLogic | Evaluate JsonLogic rules, data access operators, typed results |
| [025](025-Jsonata/) | JSONata | Evaluate JSONata expressions, path navigation, filtering, built-in functions |
| [026](026-Jmespath/) | JMESPath | JMESPath `Search()`, projections, filtering, expression evaluation |
| [027](027-Yaml/) | YAML | Parse YAML to JSON, convert JSON to YAML, Core Schema type resolution |
| [028](028-JsonCanonicalization/) | JSON Canonicalization | RFC 8785 property sorting, number normalization, zero-allocation, hashing |
| [028](028-JsonPath/) | JSONPath | RFC 9535 queries: property access, wildcards, recursive descent, slices, filters |
| [029](029-OpenApiClient/) | OpenAPI Client | Generated client, typed params/responses, headers, request/response validation |
| [030](030-OpenApiServer/) | OpenAPI Server | Generated minimal API server, handler interfaces, typed params/results, validation |
| [031](031-OpenApiAdvancedClient/) | OpenAPI Advanced Client | Deep-object/array params, cookies, headers, streaming, multipart, binary |
| [032](032-OpenApiAdvancedServer/) | OpenAPI Advanced Server | Advanced server handlers: deep-object params, streaming, files, forms, headers |
| [033](033-OpenApiEndToEnd/) | OpenAPI End to End | Full generated client/server round-trip over HTTP, typed end-to-end flow |
| [034](034-OpenApiCallbackServer/) | OpenAPI Callback Server | Callback/webhook server endpoints, generated handler interfaces |
| [035](035-OpenApiCallbackClient/) | OpenAPI Callback Client | Webhook/callback client publishing, typed body builders, validation modes |
| [036](036-AsyncApiProducer/) | AsyncAPI Producer | AsyncAPI publishing, in-memory transport, validation, auth, channel parameters |
| [037](037-AsyncApiConsumer/) | AsyncAPI Consumer | Consumer setup, message delivery, validation/error policy, typed handlers |
| [038](038-AsyncApiEndToEnd/) | AsyncAPI End to End | Producer + consumer pipeline, in-memory transport, validation, dead-letter handling |
| [039](039-AsyncApiAuthentication/) | AsyncAPI Authentication | Azure Identity, OAuth2, Bearer, API Key, mTLS, composite auth patterns |

## Related documentation

- [Getting Started with Code Generation](../GettingStartedWithCodeGeneration.md)
- [Migrating from V4 to V5](../MigratingFromV4ToV5.md)
- [ParsedJsonDocument](../ParsedJsonDocument.md)
- [JsonDocumentBuilder](../JsonDocumentBuilder.md)
- [JSON Patch](../JsonPatch.md)
- [JSON Canonicalization](../JsonCanonicalization.md)
- [JSONPath](../JsonPath.md)
- [OpenAPI](../OpenApi.md)
- [AsyncAPI](../AsyncApi.md)
