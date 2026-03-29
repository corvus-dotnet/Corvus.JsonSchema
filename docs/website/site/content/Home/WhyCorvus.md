---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Why Corvus.Text.Json?"
---
Corvus.Text.Json is a ground-up rethink of JSON handling in .NET. Where `System.Text.Json` gives you general-purpose parsing and serialization, Corvus.Text.Json starts from JSON Schema and generates strongly-typed C# models that use pooled memory, validate against the schema, and convert directly to .NET types — all with minimal allocations.

If you're building APIs, data pipelines, or any system that processes JSON at volume, here's what sets it apart:

- **92% fewer allocations** — `ArrayPool<byte>`-backed parsing eliminates GC pressure in high-throughput scenarios. Corvus.Text.Json uses just 120B per-document allocation vs 1,528 bytes for equivalent `JsonNode` operations.
- **Full JSON Schema support** — Draft 4, 6, 7, 2019-09, and 2020-12 with complete validation diagnostics including schema location, evaluation path, and error messages for every failure. Over 10x faster than other .NET JSON Schema validators.
- **Source-generated models** — A Roslyn incremental source generator or `generatejsonschematypes` CLI tool produces strongly-typed C# from any JSON Schema. You get type-safe property accessors, validation, serialization, and implicit conversions from a single schema file.
- **NodaTime integration** — JSON Schema `date`, `date-time`, `time`, and `duration` formats map to NodaTime types (`LocalDate`, `OffsetDateTime`, `OffsetTime`, `Period`), not error-prone `System.DateTime`.
- **Mutable builder pattern** — `JsonDocumentBuilder` with `JsonWorkspace`-managed pooled memory, not per-node allocations like `JsonNode`. Ideal for request/response cycles and data pipelines.
- **Extended numeric types** — `BigNumber` for arbitrary-precision decimals and `BigInteger` for large integers, going beyond `decimal`'s 28 significant digits.

## Comparison

| Feature | System.Text.Json | Corvus.Text.Json |
|---------|-----------------|-----------------|
| Read-only Memory Model | ArrayPool-backed pooled memory | ArrayPool-backed pooled memory |
| Mutable Documents | JsonNode (allocates per node) | Builder pattern on the same pooled memory model — no data copying in JSON pipelines |
| Schema Validation | None built-in | Draft 4, 6, 7, 2019-09, and 2020-12 with full diagnostics. 10x+ faster than other .NET validators |
| Code Generation | Source generation for serialization to/from fixed .NET POCOs | Source generator + CLI tool producing strongly-typed entities with composition, pattern matching, and resilience to invalid schema |
| Date/Time Types | System.DateTime, DateTimeOffset | All .NET types (DateTime, DateOnly, TimeOnly where available) plus NodaTime (LocalDate, OffsetDateTime, Period) |
| Numeric Precision | decimal (28 digits); Int128, UInt128, Half via custom serializers | BigNumber (arbitrary precision) plus native Int128, UInt128, and Half support where available |
| Strings & URIs | Allocates .NET strings; System.Uri for URI handling | Allocation-free access to UTF-8 and UTF-16 unescaped strings; UTF-8 Uri/Iri with display and canonical formatting without string allocation |
