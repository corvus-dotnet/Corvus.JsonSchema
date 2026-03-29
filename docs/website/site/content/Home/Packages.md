---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "NuGet Packages"
---
### Corvus.Text.Json

Core runtime library. Required by all generated types.

```bash
dotnet add package Corvus.Text.Json
```

### Corvus.Text.Json.SourceGenerator

Roslyn incremental source generator. Generates C# from JSON Schema at build time. Add as an analyzer reference.

```xml
<PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
</PackageReference>
```

### Corvus.Json.CodeGenerator

CLI tool for ahead-of-time code generation. Produces the same output as the source generator, for CI pipelines and pre-generation workflows. See the [CLI Code Generation](/docs/code-generator.html) guide.

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

### Corvus.Text.Json.Validator

Library for dynamically loading, compiling, and validating JSON against JSON Schema at runtime using Roslyn. Ideal for schema registries, configuration validation, and user-supplied schemas. See the [Runtime Schema Validation](/docs/validator.html) guide.

```bash
dotnet add package Corvus.Text.Json.Validator
```
