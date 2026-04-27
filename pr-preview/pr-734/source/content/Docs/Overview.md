---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Documentation"
---
Corvus.Text.Json documentation covers the core APIs, code generation, schema validation, migration guides, and reference material for building high-performance JSON applications in .NET.

## [Parsing & Reading JSON](/docs/parsed-json-document.html)

Parse JSON into read-only, strongly-typed models backed by pooled memory. `ParsedJsonDocument<T>` is a high-performance alternative to `System.Text.Json`'s `JsonDocument` — it adds generic type support, zero-copy element access, and `ArrayPool<byte>`-backed parsing so you can handle high-throughput workloads with minimal GC impact. Learn about parsing from strings, streams, and byte arrays; navigating JSON structures; extracting values including NodaTime types and extended numerics; and serialization patterns for web APIs and data pipelines.

## [Building & Mutating JSON](/docs/json-document-builder.html)

Create and modify JSON documents in-place with workspace-managed pooled memory. `JsonDocumentBuilder<T>` and `JsonWorkspace` are a builder-pattern alternative to `System.Text.Json`'s `JsonNode` — instead of per-node heap allocations, a shared workspace pools memory across operations, making it ideal for request/response cycles and data pipelines. Learn how to create documents from scratch, modify properties in-place, work with nested structures, and serialize with zero-allocation writer rentals.

## [Source Generator](/docs/source-generator.html)

Generate strongly-typed C# from JSON Schema at build time with the Roslyn incremental source generator. Annotate a `partial struct` with `[JsonSchemaTypeGenerator]`, register your schema as an `AdditionalFile`, and get full IntelliSense immediately. Covers the attribute API, MSBuild configuration properties, `AdditionalFiles` setup, inspecting generated output, and incremental build performance.

## [CLI Code Generation](/docs/code-generator.html)

Generate strongly-typed C# models from JSON Schema files using the `corvusjson` CLI tool. Produces the same `readonly struct` types as the Roslyn source generator, but runs ahead of time from the command line. Supports single-schema generation, multi-schema configuration files, document validation, and all schema drafts. Ideal for CI/CD pipelines, pre-generation workflows, and inspecting generated output.

## [Dynamic Schema Validation](/docs/validator.html)

Dynamically load, compile, and validate JSON documents against JSON Schema at runtime. `Corvus.Text.Json.Validator` uses Roslyn to compile schemas on the fly, producing the same validation logic as the build-time source generator. Ideal for schema registries, configuration validation, API gateways, and any scenario where schemas are not known at compile time. Supports all major drafts (4, 6, 7, 2019-09, 2020-12) with detailed diagnostic output.

## [Migrating from V4 to V5](/docs/migrating-from-v4-to-v5.html)

A comprehensive guide for migrating code from the V4 code generator (`Corvus.Json`) to V5 (`Corvus.Text.Json`). Covers package and namespace changes, the shift from functional `With*()` mutation to imperative `Set*()` mutation, parsing changes, validation API differences, composition types, and a quick reference table mapping V4 patterns to V5 equivalents.

## [Migration Analyzers](/docs/migration-analyzers.html)

Roslyn analyzers that detect V4 `Corvus.Json` API patterns in your code and guide you to V5 equivalents. Includes automatic code fixes for namespace changes, type renames, parsing patterns, validation calls, and more. Install the `Corvus.Text.Json.Migration.Analyzers` NuGet package and build — the analyzers do the rest.

## [Using Copilot for Migration](/docs/using-copilot-for-migration.html)

Step-by-step workflow for using GitHub Copilot to assist with V4 → V5 migration. Covers attaching context documents, writing effective prompts, and a reliability matrix showing which transformations Copilot handles well and which need human review.

## [JsonLogic Rule Engine](/docs/json-logic.html)

Evaluate JSON-encoded logic rules against JSON data using the interpreted evaluator, Roslyn source generator, or CLI code generator. `Corvus.Text.Json.JsonLogic` implements the full [JsonLogic](https://jsonlogic.com/) specification with support for extended numeric types (`BigNumber`), zero-allocation evaluation via pooled `JsonWorkspace`, custom operators (`IOperatorCompiler`), and file-based operator templates (`.jlops`). Benchmarks show code-generated evaluators are **60–95% faster** than JsonEverything across all scenarios.

## [JSONata Query Language](/docs/jsonata.html)

Query and transform JSON data using the [JSONata](https://jsonata.org/) functional language. `Corvus.Text.Json.Jsonata` provides interpreted evaluation, a Roslyn source generator, and a CLI code generator — all with zero-allocation pooled-memory evaluation. Passes 1,845 of 1,847 official test suite cases (99.89% conformance). Benchmarks show the interpreted evaluator is **up to 3.5× faster** than [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native) with 90–100% less memory allocation.

## [JSON Patch, Merge Patch & Diff](/docs/json-patch.html)

Apply, construct, and compute JSON patches. `Corvus.Text.Json.Patch` implements [RFC 6902 JSON Patch](https://datatracker.ietf.org/doc/html/rfc6902) with a fluent `PatchBuilder`, [RFC 7396 JSON Merge Patch](https://datatracker.ietf.org/doc/html/rfc7396) for simple document merging, and a diff algorithm that computes the RFC 6902 patch needed to transform one document into another. All operations work with the zero-allocation mutable document infrastructure — merge patch and diff run with 0 B per-call allocation.

## [JSON Canonicalization (RFC 8785)](/docs/json-canonicalization.html)

Produce a deterministic byte-exact serialization of JSON values using the [JSON Canonicalization Scheme (JCS)](https://datatracker.ietf.org/doc/html/rfc8785). Canonical output uses sorted property names, ECMAScript number formatting, and minimal string escaping. Ideal for digital signatures, content hashing, and content-addressed storage. The implementation operates directly on `JsonElement` with zero heap allocation.
