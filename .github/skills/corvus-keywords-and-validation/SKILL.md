---
name: corvus-keywords-and-validation
description: >
  Implement and extend JSON Schema keywords in the Corvus code generation system and the
  runtime evaluator. Covers the IKeyword interface, behavioral marker interfaces,
  vocabulary registration, draft-specific keyword variations (Draft 4 through 2020-12),
  where assertions are implemented (SchemaCompiler/Evaluator), custom keyword extension
  points, and the TypeDeclaration data structure. USE FOR: adding new JSON Schema keywords, modifying validation behavior,
  understanding keyword evolution across drafts, extending the code generation engine,
  understanding vocabularies. DO NOT USE FOR: using generated types (use corvus-codegen),
  standalone evaluator internals (use corvus-standalone-evaluator).
---

# Keywords and Validation Handlers

## Keyword Architecture

Keywords are **stateless singletons** implementing `IKeyword` plus behavioral marker interfaces.

### IKeyword Interface

Every keyword implements:
- `Keyword` property — the JSON property name (e.g., `"type"`, `"maxLength"`)
- Behavioral marker interfaces that declare how the keyword participates in schema processing

### Behavioural Marker Interface Categories (25+)

1. **Schema structure** — type shape: `ISubschemaTypeBuilderKeyword`, `ILocalSubschemaRegistrationKeyword`, `IPropertySubchemaProviderKeyword`
2. **Validation** — runtime checks: `IObjectPropertyValidationKeyword`, `IValueKindValidationKeyword`, `INumericValidationKeyword`, `IStringValidationKeyword`, `IArrayValidationKeyword`
3. **Annotation & documentation** — `IAnnotationProducingKeyword`, `IShortDocumentationProviderKeyword`, `ILongDocumentationProviderKeyword`, `IDefaultValueProviderKeyword`, `IExamplesProviderKeyword`, `INonStructuralKeyword`, `IDeprecatedKeyword`
4. **References & identity** — `IRefKeyword`, `IDynamicRefKeyword`, `IIdKeyword`, `IAnchorKeyword`, `ISchemaKeyword`

## Vocabularies

Vocabularies are modular sets of keywords (introduced formally in Draft 2019-09):
- **Core** — `$id`, `$schema`, `$ref`, `$defs`
- **Applicator** — `allOf`, `anyOf`, `oneOf`, `if/then/else`, `properties`, `items`
- **Validation** — `type`, `minimum`, `maximum`, `pattern`, `required`
- **MetaData** — `title`, `description`, `default`, `examples`
- **Format** — `format` keyword (annotation in 2019-09+, assertion in earlier drafts)
- **Content** — `contentEncoding`, `contentMediaType`, `contentSchema`
- **Unevaluated** — `unevaluatedProperties`, `unevaluatedItems` (2019-09+)

## Validation Handler Priorities

Handlers execute in strict priority order (defined in `ValidationPriorities` static class in `src-v4/Corvus.Json.CodeGeneration/.../Validation/ValidationPriorities.cs`, shared by both V4 and V5):

| Priority | Name | Value | Purpose |
|----------|------|-------|---------|
| First | `First` | 0 | Must run before everything |
| CoreType | `CoreType` | 1,000 | Basic type checking (`type` keyword) |
| Default | `Default` | ~2.1 billion (`uint.MaxValue / 2`) | Standard validation (min/max, pattern, etc.) |
| Composition | `Composition` | Default + 1,000 | `allOf`, `anyOf`, `oneOf`, `not` |
| AfterComposition | `AfterComposition` | Composition + 1,000 | Keywords that depend on composition results (arrays, objects) |
| Last | `Last` | `uint.MaxValue` | Final cleanup, unevaluated properties/items |

The large gap between CoreType (1,000) and Default (~2.1B) leaves room for future priorities without redefining existing values.

## Draft-Specific Keyword Variations

Keywords change behavior across JSON Schema drafts:

| Keyword | Draft 4 | Draft 6+ |
|---------|---------|----------|
| `exclusiveMaximum` | Boolean modifier on `maximum` | Standalone number |
| `exclusiveMinimum` | Boolean modifier on `minimum` | Standalone number |
| `items` | Single schema or array | Single schema only in 2020-12 (`prefixItems` for array) |
| `additionalItems` | Used with array `items` | Removed in 2020-12 |
| `$ref` | Replaces all siblings | Coexists with siblings in 2019-09+ |

## Custom Keyword Extension Points (8)

The code generation engine exposes extension points at different phases:

1. **Schema discovery** — register custom schema locations
2. **Type declaration building** — modify type declarations during construction
3. **Type reduction** — control how trivial subschemas are collapsed
4. **Validation handler registration** — add custom validation code emitters
5. **Property handler** — customize how object properties are processed
6. **Array handler** — customize array item processing
7. **Format handler** — add custom format validators
8. **Composition handler** — customize allOf/anyOf/oneOf/not processing

## Where Validation Lives

Validation handlers no longer exist. Keywords are declared for the type builder (so generated *types*
reflect them), but every assertion is implemented once in the runtime evaluator
(`src/Corvus.Text.Json.RuntimeEvaluator`), which generated types, standalone evaluators, the Validator and
the CLI all use:

- `Compilation/SchemaCompiler.cs` — `CompileNode` reads each keyword into a `SchemaNode` (dispatch by
  keyword name length, then bytes), then whole-graph passes (dynamic refs, tracking, marking, `$ref`
  elision, discriminators, plans, flags).
- `Evaluation/Evaluator.cs` — the assertions, generic over `FastMode`/`CollectingMode` and the document
  access type; report results with the keyword name through the collector.
- Tests: `tests/Corvus.Text.Json.RuntimeEvaluator.Tests` (unit + JSON Schema Test Suite), plus the
  generated suite projects, which now run the same engine through generated types.

The generator emits one `CorvusJsonSchemaProgram` per compilation (`RuntimeProgramGenerator.cs`) with an
entry point per type: pre-compiled as an image when the host supplies `Options.ProgramCompiler` (the CLI),
else holding the schema documents for compilation on first use; see `docs/StandaloneEvaluatorInternals.md`.

## TypeDeclaration

The central data structure representing a resolved JSON Schema as a C# type:
- Holds the schema keywords, their values, and resolved references
- Tracks the type's place in the inheritance/composition hierarchy
- Type reduction collapses trivial subschemas for leaner generated code

## Common Pitfalls

- **Draft awareness**: Always check which draft(s) your keyword applies to. A keyword may have different semantics or not exist in certain drafts.
- **Stateless keywords**: Keywords must be stateless singletons. State lives in `TypeDeclaration`.

## Cross-References
- For code generation, see `corvus-codegen`
- For the evaluation program and evaluator-only generation, see `corvus-standalone-evaluator`
- Full guide: `docs/AddingKeywords.md`, `docs/ValidationHandlerGuide.md`, `docs/RuntimeEvaluator.md`
