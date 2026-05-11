# Cross-Platform Dynamic Compilation

## Overview

Several test fixtures and the `Corvus.Text.Json.Validator` package dynamically compile C# code at runtime using Roslyn (`Microsoft.CodeAnalysis.CSharp`). The CI pipeline builds on Ubuntu Linux, caches the build artifacts, and then runs the test matrix across multiple platforms:

- **Ubuntu** — `net10.0`
- **Windows** — `net10.0` and `net481`

This means the `net481` tests on Windows run with build artifacts (including `.deps.json` and the NuGet package cache) that were produced on Linux. This cross-OS scenario creates a unique challenge for resolving metadata references for dynamic Roslyn compilation.

## Affected Sites

Six locations in the codebase perform dynamic Roslyn compilation:

| Location | Context |
|----------|---------|
| `src/Corvus.Text.Json.Validator/.../DynamicCompiler.cs` | Production: runtime schema validation |
| `tests/Corvus.Text.Json.JMESPath.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test: JMESPath code-gen conformance |
| `tests/Corvus.Text.Json.Jsonata.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test: JSONata code-gen conformance |
| `tests/Corvus.Text.Json.JsonLogic.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test: JsonLogic code-gen conformance |
| `tests/Corvus.Text.Json.JsonPath.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test: JSONPath code-gen conformance |
| `tests-v4/Corvus.Json.Specs.Tests/Drivers/JsonSchemaBuilderDriver.cs` | Test: V4 schema builder conformance |

A seventh set of sites — the `SourceGeneratorDiagnosticTests.cs` files in the JMESPath, Jsonata, and JsonLogic source generator test projects — also compile dynamically, but these only run on `net10.0` where they use `TRUSTED_PLATFORM_ASSEMBLIES` (an `AppContext` property) and are not affected by the cross-OS issue.

## The Problem

### How `DependencyContext` works

On .NET Framework (`net481`), the standard approach for discovering metadata references for Roslyn compilation is:

1. Call `DependencyContext.Load(assembly)` to read the `.deps.json` file
2. Iterate `CompileLibraries` and call `ResolveReferencePaths()` on each
3. Create `MetadataReference` objects from the resolved file paths

`ResolveReferencePaths()` uses a chain of resolvers:
- **`AppBaseCompilationAssemblyResolver`** — looks in the application's base directory
- **`ReferenceAssemblyPathResolver`** — looks in .NET Framework reference assembly directories
- **`PackageCompilationAssemblyResolver`** — looks in the NuGet global packages folder

### What goes wrong cross-OS

When the build ran on Linux, the `.deps.json` contains Linux file paths for the NuGet package cache (e.g., `/home/runner/.nuget/packages/...`). On the Windows test runner:

- **NuGet packages that were output-copied** (DLLs in `bin/Release/net481/`) resolve successfully via `AppBaseCompilationAssemblyResolver`
- **Framework assemblies** (`mscorlib`, `System`, `netstandard`) are **not** in the output directory — on a native Windows build they come from the GAC or reference assembly directories
- **NuGet-cached reference assemblies** from the `Microsoft.NETFramework.ReferenceAssemblies.net481` package may or may not be available, depending on whether the NuGet cache was restored

This creates two distinct failure modes:

#### Failure Mode 1: Partial Resolution

DependencyContext resolves *some* libraries (the ones in the output directory) but not framework assemblies. The result is a reference list that has NuGet packages but is missing `mscorlib`, `netstandard`, etc. Compilation fails with:

```
error CS0012: The type 'ValueType' is defined in an assembly that is not referenced.
    You must add a reference to assembly 'netstandard, Version=2.0.0.0'
```

#### Failure Mode 2: Facade Resolution

The NuGet cache is restored on the Windows runner, and DependencyContext resolves `mscorlib.dll` from the `Microsoft.NETFramework.ReferenceAssemblies.net481` NuGet package. But this is a **reference/facade assembly** (`mscorlib, Version=2.0.0.0`), not the real runtime assembly from the GAC (`mscorlib, Version=4.0.0.0`). Compilation fails with:

```
error CS1705: Assembly 'System.Text.Json' with identity '...Version=10.0.0.7...'
    uses 'mscorlib, Version=4.0.0.0' which has a higher version than referenced
    assembly 'mscorlib' with identity 'mscorlib, Version=2.0.0.0'
```

## The Solution

All six sites use the same three-phase reference resolution strategy:

### Phase 1: DependencyContext (preserving NuGet package refs and defines)

```csharp
DependencyContext? ctx = DependencyContext.Load(Assembly.GetExecutingAssembly())
    ?? DependencyContext.Default;

if (ctx is not null)
{
    references = (
        from l in ctx.CompileLibraries
        from r in TryResolveReferencePaths(l)
        select (MetadataReference)MetadataReference.CreateFromFile(r)).ToList();

    defines.AddRange(ctx.CompilationOptions.Defines.Where(d => d is not null)!);
}
```

This preserves whatever DependencyContext can resolve — crucially including NuGet packages that provide newer APIs (e.g., `System.Runtime.CompilerServices.Unsafe` with `MethodImplOptions.AggressiveOptimization`) and compilation defines (`NETFRAMEWORK`, `NET481`) that guard target-framework-specific code.

### Phase 2: Always supplement with AppDomain assemblies (replacing facades)

```csharp
// Always supplement — even when DependencyContext resolves framework assemblies,
// they may be facades that cause CS1705 version mismatch.
SupplementWithDirectoryAndAppDomain(references);
```

The supplement method **always** runs — there is no gate checking whether framework assemblies were already resolved. This is critical because DependencyContext may resolve *facade* versions that look correct by name but are wrong by version.

Within the supplement, **AppDomain assemblies replace any DependencyContext ref with the same name**:

```csharp
foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
{
    if (!a.IsDynamic && !string.IsNullOrEmpty(a.Location))
    {
        string name = a.GetName().Name
            ?? Path.GetFileNameWithoutExtension(a.Location);

        if (!seenNames.Add(name))
        {
            // Name already exists from DependencyContext — replace it.
            // The AppDomain version is the real runtime assembly (e.g.
            // mscorlib 4.0.0.0 from the GAC), not a facade.
            references.RemoveAll(r =>
                r is PortableExecutableReference peRef &&
                peRef.FilePath is string p &&
                Path.GetFileNameWithoutExtension(p)
                    .Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        references.Add(MetadataReference.CreateFromFile(a.Location));
    }
}
```

This ensures the real GAC `mscorlib` (version 4.0.0.0) replaces any facade (version 2.0.0.0), while NuGet packages that DependencyContext resolved but haven't been loaded into the AppDomain are preserved.

### Phase 3: Directory scan and transitive resolution

After AppDomain assemblies, two more steps fill remaining gaps:

1. **Output directory scan** — enumerates `*.dll` files in `AppDomain.CurrentDomain.BaseDirectory`, validates each is a managed assembly via `AssemblyName.GetAssemblyName()` (skipping native DLLs like coverage instrumentation), and adds any not yet seen.

2. **Transitive resolution** — iterates `GetReferencedAssemblies()` on all loaded assemblies and calls `Assembly.Load()` for each. This catches assemblies like `netstandard.dll` that are in the GAC but not directly loaded into the AppDomain at startup and not present in the output directory.

### Why "always supplement" instead of conditional

An earlier approach checked `hasFrameworkAssembly` — if any assembly named `mscorlib`, `System.Runtime`, or `netstandard` existed in the DependencyContext refs, it skipped supplementing. This failed because:

- On CI, the NuGet cache was restored, so DependencyContext resolved `mscorlib.dll` from the `Microsoft.NETFramework.ReferenceAssemblies` package — a **facade** with version 2.0.0.0
- The check saw "mscorlib" and concluded framework assemblies were present
- The supplement was skipped, leaving the facade in place
- Compilation failed with CS1705 version mismatch

The unconditional supplement with AppDomain-wins-duplicates semantics handles all cases:
- **Normal build** (same OS): DependencyContext resolves everything correctly, AppDomain assemblies match, replacements are no-ops
- **Cross-OS, partial resolution**: DependencyContext resolves NuGet packages, AppDomain adds framework assemblies, transitive resolution fills `netstandard`
- **Cross-OS, facade resolution**: DependencyContext resolves facade `mscorlib 2.0.0.0`, AppDomain **replaces** it with the real `mscorlib 4.0.0.0`

### Additional considerations

- **`TryResolveReferencePaths`** wraps `ResolveReferencePaths()` in a try/catch for `InvalidOperationException`. Some reference entries (e.g., `System.ValueTuple.Reference`) are unresolvable when framework reference assemblies are missing. These types are in-box in the target framework and reachable through other references.

- **Native DLL filtering** — the directory scan uses `AssemblyName.GetAssemblyName()` to validate each DLL is managed before adding it as a metadata reference. Native DLLs (e.g., `msdia140.dll`, coverage instrumentation DLLs) pass `MetadataReference.CreateFromFile()` but cause `CS0009 "PE image doesn't contain managed metadata"` at compile time.

- **DynamicCompiler specifics** — the production `DynamicCompiler` in `Corvus.Text.Json.Validator` has additional complexity: a `hostAssembly` parameter, per-host-assembly caching of metadata references, and on .NET 8+ a collectible `AssemblyLoadContext` for loading compiled assemblies without leaking memory.

## Testing the Cross-OS Scenario Locally

To reproduce the CI cross-OS scenario on a Windows machine with WSL:

```powershell
# 1. Build in WSL (Linux) — produces Linux-path deps.json
wsl -d Ubuntu-22.04 -- bash -c "
  cd /mnt/d/source/corvus-dotnet/Corvus.JsonSchema &&
  dotnet restore --force Corvus.Text.Json.slnx &&
  dotnet build Corvus.Text.Json.slnx -c Release"

# 2. Run net481 tests on Windows with the Linux-built artifacts
dotnet test --project tests\Corvus.Text.Json.JMESPath.CodeGeneration.Tests `
  -f net481 -c Release --filter "TestCategory!=outerloop" --no-build
```

Note: `dotnet restore --force` is needed in WSL because the `project.assets.json` files contain OS-specific paths. The `--no-build` flag on Windows ensures the Linux build artifacts are used as-is.

All four CodeGeneration test projects and the V4 Specs tests should be verified this way before pushing changes to the dynamic compilation reference resolution logic.
