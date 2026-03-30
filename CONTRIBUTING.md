# Contributing to Corvus.JsonSchema

Thank you for your interest in contributing! This guide covers the practical details you need to submit a successful pull request.

## Getting started

### Prerequisites

- .NET 10.0 SDK (primary target)
- .NET 8.0 SDK (multi-targeting)
- Git (with submodule support)
- PowerShell 7+

### Clone and build

```powershell
git clone --recurse-submodules https://github.com/corvus-dotnet/Corvus.JsonSchema.git
cd Corvus.JsonSchema
dotnet build Corvus.Text.Json.slnx
```

The `--recurse-submodules` flag is required to fetch the `JSON-Schema-Test-Suite/` submodule used by the test suite.

### Run tests

```powershell
dotnet test Corvus.Text.Json.slnx --filter "category!=failing&category!=outerloop"
```

Always exclude the `failing` and `outerloop` categories:
- **`failing`** — known failures tracked for future fixes
- **`outerloop`** — memory stress tests that are too resource-intensive for normal CI

Tests run sequentially (`-m:1`) in CI to avoid memory pressure on GitHub Actions runners.

## Branch conventions

| Branch | Purpose |
|--------|---------|
| `main` | Stable release branch (auto-tags via GitVersion) |
| `feature/v5` | Active V5 development branch |
| `dependabot/*` | Automated dependency updates |

Target your PRs at `feature/v5` for V5 work, or `main` for hotfixes.

## Code style

The repository uses `.editorconfig` to enforce formatting. Key rules:

- **Indentation:** 4 spaces (2 spaces for XML/project files)
- **Braces:** Allman style (`csharp_new_line_before_open_brace = all`)
- **Final newline:** No final newline in `.cs` files (`insert_final_newline = false`)
- **Final newline:** Yes for all other files (`insert_final_newline = true`)
- **Language version:** `preview` — new C# features are encouraged
- **Nullable annotations:** Enabled in all library projects
- **`TreatWarningsAsErrors=true`** — the build fails on any warning

### Suppressions

These warnings are suppressed project-wide and should not be re-suppressed with `#pragma`:
- `JSON001`, `xUnit1031`, `xUnit2013`, `CS8500`, `IDE0065`, `IDE0290`

### StyleCop

SA rules are enforced. Notably, `SA1124` (Do not use regions) is an error — do not use `#region` directives.

## Project structure conventions

### No glob includes

Every source file must be explicitly listed in the `.csproj` file:

```xml
<Compile Include="MyNewFile.cs" />
```

Adding a new `.cs` file requires a corresponding `<Compile>` entry. The build does not use wildcard includes.

### Partial-class organisation

Large types are split across files by concern:

```
JsonElement.cs              — Core struct
JsonElement.Parse.cs        — Parsing
JsonElement.JsonSchema.cs   — Schema validation
JsonElement.Mutable.cs      — Mutable operations
```

Follow this pattern: keep the core file untouched and add a new `TypeName.<Concern>.cs` file.

### Shared source via Common/

Files in `Common/src/` and `Common/tests/` are shared across projects via MSBuild links:

```xml
<Compile Include="$(CommonPath)Corvus\Text\Json\SharedHelper.cs" Link="Shared\SharedHelper.cs" />
```

Do not duplicate shared files — link them instead.

### Source generator linking

Files in `Corvus.Text.Json.CodeGeneration` that are also needed by the Roslyn source generator must be listed in **both** `.csproj` files. The source generator project links them via `$(CorvusTextJsonCodeGenerationPath)`.

## Test requirements

- All new functionality must have tests
- Tests must pass with `category!=failing&category!=outerloop`
- Use `JsonReaderHelper.TranscodeHelper()` to convert UTF-8 output to strings in assertions
- Follow the existing `stackalloc`/`ArrayPool` pattern for buffer-heavy tests

## XML documentation

All public APIs in library projects must have complete XML doc comments. `CS1591` (missing XML comment) is treated as an error. Use `<inheritdoc />` where the base interface documentation is sufficient.

## String resources

Use `SR.ExceptionMessageName` for all user-facing exception messages. Define new strings in the project's `.resx` file, not as inline string literals.

## Pull request checklist

- [ ] Code compiles with no warnings (`TreatWarningsAsErrors=true`)
- [ ] Tests pass: `dotnet test --filter "category!=failing&category!=outerloop"`
- [ ] New public APIs have XML doc comments
- [ ] New source files are listed in `.csproj` (no glob includes)
- [ ] Exception messages use `SR.*` resource strings
- [ ] Follows existing code patterns (partial classes, Common/ linking, etc.)

## Questions?

Open an issue for discussion before starting large changes. This helps avoid duplicate work and ensures your approach aligns with the project direction.