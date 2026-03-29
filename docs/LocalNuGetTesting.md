# Local NuGet Package Testing

This guide explains how to build, pack, and test NuGet packages locally before publishing. This is useful when iterating on the migration analyzers, source generator, or any other package in the solution.

## Prerequisites

Create a local NuGet feed directory. The repository's `nuget.config` is already configured to use `D:\source\local-nuget-feed`:

```powershell
New-Item -ItemType Directory -Path D:\source\local-nuget-feed -Force
```

Both the root `nuget.config` and the `docs/V4MigrationExample/nuget.config` reference this local feed **above** nuget.org, so locally packed versions take priority.

## Packing a project

Use a `-local.N` version suffix to distinguish local iterations:

```powershell
dotnet pack src\Corvus.Text.Json.Migration.Analyzers `
    -c Release `
    -o D:\source\local-nuget-feed `
    /p:Version=1.0.0-local.1
```

Increment the number (`local.2`, `local.3`, ...) each time you repack.

## Clearing the NuGet cache

NuGet caches restored packages per version. If you repack with the **same** version string, you must clear the cached copy before restoring again:

```powershell
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\corvus.text.json.migration.analyzers"
```

Replace the package name with whichever package you are iterating on.

## Consuming the local package

Reference the local version in your test project's `.csproj`:

```xml
<PackageReference Include="Corvus.Text.Json.Migration.Analyzers"
                  Version="1.0.0-local.1"
                  PrivateAssets="all" />
```

Then restore and build:

```powershell
dotnet restore docs\V4MigrationExample --force
dotnet build docs\V4MigrationExample --no-incremental
```

The `--force` flag ensures NuGet re-evaluates sources. The `--no-incremental` flag ensures the compiler reloads analyzers.

## Typical iteration loop

```powershell
# 1. Make changes to the analyzer/package source

# 2. Pack with a new version number
dotnet pack src\Corvus.Text.Json.Migration.Analyzers -c Release `
    -o D:\source\local-nuget-feed /p:Version=1.0.0-local.2

# 3. Clear the cached copy
Remove-Item -Recurse -Force "$env:USERPROFILE\.nuget\packages\corvus.text.json.migration.analyzers"

# 4. Update the version in the consuming project's .csproj

# 5. Restore and rebuild
dotnet restore docs\V4MigrationExample --force
dotnet build docs\V4MigrationExample --no-incremental
```

## Applies to any package in the solution

The same workflow works for any project that produces a NuGet package — just substitute the project path and package name. For example, to test the source generator locally:

```powershell
dotnet pack src\Corvus.Text.Json.SourceGenerator `
    -c Release `
    -o D:\source\local-nuget-feed `
    /p:Version=5.0.0-local.1
```
