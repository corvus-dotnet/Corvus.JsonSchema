# Corvus.Json.Cli

`Corvus.Json.Cli` installs the `corvusjson` command-line tool for generating C# code from JSON Schema and query-language expressions.

## Install

```bash
dotnet tool install --global Corvus.Json.Cli
```

## JSON Schema generation

Generate strongly typed C# from a JSON Schema:

```bash
corvusjson jsonschema person.json --rootNamespace MyApp.Models --outputPath Generated/
```

By default, `corvusjson jsonschema` uses the V5 engine and generates `Corvus.Text.Json` types. Use `--engine V4` when you explicitly need the immutable `Corvus.Json.ExtendedTypes` model.

Common options:

| Option | Description |
|---|---|
| `--rootNamespace` | Namespace for generated types. |
| `--outputPath` | Directory for generated `.cs` files. |
| `--outputRootTypeName` | Name for the root generated type. |
| `--engine` | Code generation engine: `V5` or `V4`. |
| `--codeGenerationMode` | `TypeGeneration`, `SchemaEvaluationOnly`, or `Both`. |
| `--assertFormat` | Enables format assertion. Defaults to `true`. |

## Other commands

The tool also includes commands for configuration-driven generation, validation, and query-language code generation:

```bash
corvusjson config myconfig.json
corvusjson validateDocument schema.json data.json
corvusjson jsonata expression.jsonata --rootNamespace MyApp.Expressions --outputPath Generated/
corvusjson jmespath expression.jmespath --rootNamespace MyApp.Expressions --outputPath Generated/
corvusjson jsonlogic rule.json --rootNamespace MyApp.Rules --outputPath Generated/
```

## Legacy package

The older `Corvus.Json.CodeGenerator` package still provides the `generatejsonschematypes` command as a compatibility shim. New projects should install this package and use `corvusjson`.

## Documentation

See the [CLI Code Generation guide](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/CodeGenerator.md) for the full reference.

## License

Apache 2.0 - see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
