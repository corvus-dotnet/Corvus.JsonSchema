---
name: corvus-docs-website
description: >
  Build, serve, and maintain the Corvus.Text.Json documentation website and the
  six Blazor WASM playgrounds (JSON Schema, JSONata, JMESPath, JsonLogic, JSONPath, YAML).
  Covers the 11-step build.ps1 pipeline, generated vs hand-authored file boundaries,
  incremental rebuild patterns, XmlDocToMarkdown API doc generation, SCSS/JS asset
  compilation, and playground startup with Monaco editor.
  USE FOR: building or previewing the docs site, modifying website content or theme,
  updating API documentation, running or modifying playgrounds.
  DO NOT USE FOR: library development (use other skills).
---

# Documentation Website and Playgrounds

## Documentation Website

### Location
Source: `docs/website/site/`
Build output: `docs/website/.output/`

### Building

```powershell
cd docs\website
.\build.ps1
```

The build script runs an 11-step pipeline compiling content, SCSS, taxonomy, and API docs into `.output/`.

### Serving Locally

```powershell
# Preview the built site
cd docs\website
.\preview.ps1
# Or:
.\build.ps1 -ServeOnly
```

### ⚠️ CRITICAL: Stop the Server Before Rebuilding

The build script deletes and recreates `.output/`. On Windows, the Node file server holds file locks that prevent deletion, causing the build to **hang indefinitely**. Always stop the serving process before running `build.ps1`.

### Generated vs Hand-Authored Files

**Auto-generated** (by build.ps1, `.gitignored`):
- `site/theme/corvus/views/api/v5/index.cshtml` and `v4/index.cshtml`
- `site/theme/corvus/views/Shared/_ApiSidebarV5.cshtml` and `V4`
- `site/content/Api-v5/`, `site/content/Api-v4/` (except `namespaces/` and `examples/`)
- `site/taxonomy/api-v5/`, `site/taxonomy/api-v4/`
- `site/content/Docs/`, `site/content/Examples/`
- `site/taxonomy/docs/`, `site/taxonomy/examples/`

**Hand-authored** (committed):
- `site/source/` — copied into target tree by build step 0
- `site/content/GettingStarted/`, `site/content/Home/`
- `site/content/Api-v5/namespaces/`, `site/content/Api-v5/examples/`
- Theme SCSS, JS, layout views

### Incremental Rebuilds

| Changed | Re-run |
|---------|--------|
| SCSS styles | `npx sass theme\corvus\assets\css\scss\main.scss .output\main.css --style=compressed --no-source-map` |
| JavaScript | `Copy-Item theme\corvus\assets\js\*.js .output\` |
| Content markdown | Steps 3-6 of `build.ps1` |
| Library source code | `dotnet build` the library, then regenerate API docs |
| API page templates | Rebuild XmlDocToMarkdown tool + regenerate |

### XmlDocToMarkdown Tool

Located at `docs/website/tools/XmlDocToMarkdown/`. Processes XML doc comments + assemblies into:
- Markdown content files
- Taxonomy YAML
- Razor views
- Per-type HTML pages

Supports multi-assembly input, versioned output with engine switcher, and per-version search indices.

## Playgrounds

Six Blazor WASM playgrounds with Monaco editor integration:

| Playground | Directory | Port |
|-----------|-----------|------|
| JSON Schema | `docs/playground/` | 5281 |
| JSONata | `docs/playground-jsonata/` | 5280 |
| JMESPath | `docs/playground-jmespath/` | — |
| JsonLogic | `docs/playground-jsonlogic/` | — |
| JSONPath | `docs/playground-jsonpath/` | — |
| YAML | `docs/playground-yaml/` | — |

### Running a Playground

```powershell
# 1. Build the JS bundle (only after changing JS/Monaco assets)
cd docs\playground-jsonata
npm ci
npm run bundle

# 2. Start the Blazor WASM dev server on a fixed port
$env:ASPNETCORE_URLS = "http://127.0.0.1:5280"
dotnet run --project src\Corvus.Text.Json.Jsonata.Playground\Corvus.Text.Json.Jsonata.Playground.csproj
```

**Use `ASPNETCORE_URLS` env var** to pin the port — `--urls` flag does not work with the WASM app host.

### SR.Format WASM Bug

`SR.Format` does not work correctly in Blazor WASM because `System.Resources.UseSystemResourceKeys` returns `true`, causing string.Join fallback. The `EvaluationService.FixBrokenSRFormat()` method compensates. All exception messages displayed to the user must go through this method.

## Cross-References
- For building the library (prerequisite for API docs), see `corvus-build-and-test`
- See `docs/website/DEVELOPMENT.md` for the full development guide
