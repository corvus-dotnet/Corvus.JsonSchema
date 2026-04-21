# Corvus.Text.Json

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

High-performance, source-generated, strongly-typed C# models from JSON Schema — with pooled-memory parsing, full draft 4 through 2020-12 validation, and 120B per-document allocation.

## Features

- **Source Generation** — Generate strongly-typed C# from JSON Schema at build time with the Roslyn incremental source generator, or ahead of time with the `generatejsonschematypes` CLI tool.
- **Schema Validation** — Full JSON Schema draft 4, 6, 7, 2019-09, and 2020-12 validation. Over 10× faster than other .NET JSON Schema validators.
- **Pooled Memory** — `ParsedJsonDocument<T>` uses `ArrayPool<byte>` for minimal GC impact. Just 120B per-document vs 1,528B for `JsonNode` — 92% less memory.
- **Mutable Documents** — `JsonDocumentBuilder<T>` and `JsonWorkspace` provide a builder pattern for creating and modifying JSON with pooled workspace memory.
- **Extended Types** — `BigNumber` for arbitrary-precision decimals, `BigInteger` for large integers, plus NodaTime integration for `date`, `date-time`, `time`, and `duration` formats.
- **Pattern Matching** — Type-safe `Match()` for `oneOf`/`anyOf` discriminated unions with exhaustive dispatch.
- **[JSONata](#jsonata)** — Full [JSONata](https://jsonata.org/) query and transformation language with 100% test-suite conformance. Interpreted and code-generated evaluation modes.
- **[JMESPath](#jmespath)** — Full [JMESPath](https://jmespath.org/) query language with 100% conformance against the official test suite. Interpreted and code-generated evaluation modes.
- **JsonLogic** — Complete [JsonLogic](https://jsonlogic.com/) rule engine for evaluating business rules against JSON data with interpreted and code-generated modes.
- **JSON Patch** — RFC 6902 JSON Patch with pooled-memory operations on `JsonElement`.
- **[YAML](#yaml)** — High-performance YAML 1.2 to JSON converter with 100% yaml-test-suite conformance. Zero-allocation `ref struct` tokenizer.

## Quick Start

```csharp
// 1. Define a schema (Schemas/person.json)
// {
//     "$schema": "https://json-schema.org/draft/2020-12/schema",
//     "type": "object",
//     "required": ["name"],
//     "properties": {
//         "name": { "type": "string", "minLength": 1 },
//         "age": { "type": "integer", "format": "int32", "minimum": 0 }
//     }
// }

// 2. Use the source generator
[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;

// 3. Parse and access properties
using var doc = ParsedJsonDocument<Person>.Parse(
    """{"name":"Alice","age":30}""");
Person person = doc.RootElement;

string name = (string)person.Name;           // "Alice"
int age = person.Age;                        // 30

// 4. Validate against the schema
bool valid = person.EvaluateSchema();        // true

// 5. Mutate with the builder pattern
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = person.CreateBuilder(workspace);
Person.Mutable root = builder.RootElement;
root.SetAge(31);

Console.WriteLine(root.ToString());
// {"name":"Alice","age":31}
```

## NuGet Packages

| Package | Description |
|---|---|
| **Corvus.Text.Json** | Core runtime library. Required by all generated types. |
| **Corvus.Text.Json.SourceGenerator** | Roslyn incremental source generator. Generates C# from JSON Schema at build time. |
| **Corvus.Json.CodeGenerator** | CLI tool (`generatejsonschematypes`) for ahead-of-time code generation. |
| **Corvus.Text.Json.Validator** | Dynamically load and validate JSON against JSON Schema at runtime using Roslyn. |
| **Corvus.Text.Json.Jsonata** | JSONata query and transformation language — interpreted runtime evaluator. |
| **Corvus.Text.Json.Jsonata.SourceGenerator** | Roslyn source generator for compile-time JSONata code generation. |
| **Corvus.Text.Json.JMESPath** | JMESPath query language — interpreted runtime evaluator. |
| **Corvus.Text.Json.JMESPath.SourceGenerator** | Roslyn source generator for compile-time JMESPath code generation. |
| **Corvus.Text.Json.JsonLogic** | JsonLogic rule engine — interpreted runtime evaluator. |
| **Corvus.Text.Json.JsonLogic.SourceGenerator** | Roslyn source generator for compile-time JsonLogic code generation. |
| **Corvus.Text.Json.Patch** | RFC 6902 JSON Patch with pooled-memory operations. |
| **Corvus.Text.Json.Yaml** | YAML 1.2 to JSON converter with Corvus document model integration. |
| **Corvus.Yaml.SystemTextJson** | YAML 1.2 to JSON converter using only System.Text.Json (no Corvus dependency). |

### Install

```bash
# Core library
dotnet add package Corvus.Text.Json

# CLI code generator
dotnet tool install --global Corvus.Json.CodeGenerator
```

For the source generator, add as an analyzer reference:

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

## Target Frameworks

### V5 engine (Corvus.Text.Json)
- .NET 10.0, 9.0
- .NET Standard 2.0, 2.1

### V4 engine (Corvus.Json)
- .NET 10.0, 8.0
- .NET Standard 2.0

> **Note:** .NET 8.0 is not supported on the V5 engine. The V4 engine will drop .NET 8.0 support in November 2026 when it reaches end-of-life.

## Documentation

Full documentation is available at the [Corvus.Text.Json documentation website](docs/website/.output/index.html).

To build and preview the docs locally:

```powershell
cd docs/website
./preview.ps1
```

Then open http://localhost:5000.

### Guides

- [Parsing & Reading JSON](docs/ParsedJsonDocument.md)
- [Building & Mutating JSON](docs/JsonDocumentBuilder.md)
- [Source Generator](docs/SourceGenerator.md)
- [CLI Code Generation](docs/CodeGenerator.md)
- [Dynamic Schema Validation](docs/Validator.md)
- [JSONata Query & Transformation](docs/Jsonata.md)
- [JMESPath Query Language](docs/JMESPath.md)
- [YAML to JSON Converter](docs/Yaml.md)
- [Migrating from V4](docs/MigratingFromV4ToV5.md)

## Building

```bash
dotnet build Corvus.Text.Json.slnx
```

## Testing

```bash
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"
```

## Comparison with System.Text.Json

| Feature | System.Text.Json | Corvus.Text.Json |
|---|---|---|
| Read-only parsing | `JsonDocument` (pooled) | `ParsedJsonDocument<T>` (pooled, generic) |
| Mutable documents | `JsonNode` (allocates per node) | Builder pattern on pooled memory |
| Schema validation | None built-in | Draft 4/6/7/2019-09/2020-12 with full diagnostics |
| Code generation | Serialization to/from POCOs | Strongly-typed entities from JSON Schema |
| Date/Time | `DateTime`, `DateTimeOffset` | All .NET types plus NodaTime |
| Numeric precision | `decimal` (28 digits) | `BigNumber` (arbitrary precision), `Int128`, `Half` |
| String/URI handling | .NET string allocation | Zero-allocation UTF-8/UTF-16 access |

## JSONata

`Corvus.Text.Json.Jsonata` implements [JSONata](https://jsonata.org/) — an expressive, Turing-complete functional query and transformation language for JSON. It supports path navigation, predicate filtering, higher-order functions (`$map`, `$filter`, `$reduce`, `$sort`), object construction, string manipulation, arithmetic, regular expressions, and user-defined functions.

- **100% conformance** — passes all 1,665 tests in the official [JSONata test suite](https://github.com/jsonata-js/jsonata)
- **Up to 8× faster** than [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native) with 90–100% less memory allocation
- **Code-generated evaluation** — source generator and CLI tool produce optimized static C# (up to 12× faster)
- **Zero-allocation hot path** — pooled workspace memory with `ArrayPool`-backed evaluation

```csharp
using Corvus.Text.Json.Jsonata;

string? result = JsonataEvaluator.Default.EvaluateToString(
    "Account.Order.Product.Price ~> $sum()",
    """{"Account":{"Order":[{"Product":{"Price":10}},{"Product":{"Price":20}}]}}""");
// result: "30"
```

See [JSONata documentation](docs/Jsonata.md) for the full API, code generation, and performance benchmarks.

## JMESPath

`Corvus.Text.Json.JMESPath` implements [JMESPath](https://jmespath.org/) — a query language for JSON that supports path navigation, sub-expressions, index access, slicing, list and object projections, flatten, filter expressions, multiselect lists and hashes, pipe expressions, comparisons, and built-in functions.

- **100% conformance** — passes all 892 conformance test cases in the official [JMESPath Compliance Test Suite](https://github.com/jmespath/jmespath.test)
- **Up to 150× faster** than [JmesPath.Net](https://github.com/jdevillard/JmesPath.Net) on common benchmarks with zero allocation
- **Code-generated evaluation** — source generator and CLI tool produce optimized static C#
- **Zero-allocation hot path** — pooled workspace memory with `ArrayPool`-backed evaluation

```csharp
using Corvus.Text.Json.JMESPath;

JsonElement result = JMESPathEvaluator.Default.Search(
    "locations[?state == 'WA'].name | sort(@) | {WashingtonCities: join(', ', @)}",
    JsonElement.ParseValue("""
    {
      "locations": [
        {"name": "Seattle", "state": "WA"},
        {"name": "New York", "state": "NY"},
        {"name": "Bellevue", "state": "WA"},
        {"name": "Olympia", "state": "WA"}
      ]
    }
    """u8));

Console.WriteLine(result); // {"WashingtonCities":"Bellevue, Olympia, Seattle"}
```

See [JMESPath documentation](docs/JMESPath.md) for the full API, code generation, and performance benchmarks.

## YAML

`Corvus.Text.Json.Yaml` is a high-performance YAML 1.2 to JSON converter built on a custom `ref struct` tokenizer operating directly on UTF-8 bytes. It supports all YAML scalar styles, flow and block collections, anchors and aliases, multi-document streams, and four schema modes.

- **100% conformance** — passes all 402 tests in the [yaml-test-suite](https://github.com/yaml/yaml-test-suite) (308 valid + 94 invalid)
- **Zero-allocation** on the hot path with `stackalloc`/`ArrayPool` pattern
- **Two packages**: `Corvus.Text.Json.Yaml` (full Corvus integration) and `Corvus.Yaml.SystemTextJson` (System.Text.Json only)

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;

string yaml = """
    name: Alice
    age: 30
    hobbies: [reading, cycling]
    """;

using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
Console.WriteLine(doc.RootElement.GetProperty("name").GetString()); // "Alice"
```

See [YAML documentation](docs/Yaml.md) for the full API, configuration options, and supported YAML features. **[Try the YAML Playground](docs/playground-yaml/)** to convert between YAML and JSON in your browser.

## Supported platforms

### .NET 4.8.1 (Windows)
It now works with .NET 4.8.1 and later by providing `netstandard2.0` packages.

### .NET 9.0, 10.0 (Windows, Linux, MacOs)
The V5 engine provides `net9.0` and `net10.0` packages. These are supported on Windows, Linux, and MacOS.

> **Note:** .NET 8.0 is not supported on the V5 engine. If you need .NET 8.0, use the V4 engine (which will retain .NET 8.0 support until its end-of-life in November 2026).

Note that if you are building libraries using `Corvus.JsonSchema`, you should ensure
that you target *both* `netstandard2.0` *and* `net9.0` (or later) to ensure that your library can be consumed
by the widest possible range of projects.

If you build your library against `netstandard2.0` only, and are consumed by a `net9.0` or later project, you will see type load errors.

## Supported schema dialects

Since V4 we have full support for the following schema dialects:

- Draft 4
- OpenAPI 3.0
- Draft 6
- Draft 7
- 2019-09
- 2020-12 (Including OpenAPI 3.1)

You can see full details of the supported schema dialects [on the bowtie website](https://bowtie.report/#/implementations/dotnet-corvus-jsonschema).

Bowtie is tool for understanding and comparing implementations of the JSON Schema specification across all programming languages.

It uses the official JSON Schema Test Suite to display bugs or functionality gaps in implementations.

## Project Sponsor

This project is sponsored by [endjin](https://endjin.com), a UK based Microsoft Gold Partner for Cloud Platform, Data Platform, Data Analytics, DevOps, and a Power BI Partner.

For more information about our products and services, or for commercial support of this project, please [contact us](https://endjin.com/contact-us).

We produce two free weekly newsletters; [Azure Weekly](https://azureweekly.info) for all things about the Microsoft Azure Platform, and [Power BI Weekly](https://powerbiweekly.info).

Keep up with everything that's going on at endjin via our [blog](https://endjin.com/blog), follow us on [Twitter](https://twitter.com/endjin), or [LinkedIn](https://www.linkedin.com/company/1671851/).

Our other Open Source projects can be found at [https://endjin.com/open-source](https://endjin.com/open-source)

## Code of conduct

This project has adopted a code of conduct adapted from the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. This code of conduct has been [adopted by many other projects](http://contributor-covenant.org/adopters/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [&#104;&#101;&#108;&#108;&#111;&#064;&#101;&#110;&#100;&#106;&#105;&#110;&#046;&#099;&#111;&#109;](&#109;&#097;&#105;&#108;&#116;&#111;:&#104;&#101;&#108;&#108;&#111;&#064;&#101;&#110;&#100;&#106;&#105;&#110;&#046;&#099;&#111;&#109;) with any additional questions or comments.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) for details.
