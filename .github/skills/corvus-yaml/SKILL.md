---
name: corvus-yaml
description: >
  Convert between YAML 1.2 and JSON using the high-performance ref struct tokenizer. Covers the
  two packages (Corvus.Text.Json.Yaml for full integration, Corvus.Yaml.SystemTextJson
  for System.Text.Json-only), YAML→JSON and JSON→YAML conversion, schema modes,
  anchor/alias support, multi-document streams, Utf8YamlWriter, and 100% yaml-test-suite JSON-testable
  conformance (373 of 402 tests). USE FOR: YAML-to-JSON conversion, JSON-to-YAML conversion,
  understanding the tokenizer architecture, troubleshooting YAML parsing issues.
  DO NOT USE FOR: JSON-only workflows with no YAML involvement.
---

# YAML 1.2 ↔ JSON Conversion

## Overview

**Conformance:** 100% of JSON-testable cases — 373 of 402 total (279 valid + 94 error). The remaining 29 cases exercise YAML features with no JSON equivalent.

The YAML converter operates as a zero-allocation `ref struct` tokenizer on UTF-8 bytes, streaming tokens directly into a `Utf8JsonWriter`.

## Two Packages

| Package | Purpose |
|---------|---------|
| `Corvus.Text.Json.Yaml` | Full Corvus integration — returns `ParsedJsonDocument<T>` |
| `Corvus.Yaml.SystemTextJson` | System.Text.Json only — writes to `Utf8JsonWriter` |

## Basic Usage

### YAML → JSON

```csharp
// Full Corvus integration — returns ParsedJsonDocument<T>
using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yamlBytes);
JsonElement root = doc.RootElement;

// System.Text.Json only — returns JsonDocument
JsonDocument doc = YamlDocument.Parse(yamlBytes);

// Get JSON string from YAML
string json = YamlDocument.ConvertToJsonString(yamlBytes);
```

### JSON → YAML

```csharp
// Convert a JSON element to a YAML string (static method on YamlDocument)
string yaml = YamlDocument.ConvertToYamlString(jsonElement);

// Convert to YAML, writing to an IBufferWriter<byte>
YamlDocument.ConvertToYaml(jsonElement, bufferWriter);

// Convert to YAML, writing to a Stream
YamlDocument.ConvertToYaml(jsonElement, utf8Stream);
```

## Schema Modes

Four YAML schema modes control how scalar values are interpreted:

| Mode | Description |
|------|-------------|
| Core | YAML 1.2 Core Schema (default) |
| JSON | JSON-compatible — strings, numbers, booleans, null only |
| Failsafe | All scalars as strings |
| Yaml11 | YAML 1.1 compatibility (yes/no/on/off booleans, sexagesimal integers, merge keys) |

## Features

- **Anchors & aliases**: Full support for `&anchor` and `*alias` references
- **Multi-document streams**: Processes `---` / `...` document boundaries
- **Block & flow styles**: All YAML block and flow collection styles
- **Tag resolution**: `%TAG` directives, standard `!!` tags, custom tags

## Running Tests

```powershell
dotnet test tests\Corvus.Text.Json.Yaml.Tests --filter "category!=failing&category!=outerloop"
```

## Key Implementation Details

These are important if you're modifying the YAML converter:

- **FinishAnchor**: Must not clear `_activeAnchorNameStart` when `hasAnchor=false`
- **Block scalars**: Must check for document markers (`---`/`...`) at column 0
- **`%TAG !!` redefinition**: Makes `!!name` tags non-standard
- **Float normalization**: Written via `Utf8Parser` + `WriteNumberValue` for consistent output

## Common Pitfalls

- **Encoding**: The tokenizer expects UTF-8 bytes. Pass `ReadOnlySpan<byte>` for best performance.
- **Multi-document**: A YAML stream may contain multiple documents. The converter handles them, but the consumer must be aware.
- **Scalar resolution**: In Core schema mode, strings like `true`, `false`, `null`, and numbers are auto-resolved. Use Failsafe mode to keep everything as strings.

## Cross-References
- For parsed document handling, see `corvus-parsed-documents-and-memory`
- For building/testing, see `corvus-build-and-test`
- Full guide: `docs/Yaml.md`
