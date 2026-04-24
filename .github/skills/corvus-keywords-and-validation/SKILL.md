---
name: corvus-keywords-and-validation
description: >
  Implement and extend JSON Schema keywords and validation handlers in the Corvus code
  generation system. Covers the IKeyword interface, behavioral marker interfaces,
  vocabulary registration, draft-specific keyword variations (Draft 4 through 2020-12),
  validation handler priorities, custom keyword extension points, and the TypeDeclaration
  data structure. USE FOR: adding new JSON Schema keywords, modifying validation behavior,
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

Handlers execute in strict priority order:

| Priority | Name | Purpose |
|----------|------|---------|
| 1000 | CoreType | Basic type checking (`type` keyword) |
| 2000 | Default | Standard validation (min/max, pattern, etc.) |
| 3000 | Composition | `allOf`, `anyOf`, `oneOf`, `not` |
| 4000 | AfterComposition | Keywords that depend on composition results |
| 5000 | Last | Final cleanup, unevaluated properties/items |

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

## TypeDeclaration

The central data structure representing a resolved JSON Schema as a C# type:
- Holds the schema keywords, their values, and resolved references
- Tracks the type's place in the inheritance/composition hierarchy
- Type reduction collapses trivial subschemas for leaner generated code

## Common Pitfalls

- **Draft awareness**: Always check which draft(s) your keyword applies to. A keyword may have different semantics or not exist in certain drafts.
- **Priority order**: Putting a handler at the wrong priority can cause it to run before its dependencies are resolved.
- **Stateless keywords**: Keywords must be stateless singletons. State lives in `TypeDeclaration`.

## Cross-References
- For code generation, see `corvus-codegen`
- For the standalone evaluator's internal architecture, see `corvus-standalone-evaluator`
- For annotation flow, see `corvus-standalone-evaluator` (annotation pipeline section)
- Full guide: `docs/AddingKeywords.md`, `docs/ValidationHandlerGuide.md`
