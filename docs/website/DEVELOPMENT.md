# Website Development Guide

This document explains how the Corvus.Text.Json documentation website is built, how to make changes, and how the build pipeline works.

## Prerequisites

- .NET 10+ SDK
- Node.js (for SCSS compilation and search index)
- Vellum SSG (installed automatically by `build.ps1`)

Install Node dependencies once:

```powershell
cd docs/website
npm install
```

## Quick Start

### Full build (from scratch)

```powershell
cd docs/website
./build.ps1
```

This runs all 10 pipeline steps (see [Build Pipeline](#build-pipeline) below) and produces a complete site in `.output/`.

### Integrated build (via root build.ps1)

```powershell
# From the repo root — builds the .NET solution then the website
./build.ps1 -Website
```

This runs the standard .NET build (compile, test, package) and then builds the website in the `PostBuild` phase, reusing the already-compiled binaries. The website build skips steps 1a/1b since the solution is already built.

For GitHub Pages subpath hosting:

```powershell
./build.ps1 -Website -BasePathPrefix "/Corvus.Text.Json"
```

### Skip .NET compilation (website only)

```powershell
cd docs/website
./build.ps1 -SkipDotNetBuild
```

Skips the V5 and V4 .NET builds (steps 1a/1b), assuming the binaries are already in the `bin/Release` directories from a prior `dotnet build`. Useful when iterating on website content after building the solution once.

### Preview with local server

```powershell
./preview.ps1
```

This runs `build.ps1 -Preview`, which builds everything then starts a local server.

### Serve existing output (no rebuild)

```powershell
./build.ps1 -ServeOnly
```

Starts the Vellum preview server on existing `.output/` without rebuilding anything. Useful when you've already done a full build and just want to view the site.

### Incremental rebuilds (during development)

For day-to-day iteration you usually don't need to re-run the full pipeline. Run only the steps affected by your change:

| What you changed | What to re-run |
|---|---|
| SCSS styles | [Step 7](#step-7-compile-scss) only |
| JavaScript files | Copy JS to `.output/` (see [Step 7 note](#step-7-compile-scss)) |
| API page layout/templates | [Steps 8](#step-8-generate-per-type-api-html-pages) |
| XmlDocToMarkdown tool code | Rebuild tool + [Step 8](#step-8-generate-per-type-api-html-pages) |
| Library source code | [Steps 1, 2, 8](#step-1-build-corvustextjson) |
| Content markdown (non-API) | [Steps 3–6](#step-3-generate-recipe-content) as applicable |
| Recipe source docs | [Steps 3, 6](#step-3-generate-recipe-content) |
| Taxonomy YAML | [Step 6](#step-6-run-vellum) |
| Razor views | [Step 6](#step-6-run-vellum) |

#### Common incremental commands

**Rebuild API docs only** (after changing XmlDocToMarkdown tool or wanting fresh API pages):

```powershell
cd docs/website
$xml = "..\..\src\Corvus.Text.Json\bin\Release\net10.0\Corvus.Text.Json.xml"
$dll = "..\..\src\Corvus.Text.Json\bin\Release\net10.0\Corvus.Text.Json.dll"

# Rebuild the tool
dotnet build tools\XmlDocToMarkdown\XmlDocToMarkdown.csproj -c Debug

# Regenerate HTML pages (this also regenerates namespace markdown, taxonomy, and views)
dotnet run --project tools\XmlDocToMarkdown -- `
    --xml $xml --assembly $dll `
    --output content\Api `
    --taxonomy-output taxonomy\api `
    --api-views-dir theme\corvus\views\api `
    --html-output .output\api `
    --site-title "Corvus.Text.Json"
```

**Rebuild CSS only** (after SCSS changes):

```powershell
npx sass theme\corvus\assets\css\scss\main.scss .output\main.css --style=compressed --no-source-map
```

**Copy JS only** (after JavaScript changes):

```powershell
Copy-Item theme\corvus\assets\js\*.js .output\
```

**Rebuild the library** (after source code changes — needed before API doc regeneration):

```powershell
dotnet build ..\..\src\Corvus.Text.Json\Corvus.Text.Json.csproj -c Release -f net10.0
```

## Build Pipeline

`build.ps1` runs these steps in order:

### Step 1a: Build Corvus.Text.Json (V5)

```powershell
dotnet build src/Corvus.Text.Json/Corvus.Text.Json.csproj -c Release -f net10.0
```

Compiles the V5 library, generating XML documentation and assembly.

### Step 1b: Build V4 libraries

Builds 8 core V4 libraries (Release, net10.0 + netstandard2.0):

- Corvus.Json.ExtendedTypes, JsonReference, Patch, Validator
- Corvus.Json.CodeGeneration, CodeGeneration.CSharp, CodeGeneration.CSharp.QuickStart
- Corvus.Json.CodeGeneration.HttpClientDocumentResolver

> **Note:** The 7 JsonSchema dialect libraries (Draft4/6/7/201909/202012/OpenApi30/31) are excluded from docs because they contain thousands of generated types with repetitive patterns (~25K pages), making the build impractically slow.

### Step 2a: Generate V5 API markdown, taxonomy & views

Runs `XmlDocToMarkdown` for the V5 library with `--api-base-url /api/v5`, `--version-label "V5 Engine"`, and related version switcher params. Generates:
- Namespace-level markdown in `content/Api-v5/`
- Taxonomy YAML in `taxonomy/api-v5/`
- Razor views (index + sidebar) in `theme/corvus/views/api/v5/`
- Per-version search index at `content/Api-v5/search-index.json`

### Step 2b: Generate V4 API markdown, taxonomy & views

Same as 2a but for all 8 V4 libraries (multi-assembly mode with multiple `--xml`/`--assembly` pairs), output to `Api-v4/` and `api-v4/` paths with `--version-label "V4 Engine"`.

### Step 3: Generate recipe content

Scans `docs/ExampleRecipes/` for numbered recipe directories. Generates content, taxonomy, and views for each recipe.

### Step 4: Generate docs content

Processes selected markdown files from `docs/` into content, taxonomy, and views.

### Step 5: Install Vellum

Downloads and installs Vellum SSG if not already present in `.endjin/`.

### Step 6: Run Vellum

Renders the site from content + taxonomy + views into `.output/`.

### Step 7: Compile SCSS

```powershell
npx sass theme/corvus/assets/css/scss/main.scss .output/main.css --style=compressed --no-source-map
```

### Step 8: Build search index

```powershell
node tools/build-search-index.js --output .output/search-index.json
```

Builds a site-wide Lunr search index. Per-version API search indices are generated in Step 2a/2b.

## Directory Structure

```
docs/website/
├── build.ps1              # Full build pipeline script
├── preview.ps1            # Runs build.ps1 -Preview
├── site.yml               # Vellum site configuration
├── package.json           # Node dependencies (sass, vellum, js-yaml)
│
├── content/               # Markdown content (input to Vellum)
│   ├── Api-v5/            # Generated — V5 namespace index pages + search-index.json
│   ├── Api-v4/            # Generated — V4 namespace index pages + search-index.json
│   ├── Docs/              # Generated — from docs/*.md
│   ├── Examples/          # Generated — from docs/ExampleRecipes/
│   ├── GettingStarted/    # Hand-authored getting started guide
│   └── Home/              # Hand-authored homepage content
│
├── taxonomy/              # Vellum taxonomy (navigation, metadata, content blocks)
│   ├── api/               # Hand-authored — version selector landing page
│   ├── api-v5/            # Generated — V5 per-namespace and per-type entries
│   ├── api-v4/            # Generated — V4 per-namespace and per-type entries
│   ├── docs/              # Generated — per-doc entries
│   └── examples/          # Generated — per-recipe entries
│
├── theme/corvus/          # Site theme
│   ├── assets/
│   │   ├── css/scss/      # SCSS source (main.scss entry point)
│   │   └── js/            # JavaScript (search, sidebar, mobile nav)
│   └── views/             # Razor views (.cshtml templates)
│       ├── Shared/        # Layout, partials (_Layout.cshtml, _ApiSidebar*.cshtml)
│       ├── api/           # Version selector landing page (index.cshtml)
│       │   ├── v5/        # V5 API views (index.cshtml + api-page.cshtml)
│       │   └── v4/        # V4 API views (index.cshtml + api-page.cshtml)
│       ├── docs/          # Generated — per-doc views
│       └── examples/      # Generated — per-recipe views
│
├── tools/
│   ├── XmlDocToMarkdown/  # .NET tool: XML docs + assembly → markdown + views
│   ├── build-search-index.js  # Node script for Lunr search index
│   └── PdbDiag.csx       # Diagnostic script for PDB inspection
│
├── .endjin/               # Vellum SSG binary (gitignored)
├── .output/               # Build output (gitignored)
└── node_modules/          # Node dependencies (gitignored)
```

## XmlDocToMarkdown Tool

The core tool that generates API documentation. It supports multi-assembly input (for V4's 8 libraries) and versioned output with version switcher integration.

### Key CLI arguments

| Argument | Purpose |
|---|---|
| `--xml <path>` | Path to XML documentation file (repeatable for multi-assembly) |
| `--assembly <path>` | Path to compiled assembly DLL (repeatable, paired with `--xml`) |
| `--ns20-assembly <path>` | Path to netstandard2.0 assembly (repeatable, for API surface comparison) |
| `--output <dir>` | Output directory for namespace markdown |
| `--taxonomy-output <dir>` | Output directory for taxonomy YAML |
| `--api-views-dir <dir>` | Output directory for Razor views |
| `--shared-views-dir <dir>` | Output directory for shared partials (sidebar) |
| `--ns-descriptions <dir>` | Directory containing hand-authored namespace description markdown |
| `--api-base-url <url>` | Base URL prefix for links (e.g. `/api/v5`) |
| `--sidebar-partial-name <name>` | Sidebar partial filename (e.g. `_ApiSidebarV5`) |
| `--layout-path <path>` | Relative path to layout from views dir |
| `--version-label <text>` | Current version label (e.g. `"V5 Engine"`) |
| `--alt-version-label <text>` | Alternate version label (e.g. `"V4 Engine"`) |
| `--alt-version-url <url>` | URL to switch to alternate version |
| `--repo-url <url>` | Override repository URL for source links |

### Version switcher

When `--version-label`, `--alt-version-label`, and `--alt-version-url` are all provided, the generated `index.cshtml` includes:
- A version bar showing the current version with a link to switch
- A `data-search-index` attribute on the search input pointing to the per-version search index
- A localStorage script that saves the user's version preference (`corvus-api-version`)

The `/api/index.html` landing page reads this localStorage value and redirects to the preferred version (defaulting to V5).

## Making Changes

### Adding a new content section

1. Create markdown files in `content/YourSection/`
2. Create taxonomy YAML in `taxonomy/yoursection/`
3. Create Razor views in `theme/corvus/views/yoursection/`
4. Add navigation entries to the taxonomy index
5. Run Steps 6–7 to rebuild

### Modifying API page layout

1. Edit files in `tools/XmlDocToMarkdown/` (usually `HtmlPageGenerator.cs` or `MarkdownGenerator.cs`)
2. Rebuild the tool: `dotnet build tools\XmlDocToMarkdown\XmlDocToMarkdown.csproj -c Debug`
3. Regenerate API pages (the incremental command from [above](#common-incremental-commands))

### Changing styles

1. Edit SCSS files in `theme/corvus/assets/css/scss/`
2. The entry point is `main.scss`; API-specific styles are in `pages/_api.scss`
3. Recompile: `npx sass theme\corvus\assets\css\scss\main.scss .output\main.css --style=compressed --no-source-map`
4. Refresh browser

### Adding a new recipe

1. Create a numbered directory in `docs/ExampleRecipes/` (e.g. `10-MyRecipe/`)
2. Add a `README.md` with a `# JSON Schema Patterns in .NET - My Recipe Title` heading
3. Run `build.ps1` or Steps 3 + 6 + 7

### Modifying source link resolution

1. Edit `tools/XmlDocToMarkdown/SourceLinkResolver.cs`
2. Use `tools/PdbDiag.csx` to inspect PDB metadata during debugging
3. Rebuild and regenerate API pages
