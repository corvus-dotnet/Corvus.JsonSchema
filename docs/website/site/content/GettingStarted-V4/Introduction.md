---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-29T00:00:00.0+00:00
Title: "Introduction"
---
## Introduction — V4 Engine

This hands-on tutorial walks you through JSON Schema-based code generation with the **V4 engine** (`Corvus.Json.ExtendedTypes` and `Corvus.Json.JsonSchema.TypeGeneratorTool`). It builds on `System.Text.Json` to provide rich serialization, deserialization, composition, and validation support.

> **Looking for the V5 engine?** The V5 engine (`Corvus.Text.Json`) offers source-generator integration, pooled-memory parsing, mutable documents, and improved performance. See the [V5 Getting Started guide](/getting-started/index.html).

### What you will learn

- Generate C# code from JSON Schema using the `corvusjson` CLI tool (with `--engine V4` for V4 output)
- Serialize and deserialize JSON documents with the generated types
- Validate JSON documents against schema
- Navigate JSON documents — including additional properties, arrays, and union types
- Create new JSON documents using factory methods and composition
- Use pattern matching with `oneOf`/`anyOf` union types

### Prerequisites

- [.NET 8 SDK or later](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (or Visual Studio 2022)
- A shell with developer tools in the path (PowerShell, Terminal, etc.)
- A text editor or IDE (VS Code, Visual Studio, etc.)

> **Note:** .NET 8.0 support on the V4 engine will be dropped in November 2026 when .NET 8 reaches end-of-life.

Some familiarity with C#, [JSON Schema](https://json-schema.org/understanding-json-schema/), and `System.Text.Json` will help, but is not required.