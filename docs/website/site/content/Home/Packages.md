---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "NuGet Packages"
---
### Corvus.Text.Json

Core runtime library. Required by all generated types. See the [Getting Started](/getting-started/index.html) guide.

```bash
dotnet add package Corvus.Text.Json
```

### Corvus.Text.Json.SourceGenerator

Roslyn incremental source generator. Generates C# from JSON Schema at build time. Add as an analyzer reference. See the [Source Generator](/docs/source-generator.html) guide.

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

### Corvus.Json.Cli

CLI tool (`corvusjson`) for ahead-of-time code generation. Produces the same output as the source generator, for CI pipelines and pre-generation workflows. See the [CLI Code Generation](/docs/code-generator.html) guide.

```bash
dotnet tool install --global Corvus.Json.Cli
```

### Corvus.Json.CodeGenerator (Legacy)

Legacy CLI tool (`generatejsonschematypes`). Delegates to the same engine as `corvusjson` but defaults to the V4 engine. New projects should use `Corvus.Json.Cli` above.

### Corvus.Text.Json.Validator

Library for dynamically loading, compiling, and validating JSON against JSON Schema at runtime using Roslyn. Ideal for schema registries, configuration validation, and user-supplied schemas. See the [Runtime Schema Validation](/docs/validator.html) guide.

```bash
dotnet add package Corvus.Text.Json.Validator
```

### Corvus.Text.Json.Patch

JSON Patch (RFC 6902), Merge Patch (RFC 7396), and diff. Apply, construct, and compute patches on mutable documents with zero allocation. See the [JSON Patch, Merge Patch & Diff](/docs/json-patch.html) guide.

```bash
dotnet add package Corvus.Text.Json.Patch
```

### Corvus.Text.Json.JsonPath

JSONPath (RFC 9535) query language evaluator. Extract values from JSON documents with property access, wildcards, filters, recursive descent, and function extensions. See the [JSONPath Query Language](/docs/json-path.html) guide.

```bash
dotnet add package Corvus.Text.Json.JsonPath
```

### Corvus.Text.Json.JsonPath.SourceGenerator

Roslyn source generator for compile-time JSONPath code generation. Produces optimized static C# from `.jsonpath` expression files. See the [JSONPath Query Language](/docs/json-path.html) guide.

```xml
<PackageReference Include="Corvus.Text.Json.JsonPath.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```
