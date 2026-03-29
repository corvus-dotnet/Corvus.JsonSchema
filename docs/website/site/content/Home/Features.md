---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Features"
---
## 🔧 Source Generation

Generate strongly-typed C# from JSON Schema at build time with the Roslyn incremental source generator, or ahead of time with the `generatejsonschematypes` CLI tool. Your models get type-safe property accessors, validation, serialization, and implicit conversions — all from a single schema file. The generator walks the schema tree and produces `readonly struct` types that are lightweight indexes into pooled JSON data.

## 📋 Schema Validation

Full JSON Schema draft 4, 6, 7, 2019-09, and 2020-12 validation with `EvaluateSchema()`. Over 10x faster than other .NET JSON Schema validators. Get detailed diagnostic results including the exact schema location, evaluation path, and error messages for every validation failure. Choose from four levels — flag-only `bool`, basic failure messages, detailed messages with locations, or verbose output including successful validations.

## ⚡ Pooled Memory

`ParsedJsonDocument<T>` uses `ArrayPool<byte>` to parse JSON with minimal GC impact. Just 120B per-document allocation vs 1,528 bytes for equivalent `JsonNode` operations — 92% less memory. Generated types are thin struct wrappers — creating a typed value from a parsed document is essentially free.

## 🔄 Mutable Documents

`JsonDocumentBuilder<T>` and `JsonWorkspace` provide a high-performance builder pattern for creating and modifying JSON. Pooled workspace memory is reused across operations, ideal for request/response cycles and data pipelines. Call `Set*()` and `Remove*()` methods on mutable elements, then serialize via `WriteTo()` or convert to immutable via `Clone()`.

## 📐 Extended Types

`BigNumber` for arbitrary-precision decimals, `BigInteger` for large integers, plus NodaTime integration: `LocalDate`, `OffsetDateTime`, `OffsetTime`, and `Period` map directly from JSON Schema `format` keywords. UTF-8 URI/IRI types (`Utf8UriValue`, `Utf8IriValue`) avoid UTF-16 string allocations. Zero-allocation string comparison with `ValueEquals()`.

## 🧩 Pattern Matching

Type-safe `Match()` for `oneOf`/`anyOf` discriminated unions with exhaustive delegate-per-variant dispatch. Full `allOf` composition with `From()` conversion between generated types. String and numeric enumerations with `Match()` support, including context-passing overloads that avoid closure allocations.
