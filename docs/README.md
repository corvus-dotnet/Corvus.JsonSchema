# Corvus.Text.Json Documentation

Full documentation is available on the **[Corvus.Text.Json documentation website](../docs/website/.output/index.html)**.

To build and preview the website locally:

```powershell
cd docs/website
./preview.ps1
```

Then open http://localhost:5000 in your browser.

## Source Documentation

The markdown files in this directory are the source content for the website. They are read by `build.ps1` during the website build process.

| File | Website Section |
|---|---|
| [ParsedJsonDocument.md](./ParsedJsonDocument.md) | Docs — Parsing & Reading JSON |
| [JsonDocumentBuilder.md](./JsonDocumentBuilder.md) | Docs — Building & Mutating JSON |
| [SourceGenerator.md](./SourceGenerator.md) | Docs — Source Generator |
| [CodeGenerator.md](./CodeGenerator.md) | Docs — CLI Code Generation |
| [Validator.md](./Validator.md) | Docs — Dynamic Schema Validation |
| [MigratingFromV4ToV5.md](./MigratingFromV4ToV5.md) | Docs — Migrating from V4 |
| [UsingCopilotForMigration.md](./UsingCopilotForMigration.md) | Docs — Copilot Migration |

### Other Content

- **[ExampleRecipes/](./ExampleRecipes/)** — Source for the website's Examples section (17 recipe walkthroughs)
- **[copilot/](./copilot/)** — Copilot migration instructions referenced by the migration guide
- **[V4/](./V4/)** — Legacy V4 documentation (for reference during migration back to Corvus.JsonSchema)
- **[website/](./website/)** — The documentation website build pipeline and theme
