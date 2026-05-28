# Corvus.Text.Json

[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

High-performance, source-generated, strongly-typed C# models from JSON Schema — with pooled-memory parsing, full draft 4 through 2020-12 validation, and 136B per-document allocation.

## Features

- **Source Generation** — Generate strongly-typed C# from JSON Schema at build time with the Roslyn incremental source generator, or ahead of time with the `corvusjson` CLI tool.
- **[OpenAPI](#openapi)** — Generate strongly-typed OpenAPI 3.0, 3.1, and 3.2 HTTP clients and ASP.NET Core server stubs with typed parameters, request/response validation, streaming, and result matching.
- **[AsyncAPI](#asyncapi)** — Generate strongly-typed AsyncAPI 2.6 and 3.0 producers, consumers, handlers, and request/reply flows with broker transport packages for NATS, Kafka, AMQP, MQTT, WebSocket, and Azure Service Bus.
- **Schema Validation** — Full JSON Schema draft 4, 6, 7, 2019-09, and 2020-12 validation. Over 10× faster than other .NET JSON Schema validators.
- **Pooled Memory** — `ParsedJsonDocument<T>` uses `ArrayPool<byte>` for minimal GC impact. Just 136 bytes per-document allocation.
- **Mutable Documents** — `JsonDocumentBuilder<T>` and `JsonWorkspace` provide a builder pattern for creating and modifying JSON with pooled workspace memory.
- **Extended Types** — `BigNumber` for arbitrary-precision decimals, `BigInteger` for large integers, plus NodaTime integration for `date`, `date-time`, `time`, and `duration` formats.
- **Pattern Matching** — Type-safe `Match()` for `oneOf`/`anyOf` discriminated unions with exhaustive dispatch.
- **[JSONata](#jsonata)** — Full [JSONata](https://jsonata.org/) query and transformation language with 100% test-suite conformance. Interpreted and code-generated evaluation modes.
- **[JMESPath](#jmespath)** — Full [JMESPath](https://jmespath.org/) query language with 100% conformance against the official test suite. Interpreted and code-generated evaluation modes.
- **[JsonLogic](#jsonlogic)** — Complete [JsonLogic](https://jsonlogic.com/) rule engine for evaluating business rules against JSON data with interpreted and code-generated modes.
- **[JSONPath](#jsonpath)** — Full [JSONPath (RFC 9535)](https://www.rfc-editor.org/rfc/rfc9535) query language with 100% conformance against the official compliance test suite. Interpreted and code-generated evaluation modes.
- **JSON Pointer** — RFC 6901 JSON Pointer for navigating and resolving paths within JSON documents.
- **JSON Patch, Merge Patch & Diff** — RFC 6902 JSON Patch, RFC 7396 Merge Patch, and diff with zero-allocation operations on `JsonElement`.
- **JSON Canonicalization** — RFC 8785 JSON Canonicalization Scheme (JCS) for deterministic serialization. Zero heap allocation.
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
int age = (int)person.Age;                        // 30

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

Most applications need `Corvus.Text.Json` plus either the source generator or the `corvusjson` CLI. Add the feature-specific runtime packages you use.

| Area | Package | Description |
|---|---|---|
| Core runtime | **Corvus.Text.Json** | Core V5 runtime library. Required by all V5 generated types. |
| JSON Schema generation | **Corvus.Text.Json.SourceGenerator** | Roslyn incremental source generator for JSON Schema model generation at build time. |
| JSON Schema generation | **Corvus.Text.Json.CodeGeneration** | Shared JSON Schema code-generation engine for tools and advanced extension scenarios. |
| JSON Schema generation | **Corvus.Json.Cli** | CLI tool (`corvusjson`) for ahead-of-time JSON Schema, OpenAPI, AsyncAPI, and query-language code generation. |
| JSON Schema generation | **Corvus.Json.CodeGenerator** | Immutable-model CLI tool (`generatejsonschematypes`). Delegates to the same engines; defaults to V4. |
| Dynamic validation | **Corvus.Text.Json.Validator** | Dynamically load, compile, and validate JSON against JSON Schema at runtime using Roslyn. |
| Compatibility | **Corvus.Text.Json.Compatibility** | Bridge helpers for interop with V4 `Corvus.Json.ExtendedTypes` and System.Text.Json during migration. |
| Migration | **Corvus.Text.Json.Migration.Analyzers** | Roslyn analyzers and code fixes for migrating V4 `Corvus.Json` code to V5 `Corvus.Text.Json`. |
| OpenAPI | **Corvus.Text.Json.OpenApi** | Runtime abstractions for generated OpenAPI clients and ASP.NET Core server stubs. |
| OpenAPI | **Corvus.Text.Json.OpenApi.HttpTransport** | `HttpClient` transport for generated OpenAPI clients. |
| OpenAPI | **Corvus.Text.Json.OpenApi.CodeGeneration** | Code-generation engine for OpenAPI and related API-generation tooling. |
| OpenAPI | **Corvus.Text.Json.OpenApi30** | Strongly-typed V5 model types for OpenAPI 3.0 documents. |
| OpenAPI | **Corvus.Text.Json.OpenApi31** | Strongly-typed V5 model types for OpenAPI 3.1 documents. |
| OpenAPI | **Corvus.Text.Json.OpenApi32** | Strongly-typed V5 model types for OpenAPI 3.2 documents. |
| AsyncAPI | **Corvus.Text.Json.AsyncApi** | Runtime support for generated AsyncAPI producers, consumers, handlers, and request/reply flows. |
| AsyncAPI | **Corvus.Text.Json.AsyncApi.CodeGeneration** | Code-generation engine for AsyncAPI producer/consumer generation. |
| AsyncAPI | **Corvus.Text.Json.AsyncApi26** | Strongly-typed V5 model types for AsyncAPI 2.6 documents. |
| AsyncAPI | **Corvus.Text.Json.AsyncApi30** | Strongly-typed V5 model types for AsyncAPI 3.0 documents. |
| AsyncAPI transports | **Corvus.Text.Json.AsyncApi.Nats** | NATS transport for generated AsyncAPI applications. |
| AsyncAPI transports | **Corvus.Text.Json.AsyncApi.Kafka** | Kafka transport for generated AsyncAPI applications. |
| AsyncAPI transports | **Corvus.Text.Json.AsyncApi.Amqp** | AMQP 0-9-1/RabbitMQ transport for generated AsyncAPI applications. |
| AsyncAPI transports | **Corvus.Text.Json.AsyncApi.Mqtt** | MQTT 3.1.1/5.0 transport for generated AsyncAPI applications. |
| AsyncAPI transports | **Corvus.Text.Json.AsyncApi.WebSocket** | WebSocket transport for generated AsyncAPI applications. |
| AsyncAPI transports | **Corvus.Text.Json.AsyncApi.AzureServiceBus** | Azure Service Bus transport for generated AsyncAPI applications. |
| AsyncAPI support | **Corvus.Text.Json.AsyncApi.Testing** | In-memory transport for testing generated AsyncAPI producers and consumers. |
| AsyncAPI support | **Corvus.Text.Json.AsyncApi.HealthChecks** | ASP.NET Core health checks for AsyncAPI transports. |
| AsyncAPI support | **Corvus.Text.Json.AsyncApi.Polly** | Polly-based resilience middleware for AsyncAPI handlers/transports. |
| JSONata | **Corvus.Text.Json.Jsonata** | JSONata query and transformation evaluator. |
| JSONata | **Corvus.Text.Json.Jsonata.SourceGenerator** | Roslyn source generator for compile-time JSONata code generation. |
| JSONata | **Corvus.Text.Json.Jsonata.CodeGeneration** | Code-generation engine for JSONata expressions. |
| JMESPath | **Corvus.Text.Json.JMESPath** | JMESPath query-language evaluator. |
| JMESPath | **Corvus.Text.Json.JMESPath.SourceGenerator** | Roslyn source generator for compile-time JMESPath code generation. |
| JMESPath | **Corvus.Text.Json.JMESPath.CodeGeneration** | Code-generation engine for JMESPath expressions. |
| JsonLogic | **Corvus.Text.Json.JsonLogic** | JsonLogic rule engine. |
| JsonLogic | **Corvus.Text.Json.JsonLogic.SourceGenerator** | Roslyn source generator for compile-time JsonLogic code generation. |
| JsonLogic | **Corvus.Text.Json.JsonLogic.CodeGeneration** | Code-generation engine for JsonLogic rules. |
| JSONPath | **Corvus.Text.Json.JsonPath** | JSONPath (RFC 9535) query-language evaluator. |
| JSONPath | **Corvus.Text.Json.JsonPath.SourceGenerator** | Roslyn source generator for compile-time JSONPath code generation. |
| JSONPath | **Corvus.Text.Json.JsonPath.CodeGeneration** | Code-generation engine for JSONPath expressions. |
| JSON Patch | **Corvus.Text.Json.Patch** | RFC 6902 JSON Patch, RFC 7396 Merge Patch, and diff. |
| YAML | **Corvus.Text.Json.Yaml** | YAML 1.2 to JSON converter with Corvus document model integration. |
| YAML | **Corvus.Yaml.SystemTextJson** | YAML 1.2 to JSON converter using only System.Text.Json. |

### Install

```bash
# Core library
dotnet add package Corvus.Text.Json

# CLI code generator
dotnet tool install --global Corvus.Json.Cli
```

For the source generator, add as an analyzer reference:

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.1.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

## Target Frameworks

### V5 engine (Corvus.Text.Json)
- .NET 10.0, 9.0
- .NET Standard 2.0, 2.1

### V4 engine (Corvus.Json)
- .NET 10.0, 9.0, 8.0
- .NET Standard 2.0, 2.1

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
- [OpenAPI Client and Server Generation](docs/OpenApi.md)
- [AsyncAPI Producer and Consumer Generation](docs/AsyncApi.md)
- [JSONata Query & Transformation](docs/Jsonata.md)
- [JMESPath Query Language](docs/JMESPath.md)
- [JSONPath Query Language](docs/JsonPath.md)
- [JsonLogic Rule Engine](docs/JsonLogic.md)
- [JSON Patch, Merge Patch & Diff](docs/JsonPatch.md)
- [JSON Canonicalization (RFC 8785)](docs/JsonCanonicalization.md)
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
- **Up to 8× faster** than [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native) with 90–100% less memory allocation in the runtime evaluator
- **Code-generated evaluation** — an optional source generator and CLI tool produce optimized static C# for expressions known at build time (up to 12× faster)
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
- **Up to 150× faster** than [JmesPath.Net](https://github.com/jdevillard/JmesPath.Net) on common benchmarks with zero allocation in the runtime evaluator
- **Code-generated evaluation** — an optional source generator and CLI tool produce optimized static C# for expressions known at build time
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

## JsonLogic

`Corvus.Text.Json.JsonLogic` implements [JsonLogic](https://jsonlogic.com/) — a standard for expressing business rules as JSON. Rules are portable, storable in databases, and safely evaluated without allowing arbitrary code execution. It supports all standard operators, plus extended numeric types (`BigNumber`) via custom operators.

- **100% conformance** — passes the full [official JsonLogic test suite](https://jsonlogic.com/tests.json)
- **70–98% faster** than [JsonEverything](https://json-everything.net/) across 19 benchmark scenarios with zero or near-zero allocations in the runtime evaluator
- **Code-generated evaluation** — an optional source generator and CLI tool produce optimized static C# for rules known at build time
- **Zero-allocation hot path** — pooled workspace memory with `ArrayPool`-backed evaluation

```csharp
using Corvus.Text.Json.JsonLogic;

JsonElement ruleElement = JsonElement.ParseValue(
    """{"if": [{">":[{"var":"temp"}, 100]}, "too hot", "ok"]}"""u8);
JsonElement data = JsonElement.ParseValue("""{"temp": 110}"""u8);

JsonLogicRule rule = new(ruleElement);
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = JsonLogicEvaluator.Default.Evaluate(rule, data, workspace);
Console.WriteLine(result); // "too hot"
```

See [JsonLogic documentation](docs/JsonLogic.md) for the full API, code generation, and performance benchmarks.

## JSONPath

`Corvus.Text.Json.JsonPath` implements [JSONPath (RFC 9535)](https://www.rfc-editor.org/rfc/rfc9535) — an IETF-standardized query language for extracting values from JSON documents. It supports property access, wildcards, array slicing, filter expressions with comparisons and logical operators, recursive descent, and function extensions.

- **100% conformance** — passes all 723 tests in the official [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite)
- **Faster than JsonEverything** on 5 of 6 benchmark scenarios with 13–16× less memory allocation
- **Code-generated evaluation** — an optional source generator and CLI tool produce optimized static C# for expressions known at build time
- **Zero-allocation hot path** — stack-allocated result buffers with `ArrayPool` overflow

```csharp
using Corvus.Text.Json.JsonPath;

JsonElement result = JsonPathEvaluator.Default.Query(
    "$.store.book[?@.price<10].title",
    JsonElement.ParseValue("""{"store":{"book":[{"title":"A","price":8.95},{"title":"B","price":12.99}]}}"""u8));
// result: ["A"]
```

See [JSONPath documentation](docs/JsonPath.md) for the full API, code generation, and performance benchmarks.

## OpenAPI

`Corvus.Text.Json` can generate strongly-typed HTTP clients and ASP.NET Core server stubs from OpenAPI 3.0, 3.1, and 3.2 specifications. The generated code uses the same V5 JSON Schema engine as the core model generator, so request and response bodies are pooled, strongly typed, and schema-validated.

- **Typed clients** — generated request parameter objects, response result types, and `MatchResult()` dispatch over documented responses.
- **ASP.NET Core servers** — generated handler interfaces and endpoint mapping for minimal APIs.
- **HTTP plumbing included** — path/query/header/cookie parameter serialization, request and response validation, headers, streaming, multipart forms, and binary payloads.
- **Spec models** — strongly-typed packages for OpenAPI 3.0, 3.1, and 3.2 documents.

See [OpenAPI documentation](docs/OpenApi.md) and the OpenAPI ExampleRecipes for client, server, callback, and end-to-end samples.

## AsyncAPI

`Corvus.Text.Json` can generate strongly-typed producers and consumers from AsyncAPI 2.6 and 3.0 specifications. Generated applications use typed payloads and channel parameters, with validation before publishing and when receiving messages.

- **Typed producers and consumers** — generated APIs for publish, subscribe, and request/reply messaging patterns.
- **Broker transports** — runtime packages for NATS, Kafka, AMQP, MQTT, WebSocket, and Azure Service Bus.
- **Runtime policies** — validation modes, error handling, dead-letter routing, and test transports for local/in-memory scenarios.
- **Spec models** — strongly-typed packages for AsyncAPI 2.6 and 3.0 documents.

See [AsyncAPI documentation](docs/AsyncApi.md) and the AsyncAPI ExampleRecipes for producer, consumer, authentication, and end-to-end samples.

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
