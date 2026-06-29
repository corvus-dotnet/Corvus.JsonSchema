---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "TypeScript Guides"
---
Once you have worked through [Getting Started](/typescript/index.html), these guides cover the generated surface in depth. Each is a focused topic; read them in any order.

## [Reading and validating](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/typescript/reading-and-validating.md)

The read side of a generated module: the `readonly` type surface, and the boolean evaluators on each type's companion. Covers `Type.evaluate`, collecting detailed failure results, narrowing `oneOf` unions with `match` and the per-branch type guards, and the *evaluate-then-cast* pattern at a trust boundary.

## [Mutation](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/typescript/mutation.md)

Constructing and editing documents as canonical UTF-8 JSON bytes: `build` and `buildCanonical`, `patch` for targeted changes, and `produce` for a recipe-style draft. Explains the byte-level engine that splices only the changed spans instead of parsing, mutating, and re-serialising the whole document.

## [The type surface](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/typescript/the-type-surface.md)

A reference mapping every JSON Schema construct to the TypeScript it generates: objects and `interface`s, optionality and `null`, `$ref`, composition (`allOf`, `oneOf`, `anyOf`), arrays and tuples, enums, maps, and conditionals.

## [Value types](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/typescript/value-types.md)

The branded value types: `format` strings and their validating `from` factories, exact and arbitrary-precision numbers, typed-array views over numeric arrays, and `Temporal` dates, times, and durations via the `toTemporal` accessors.
