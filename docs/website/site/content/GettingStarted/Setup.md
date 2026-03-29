---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Project Setup"
---
## Source generator (recommended)

Add the source generator and runtime packages to your `.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.SourceGenerator" Version="5.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  <PackageReference Include="Corvus.Text.Json" Version="5.0.0" />
</ItemGroup>
```

Register your JSON Schema files as `AdditionalFiles`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Schemas\person.json" />
</ItemGroup>
```

Then declare a `partial struct` annotated with `JsonSchemaTypeGenerator`:

```csharp
using Corvus.Text.Json;

namespace MyApp.Models;

[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;
```

Build the project. The generator produces the full implementation, deriving the **namespace**, **accessibility**, and **type name** from this declaration.

## CLI tool

If you prefer ahead-of-time code generation, install the `generatejsonschematypes` .NET tool globally:

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

Generate code from a schema:

```bash
generatejsonschematypes \
  --rootNamespace MyApp.Models \
  --outputPath Generated/ \
  Schemas/person.json
```

The CLI tool and the source generator share the same code generation engine and produce identical output. Choose the CLI tool when you need to generate code outside of the build process, or when integrating with non-MSBuild workflows.
