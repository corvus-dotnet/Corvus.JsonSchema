# Documentation Code Sample Catalog

The file `docs/code-sample-catalog.yaml` is an inventory of every fenced code block across the repository's documentation, skills, and instruction files. It tracks line ranges, languages, categories, and whether each C# sample has been verified to compile.

## Why?

Documentation code samples rot. APIs change, namespaces are renamed, parameters are added — but the markdown snippets silently fall behind. The catalog makes this visible: every C# block has a `verified` flag, and the maintenance script detects when files have changed.

## Quick start — the common case

Most of the time you only need to work with the files you just edited:

```powershell
# 0. Build the ExampleRecipes projects first (if working with ExampleRecipes)
dotnet build docs\ExampleRecipes\ExampleRecipes.slnx

# 1. After editing a doc file, refresh its catalog entries
.\docs\update-code-sample-catalog.ps1 -UpdateFile docs\JsonPath.md

# 2. Verify your C# samples compile (see "Verifying samples" below)

# 3. Set verified: true for confirmed blocks in the YAML

# 4. Before committing, check the catalog is in sync
.\docs\update-code-sample-catalog.ps1 -Check
```

That's it for day-to-day work. The full-scan modes below are for bulk operations only.

## Script modes

| Mode | Command | When to use |
|------|---------|-------------|
| **Default** | `.\docs\update-code-sample-catalog.ps1` | Full re-scan of all files, preserving annotations |
| **Single file** | `.\docs\update-code-sample-catalog.ps1 -UpdateFile <path>` | After editing one doc file |
| **Check** | `.\docs\update-code-sample-catalog.ps1 -Check` | Pre-commit gate — exits with code 1 if catalog is stale |
| **Generate** | `.\docs\update-code-sample-catalog.ps1 -Generate` | Fresh catalog from scratch (resets all annotations) |
| **Stats** | `.\docs\update-code-sample-catalog.ps1 -Stats` | Print summary (files, blocks, C# count, verified count) |

Paths are relative to the repository root, using backslashes on Windows:

```powershell
.\docs\update-code-sample-catalog.ps1 -UpdateFile docs\ExampleRecipes\003-Mutation\README.md
.\docs\update-code-sample-catalog.ps1 -UpdateFile .github\copilot-instructions.md
```

## Catalog structure

The YAML file is organised into sections:

| Section | Covers |
|---------|--------|
| `example-recipes` | `docs/ExampleRecipes/*/README.md` |
| `main-docs` | `docs/*.md` (top-level documentation) |
| `v4-docs` | `docs/V4/*.md` |
| `copilot-docs` | `docs/copilot/*.md` |
| `copilot-instructions` | `.github/copilot-instructions.md` |
| `skills` | `.github/skills/*/SKILL.md` |

Each file entry lists its blocks with line ranges, language, and (for C# blocks) category and verification status:

```yaml
- file: docs/JsonPath.md
  blocks:
    - index: 0
      language: csharp
      lines: [15, 28]
      category: compilable
      verified: true
    - index: 1
      language: json
      lines: [32, 45]
    - index: 2
      language: csharp
      lines: [50, 55]
      category: fragment
      verified: false
```

## Block categories

| Category | Meaning | Verification |
|----------|---------|-------------|
| `compilable` | Complete or wrappable C# that must compile | **Yes** — verify it compiles |
| `fragment` | Partial snippet (single expression, ellipsis, pseudo-code) | No |
| `v4-before` | V4 code in migration "before/after" comparisons | No |
| `bad-pattern` | Intentionally incorrect code (❌ examples, analyzer docs) | No |
| *(absent)* | Non-C# block (JSON, YAML, bash, XML, etc.) | No |

New C# blocks default to `category: compilable, verified: false`. Change the category manually in the YAML if the block is a fragment or intentionally incorrect.

## Verifying samples

### Step 0: Build existing projects first

Before verifying any markdown code blocks, build the ExampleRecipes projects. This confirms the real code compiles and gives you a reference for cross-checking README samples:

```powershell
dotnet build docs\ExampleRecipes\ExampleRecipes.slnx
```

If any project fails, fix it before moving on — the README code blocks are derived from these projects.

### ExampleRecipes READMEs

Cross-reference each README code block against its companion `.cs` file in the same directory. The `.cs` file is the source of truth — update the README to match, not the other way around.

### Standalone doc samples

For C# blocks in `docs/*.md`, skills, or instructions, use .NET 10 file-based apps for quick verification. Create a `.cs` file **outside any project directory** (to avoid `.csproj` conflicts) with `#:project` directives pointing to the library projects:

```csharp
// D:\temp\doc-verify\test.cs  (NOT inside the repo tree)
#:project D:\source\corvus-dotnet\Corvus.JsonSchema\src\Corvus.Text.Json\Corvus.Text.Json.csproj
#:property NoWarn=CS8500;JSON001;CS8600

using Corvus.Text.Json;

// paste the code block here

string json = """{"name":"Alice"}""";
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
Console.WriteLine(doc.RootElement.GetProperty("name"u8).GetString());
```

Build (does not run the code):

```powershell
dotnet build D:\temp\doc-verify\test.cs
```

For blocks referencing additional packages (Jsonata, JMESPath, JsonPath, Patch, etc.), add extra `#:project` directives.

### Automated triage

The script `docs/triage-code-samples.ps1` performs **heuristic** categorization by analyzing block content — it detects V4 namespace markers, fragment patterns, and cross-references ExampleRecipes blocks against companion `.cs` files. It does **not** verify compilation.

```powershell
.\docs\triage-code-samples.ps1 -DryRun                  # Preview changes
.\docs\triage-code-samples.ps1                           # Apply heuristic categories
.\docs\triage-code-samples.ps1 -Section example-recipes  # Single section
.\docs\triage-code-samples.ps1 -File docs\JsonPath.md    # Single file
```

Blocks the triage script marks `verified: true` are only cross-referenced against companion `.cs` files (text matching), not compiled. Always compile-verify `compilable` blocks separately before trusting `verified: true`.

### First-time full verification

When verifying the entire catalog from scratch:

1. `dotnet build docs\ExampleRecipes\ExampleRecipes.slnx`
2. `.\docs\update-code-sample-catalog.ps1` — full catalog refresh
3. `.\docs\triage-code-samples.ps1` — heuristic categorization
4. Compile-verify remaining `compilable` blocks (file-based apps for standalone docs)
5. `.\docs\update-code-sample-catalog.ps1 -Check` — confirm sync

### After verification

Set `verified: true` for the block in `code-sample-catalog.yaml`. The `-UpdateFile` script preserves this flag as long as the block content hasn't moved.

## Common doc patterns to avoid

- **`ParsedJsonDocument<T>.Parse("""..."""u8)`** — fails to compile. `Parse` takes `ReadOnlyMemory<byte>`, not `ReadOnlySpan<byte>` (from the `u8` suffix). Remove `u8` or use `ParseValue` for the span overload.
- **Promoting `ParseValue` over `Parse`** — documentation samples should show `ParsedJsonDocument<T>.Parse(...)` with `using` to promote pooled-memory best practice. `ParseValue` creates non-disposable copies. Reserve `ParseValue` for inline contexts where `Parse` is impractical (e.g., small constants in dictionary initializers).
- **`PatchBuilder.Add`/`Replace` with `ParseValue`** — use implicit `JsonElement.Source` conversions: `.Add("/name"u8, "Alice")`, `.Replace("/version"u8, 2)`.


## Annotation preservation

When the script re-scans a file (via `-UpdateFile` or a full update), it matches blocks by `(file_path, block_index, language)`:

- **Same language, same line range** → preserves both `category` and `verified`
- **Same language, different line range** → preserves `category`, resets `verified` to `false`
- **Language mismatch or new block** → defaults to `compilable` / `false`

If you insert a new code block before existing blocks, their indices shift. Running `-UpdateFile` will detect the language mismatch at the shifted positions and reset annotations. Re-verify the affected blocks after bulk structural changes.

## CI integration (optional)

The `-Check` flag is designed as a pre-commit or CI gate:

```powershell
.\docs\update-code-sample-catalog.ps1 -Check
if ($LASTEXITCODE -ne 0) {
    Write-Error "Code sample catalog is out of date. Run update-code-sample-catalog.ps1 -UpdateFile for modified files."
    exit 1
}
```

This catches forgotten catalog updates but does not verify compilation — it only checks that the catalog's block counts and line ranges match the files on disk.
