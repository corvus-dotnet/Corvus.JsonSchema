# Corvus.Text.Json Documentation

Full documentation is available on the **[Corvus.Text.Json documentation website](https://corvus-oss.org/Corvus.JsonSchema)**.

To build and preview the website locally:

```powershell
cd docs/website
./preview.ps1
```

Then open http://localhost:5000 in your browser.

## Source Documentation

The markdown files in this directory are the source content for the website. They are read by `build.ps1` during the website build process, driven by the `doc-descriptors/` YAML files.

| File | Website Section |
|---|---|
| [ParsedJsonDocument.md](./ParsedJsonDocument.md) | Docs — Parsing & Reading JSON |
| [JsonDocumentBuilder.md](./JsonDocumentBuilder.md) | Docs — Building & Mutating JSON |
| [SourceGenerator.md](./SourceGenerator.md) | Docs — Source Generator |
| [CodeGenerator.md](./CodeGenerator.md) | Docs — CLI Code Generation |
| [Validator.md](./Validator.md) | Docs — Dynamic Schema Validation |
| [MigratingFromV4ToV5.md](./MigratingFromV4ToV5.md) | Docs — Migrating from V4 |
| [UsingCopilotForMigration.md](./UsingCopilotForMigration.md) | Docs — Copilot Migration |
| [MigrationAnalyzers.md](./MigrationAnalyzers.md) | Docs — Migration Analyzers |
| [Analyzers.md](./Analyzers.md) | Docs — Analyzers |
| [SchemaEvaluator.md](./SchemaEvaluator.md) | Docs — Standalone Schema Evaluator |

### Developer & Contributor Docs

| File | Description |
|---|---|
| [AddingKeywords.md](./AddingKeywords.md) | How to add JSON Schema keywords to the code generation system |
| [AnnotationSystem.md](./AnnotationSystem.md) | JSON Schema annotation collection architecture |
| [BenchmarkGuide.md](./BenchmarkGuide.md) | Running and maintaining benchmarks |
| [CodeGenerationPatternDiscovery.md](./CodeGenerationPatternDiscovery.md) | How the code generator discovers common schema patterns |
| [LocalNuGetTesting.md](./LocalNuGetTesting.md) | Building and testing NuGet packages locally |
| [NumericTypes.md](./NumericTypes.md) | Numeric type system and precision model |
| [ReleaseProcess.md](./ReleaseProcess.md) | Versioning, tagging, and NuGet publishing |
| [RunningTests.md](./RunningTests.md) | Test projects and how to run them |
| [StandaloneEvaluatorInternals.md](./StandaloneEvaluatorInternals.md) | Standalone evaluator architecture and implementation |
| [ValidationHandlerGuide.md](./ValidationHandlerGuide.md) | How validation handlers emit C# from schema keywords |

### Other Content

- **[ExampleRecipes/](./ExampleRecipes/)** — Source for the website's Examples section (17 recipe walkthroughs)
- **[copilot/](./copilot/)** — Copilot migration instructions referenced by the migration guide
- **[V4/](./V4/)** — Legacy V4 documentation (for reference during migration back to Corvus.JsonSchema)
- **[website/](./website/)** — The documentation website build pipeline and theme
