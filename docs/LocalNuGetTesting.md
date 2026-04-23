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
