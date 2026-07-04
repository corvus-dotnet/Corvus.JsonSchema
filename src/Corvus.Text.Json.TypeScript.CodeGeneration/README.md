# Corvus.Text.Json.TypeScript.CodeGeneration

The JSON Schema to TypeScript model engine. It is the TypeScript counterpart of the C# model generator in `Corvus.Text.Json.CodeGeneration`, emitting idiomatic, byte-native TypeScript types from JSON Schema: an AOT-compiled boolean validator, a canonical UTF-8 `build`, `parse`, `evaluate`, byte-level `patch` and `produce`, and branded types for `format` keywords.

It does not transcribe the C# emitter. It emits TypeScript that validates directly over parsed values and byte buffers, so the generated models carry no intermediate object model and no per-keyword allocation on the happy path.

## Key types

- **`TypeScriptLanguageProvider`** is the entry point. It walks a reduced JSON Schema type graph and emits, per type, the type surface (interface or map, union, enum, branded string/number/format, array), the companion object (`evaluate`, `parse`, `build`, `patch`, `produce`, `withDefaults`, `defaults`, and the format accessors), and the validator.
- **`ITsKeywordEmitter`** is the per-keyword emit contract. The `Ts*Handler` family implements it, one handler per JSON Schema keyword or group (`TsTypeHandler`, `TsObjectPropertiesHandler`, `TsAllOfHandler`, `TsOneOfHandler`, `TsIfThenElseHandler`, `TsFormatHandler`, `TsRegexHandler`, `TsUnevaluatedPropertiesHandler`, and the rest).
- **`TsEmit`** provides the shared emit helpers (identifier and string escaping, literal rendering).

The generated code imports its shared runtime (temporal, big-number, patch, and format helpers) from the `@endjin/corvus-json-runtime` npm package.

## Related packages

- `Corvus.Text.Json.CodeGeneration` is the C# model generator.
- `Corvus.Text.Json.OpenApi.TypeScript.CodeGeneration` uses this engine for OpenAPI client model types.
- `Corvus.Text.Json` is the core library.
