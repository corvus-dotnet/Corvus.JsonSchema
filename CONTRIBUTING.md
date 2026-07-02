# Contributing to Corvus.JsonSchema

Thank you for your interest in contributing! This guide covers the practical details you need to submit a successful pull request.

## Getting started

### Prerequisites

- .NET 10.0 SDK (primary target) and .NET 9.0 SDK (multi-targeting)
- Git (with submodule support)
- PowerShell 7+. The repository's build and code-generation scripts are cross-platform PowerShell (`.ps1`), so `pwsh` is required on every platform.
- Node.js 20 LTS or later, with npm. This is needed for the TypeScript code-generation engine: its model and OpenAPI-client example recipes under `docs/typescript/`, and the `@endjin/corvus-json-runtime` and `@endjin/corvus-json-client-runtime` packages under `packages/`. TypeScript itself is a dev dependency that `npm install` restores per package, so there is no separate global install.

The .NET-only workflow (build and test the solution) needs the first three. Node.js is required only when you touch the TypeScript engine, its runtime packages, or the TypeScript docs recipes.

#### Installing the prerequisites on Windows

Using [winget](https://learn.microsoft.com/windows/package-manager/winget/) (included with Windows 10 and 11):

```powershell
winget install Microsoft.DotNet.SDK.10
winget install Microsoft.DotNet.SDK.9
winget install Microsoft.PowerShell
winget install OpenJS.NodeJS.LTS
winget install Git.Git
```

Restart the terminal afterwards so the updated `PATH` is picked up.

#### Installing the prerequisites on Linux

The commands below target Ubuntu and Debian. For other distributions, see the official install guides for [.NET](https://learn.microsoft.com/dotnet/core/install/linux), [PowerShell](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-linux), and [Node.js](https://nodejs.org/en/download/package-manager).

```bash
# .NET 10 and 9 SDKs, plus Git
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0 dotnet-sdk-9.0 git

# PowerShell 7 (the classic snap works on any snap-enabled distribution)
sudo snap install powershell --classic

# Node.js 20 LTS (NodeSource)
curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
sudo apt-get install -y nodejs
```

If `dotnet-sdk-10.0` is not yet in your distribution's feed, use the distribution-agnostic install script instead. Run `curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0` (then repeat with `--channel 9.0`) and add `~/.dotnet` to your `PATH`.

### Clone and build

```powershell
git clone --recurse-submodules https://github.com/corvus-dotnet/Corvus.JsonSchema.git
cd Corvus.JsonSchema
dotnet build Corvus.Text.Json.slnx
```

The `--recurse-submodules` flag is required to fetch the `JSON-Schema-Test-Suite/` submodule used by the test suite.

### Run tests

```powershell
dotnet test --solution Corvus.Text.Json.slnx --filter "TestCategory!=failing&TestCategory!=outerloop"
```

Always exclude the `failing` and `outerloop` categories:
- **`failing`** — known failures tracked for future fixes
- **`outerloop`** — memory stress tests that are too resource-intensive for normal CI

Tests run in parallel across matrix jobs in CI. The documentation website builds in a separate post-compile job, also in parallel with tests.

### Local package testing

To test changes as NuGet packages in a consuming project, run the `build-local-packages.ps1` script from the repository root:

```powershell
.\build-local-packages.ps1
```

This packs all V4 and V5 projects into a `local-packages/` folder (git-ignored). See [docs/LocalNuGetTesting.md](docs/LocalNuGetTesting.md) for the full guide, including how to configure a consuming project's `nuget.config` and iterate efficiently.

## Branch conventions

| Branch | Purpose |
|--------|---------|
| `main` | Stable release branch (auto-tags via GitVersion) |
| `dependabot/*` | Automated dependency updates |

Target your PRs at `main`.

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
- `JSON001`, `CS8500`, `IDE0065`, `IDE0290`

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
- Tests must pass with `TestCategory!=failing&TestCategory!=outerloop`
- Use `JsonReaderHelper.TranscodeHelper()` to convert UTF-8 output to strings in assertions
- Follow the existing `stackalloc`/`ArrayPool` pattern for buffer-heavy tests

## XML documentation

All public APIs in library projects must have complete XML doc comments. `CS1591` (missing XML comment) is treated as an error. Use `<inheritdoc />` where the base interface documentation is sufficient.

## String resources

Use `SR.ExceptionMessageName` for all user-facing exception messages. Define new strings in the project's `.resx` file, not as inline string literals.

## Pull request checklist

- [ ] Code compiles with no warnings (`TreatWarningsAsErrors=true`)
- [ ] Tests pass: `dotnet test --filter "TestCategory!=failing&TestCategory!=outerloop"`
- [ ] New public APIs have XML doc comments
- [ ] New source files are listed in `.csproj` (no glob includes)
- [ ] Exception messages use `SR.*` resource strings
- [ ] Follows existing code patterns (partial classes, Common/ linking, etc.)

## Questions?

Open an issue for discussion before starting large changes. This helps avoid duplicate work and ensures your approach aligns with the project direction.