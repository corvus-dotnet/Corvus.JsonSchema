# Corvus.Json.CodeGenerator (Legacy)

> **This package has been renamed.** The CLI tool is now `corvusjson`, available in the **`Corvus.Json.Cli`** package. This package (`Corvus.Json.CodeGenerator`) provides the legacy `generatejsonschematypes` command as a shim that delegates to the same code generation engine.
>
> **New projects should install `Corvus.Json.Cli` instead:**
> ```bash
> dotnet tool install --global Corvus.Json.Cli
> ```

## Legacy shim behaviour

When you run `generatejsonschematypes`, it delegates to the same underlying implementation as `corvusjson`. The key difference is the **default engine**:

| Tool | Package | Default engine |
|---|---|---|
| `corvusjson` | `Corvus.Json.Cli` | **V5** (Corvus.Text.Json) |
| `generatejsonschematypes` | `Corvus.Json.CodeGenerator` | **V4** (Corvus.Json.ExtendedTypes) |

To generate V5 types with the legacy command, pass `--engine V5` explicitly.

## Migration

Replace your tool installation:

```bash
# Old
dotnet tool install --global Corvus.Json.CodeGenerator

# New
dotnet tool install --global Corvus.Json.Cli
```

Update your commands — schema generation is now a subcommand:

```bash
# Old
generatejsonschematypes person.json --rootNamespace MyApp.Models --outputPath Generated/

# New
corvusjson jsonschema person.json --rootNamespace MyApp.Models --outputPath Generated/
```

Other subcommands (`config`, `validateDocument`, `listNameHeuristics`, `jsonata`, `jmespath`, `jsonlogic`) just need the tool name changed:

```bash
# Old
generatejsonschematypes config myconfig.json
generatejsonschematypes validateDocument schema.json data.json

# New
corvusjson config myconfig.json
corvusjson validateDocument schema.json data.json
```

## Documentation

See the [CLI Code Generation guide](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/CodeGenerator.md) for the full reference.

## License

Apache 2.0 — see [LICENSE](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/LICENSE).
