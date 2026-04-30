---
name: corvus-bowtie-testing
description: >
  Test Corvus.JsonSchema against the JSON Schema Test Suite using Bowtie, the
  cross-implementation meta-validator. Covers local package building, configuring a
  local Bowtie checkout to use locally-built packages, running the test suite via
  Docker/Podman containers, interpreting results, the iteration loop, and teardown.
  Both V4 (dotnet-corvus-jsonschema-v4engine) and V5 (dotnet-corvus-jsonschema-v5engine)
  implementations are supported.
  USE FOR: running Bowtie conformance suites against local changes, setting up the
  local development loop, interpreting Bowtie failure reports, testing schema dialect
  compliance (Draft 4 through 2020-12).
  DO NOT USE FOR: running the in-repo xUnit test suite (use corvus-build-and-test),
  regenerating test classes from the submodule (use corvus-test-suite-regeneration).
---

# Bowtie Testing

## Overview

[Bowtie](https://github.com/bowtie-json-schema/bowtie) is a meta-validator that runs
the official JSON Schema Test Suite against multiple implementations. Corvus.JsonSchema
has two Bowtie harnesses:

| Implementation | Package |
|---------------|---------|
| `dotnet-corvus-jsonschema-v4engine` | `Corvus.Json.Validator` |
| `dotnet-corvus-jsonschema-v5engine` | `Corvus.Text.Json.Validator` |

Live results: [bowtie.report](https://bowtie.report/#/implementations/dotnet-corvus-jsonschema)

## Prerequisites

- A local clone of [bowtie](https://github.com/bowtie-json-schema/bowtie)
- Podman or Docker on `PATH`
- Python 3.13+ with [uv](https://docs.astral.sh/uv/) (or pip)

### Setting up the Bowtie venv

Always run bowtie from the local checkout's venv — **not** a system-wide install.
The checkout may contain fixes not yet released.

```powershell
cd D:\source\bowtie
python -m venv .venv
.\.venv\Scripts\pip.exe install -e .
$bowtie = "D:\source\bowtie\.venv\Scripts\bowtie.exe"
```

## Full Workflow

### 1. Build local packages

```powershell
.\build-local-packages.ps1
# Creates local-packages/ with all .nupkg files at version 5.0.0-local.1
```

To avoid NuGet cache hits after changes, increment the version:

```powershell
.\build-local-packages.ps1 -Version 5.0.0-local.2
```

### 2. Configure Bowtie for local development

```powershell
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie
```

This script:
1. Builds local packages (or reuses existing ones)
2. Copies `.nupkg` files into each Bowtie implementation directory
3. Creates a `NuGet.config` pointing at the local feed
4. Patches the `.csproj` to reference the local package version
5. Patches the `Dockerfile` and `.dockerignore`
6. Builds the container image via Podman/Docker

### 3. Run the test suite

```powershell
cd D:\source\bowtie
$bowtie = ".\.venv\Scripts\bowtie.exe"

# Run a specific draft against the V5 engine
& $bowtie suite -i localhost/dotnet-corvus-jsonschema-v5engine 2020-12 `
    | & $bowtie summary --show failures

# Run against V4 engine
& $bowtie suite -i localhost/dotnet-corvus-jsonschema-v4engine 2020-12 `
    | & $bowtie summary --show failures
```

The `localhost/` prefix tells Bowtie to use the locally-built image instead of pulling
from the registry.

### 4. Teardown

Revert all Bowtie directory changes:

```powershell
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Teardown
```

This restores original `Dockerfile`, `.csproj`, `.dockerignore` from backups and removes
copied packages and `NuGet.config`.

## Iteration Loop

```powershell
$bowtie = "D:\source\bowtie\.venv\Scripts\bowtie.exe"

# 1. Make changes to library source

# 2. Teardown previous config
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Teardown

# 3. Rebuild and reconfigure with a new version
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Version 5.0.0-local.2

# 4. Run suite
cd D:\source\bowtie
& $bowtie suite -i localhost/dotnet-corvus-jsonschema-v5engine 2020-12 `
    | & $bowtie summary --show failures
```

## Local Package Building (standalone)

For testing packages in consuming projects (not just Bowtie):

```powershell
# Pack everything
.\build-local-packages.ps1

# Pack a single project (faster)
dotnet pack src\Corvus.Text.Json.Migration.Analyzers `
    -c Release -o local-packages /p:Version=5.0.0-local.1

# Clear NuGet cache for a specific package
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\corvus.text.json"
```

The consuming project needs a `nuget.config` with the local feed:

```xml
<configuration>
  <packageSources>
    <add key="local" value="C:\full\path\to\Corvus.JsonSchema\local-packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## Common Pitfalls

- **NuGet cache**: NuGet caches packages by version. Always increment the version suffix (`-local.2`, `-local.3`) or clear the cache (`Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\<package-name>"`) when repacking.
- **System bowtie**: Don't use a system-installed `bowtie` — always use the venv from the local checkout. It may contain critical fixes.
- **Container rebuild**: The configure script builds the container image, but if you need to force a rebuild, delete the image first: `podman rmi localhost/dotnet-corvus-jsonschema-v5engine`.
- **Repo nuget.config**: Don't modify the repo's own `nuget.config` (it uses `<clear />` for CI isolation). Add the local source to the consuming project's config.

## Cross-References
- For in-repo test suite execution, see `corvus-build-and-test`
- For regenerating test classes from the submodule, see `corvus-test-suite-regeneration`
- Full guide: `docs/LocalNuGetTesting.md`
