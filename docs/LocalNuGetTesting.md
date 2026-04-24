# Local NuGet Package Testing

This guide explains how to build, pack, and test NuGet packages locally before publishing. This is useful when iterating on any package in the solution — the source generator, migration analyzers, runtime library, or anything else.

## Quick start

The `build-local-packages.ps1` script in the repository root packs **every** project (V4 and V5) into a single local feed folder:

```powershell
.\build-local-packages.ps1
```

This creates a `local-packages/` directory at the repo root containing all `.nupkg` files at version `5.0.0-local.1`.

### Incrementing the version

NuGet caches packages by version. If you repack after making changes, increment the suffix:

```powershell
.\build-local-packages.ps1 -Version 5.0.0-local.2
```

Alternatively, clear the cached copy for the specific package you changed:

```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\corvus.text.json"
```

### Custom output directory

```powershell
.\build-local-packages.ps1 -OutputDir C:\feeds\local
```

## Consuming local packages

The consuming project needs a `nuget.config` that includes the local feed. Add (or create) a `nuget.config` next to your `.csproj` or solution:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="local" value="C:\full\path\to\Corvus.JsonSchema\local-packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

> **Note:** The repository's own `nuget.config` uses `<clear />` for CI isolation and should not be modified. Add the local source to the *consuming* project's config instead.

Then reference the local version in your `.csproj`:

```xml
<PackageReference Include="Corvus.Text.Json" Version="5.0.0-local.1" />
```

And restore with `--force` to ensure NuGet re-evaluates sources:

```powershell
dotnet restore --force
dotnet build --no-incremental
```

## Packing a single project

If you only need to iterate on one package (faster than packing everything), use `dotnet pack` directly:

```powershell
dotnet pack src\Corvus.Text.Json.Migration.Analyzers `
    -c Release `
    -o local-packages `
    /p:Version=5.0.0-local.1
```

## Typical iteration loop

```powershell
# 1. Make changes to the package source

# 2. Pack (either all packages or just the one you changed)
.\build-local-packages.ps1 -Version 5.0.0-local.2

# 3. Clear the NuGet cache for the changed package
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\corvus.text.json.migration.analyzers"

# 4. Update the version in the consuming project's .csproj

# 5. Restore and rebuild the consuming project
dotnet restore --force
dotnet build --no-incremental
```

The `--force` flag ensures NuGet re-evaluates sources. The `--no-incremental` flag ensures the compiler reloads analyzers and source generators.

## Testing with Bowtie

[Bowtie](https://github.com/bowtie-json-schema/bowtie) is a meta-validator that runs JSON Schema test suites against multiple implementations, including our `dotnet-corvus-jsonschema` harness. The `configure-bowtie-for-local-development.ps1` script automates configuring a local bowtie checkout to build and test against locally-built V4 packages.

### Prerequisites

- A local clone of the [bowtie](https://github.com/bowtie-json-schema/bowtie) repository
- Podman or Docker available on `PATH`
- Python 3.13+ and [uv](https://docs.astral.sh/uv/) (or pip) installed

For detailed installation instructions, see [Bowtie Prerequisites (Windows)](BowtiePrerequisites.md).

#### Setting up the bowtie venv

You must run bowtie from the local checkout's virtual environment — **not** a system-wide install. The checkout may contain fixes (e.g. Windows encoding) that have not yet been released.

```powershell
cd D:\source\bowtie
python -m venv .venv
.\.venv\Scripts\pip.exe install -e .
```

All `bowtie` commands below assume you are using the venv binary:

```powershell
$bowtie = "D:\source\bowtie\.venv\Scripts\bowtie.exe"
```

> **Tip:** You can activate the venv instead (`D:\source\bowtie\.venv\Scripts\Activate.ps1`) so that plain `bowtie` resolves to the checkout version.

### Setup

```powershell
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie
```

This will:

1. Build local packages (or reuse existing ones at the right version)
2. Copy V4 `.nupkg` files into the bowtie implementation directory
3. Create a `NuGet.config` for the Docker build
4. Patch the `.csproj` to reference the local package version
5. Patch the `Dockerfile` and `.dockerignore` to include the local packages
6. Remove any cached container image so bowtie rebuilds from the patched Dockerfile

You can specify a custom version:

```powershell
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Version 5.0.0-local.2
```

### Running the suite

After setup, run the bowtie test suite from the bowtie repo using the **`localhost/`** image name (this tells bowtie to use the locally-built image rather than pulling from the registry):

```powershell
cd D:\source\bowtie
# Use the venv bowtie — not the system install
$bowtie = ".\.venv\Scripts\bowtie.exe"
& $bowtie suite -i localhost/dotnet-corvus-jsonschema 2020-12 | & $bowtie summary --show failures
```

### Teardown

To revert all changes and restore the original files:

```powershell
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Teardown
```

This restores the original `Dockerfile`, `.csproj`, and `.dockerignore` from backups, and removes the copied packages and `NuGet.config`.

### Iteration loop

```powershell
$bowtie = "D:\source\bowtie\.venv\Scripts\bowtie.exe"

# 1. Make changes to library source

# 2. Teardown previous config (increments version to bust caches)
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Teardown

# 3. Rebuild and reconfigure with a new version
.\configure-bowtie-for-local-development.ps1 -BowtiePath D:\source\bowtie -Version 5.0.0-local.2

# 4. Run suite (note: localhost/ prefix uses the locally-built image)
cd D:\source\bowtie
& $bowtie suite -i localhost/dotnet-corvus-jsonschema 2020-12 | & $bowtie summary --show failures
```
