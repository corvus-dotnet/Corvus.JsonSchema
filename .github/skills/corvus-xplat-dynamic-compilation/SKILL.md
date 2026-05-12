---
name: corvus-xplat-dynamic-compilation
description: >
  Resolve Roslyn metadata references for dynamic compilation in cross-OS CI scenarios
  (build on Ubuntu, test on Windows net481). Covers the three-phase reference resolution
  pattern (DependencyContext → AppDomain replacement → directory scan + transitive),
  the facade vs GAC mscorlib problem, all six affected sites, and local WSL testing.
  USE FOR: modifying any code that calls CSharpCompilation.Create with dynamically
  resolved MetadataReferences, adding new dynamic compilation sites, debugging CS0012
  or CS1705 errors in CodeGenConformanceFixture or DynamicCompiler tests.
  DO NOT USE FOR: source generator development (use corvus-codegen), Roslyn analyzer
  authoring (use corvus-analyzers).
---

# Cross-Platform Dynamic Roslyn Compilation

## Background

The CI pipeline builds once on Ubuntu Linux, caches the build artifacts, and runs a test matrix across Ubuntu (net10.0) and Windows (net10.0 + net481). Six sites in the codebase dynamically compile C# via Roslyn at test time. On net481, these need correct metadata references to framework assemblies (mscorlib, netstandard) that live in the GAC on Windows but are not in the Linux-built output directory.

See `docs/CrossPlatformDynamicCompilation.md` for the full writeup including failure mode descriptions and error examples.

## Affected Sites

| File | Context |
|------|---------|
| `src/Corvus.Text.Json.Validator/.../DynamicCompiler.cs` | Production: runtime schema validation |
| `tests/Corvus.Text.Json.JMESPath.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test fixture |
| `tests/Corvus.Text.Json.Jsonata.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test fixture |
| `tests/Corvus.Text.Json.JsonLogic.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test fixture |
| `tests/Corvus.Text.Json.JsonPath.CodeGeneration.Tests/CodeGenConformanceFixture.cs` | Test fixture |
| `tests-v4/Corvus.Json.Specs.Tests/Drivers/JsonSchemaBuilderDriver.cs` | V4 test driver |

Three `SourceGeneratorDiagnosticTests.cs` files also compile dynamically but only run on net10.0 using `TRUSTED_PLATFORM_ASSEMBLIES` — they are not affected.

## The Three-Phase Pattern

All six sites MUST follow this exact pattern. Do not deviate.

### Phase 1: DependencyContext

```csharp
DependencyContext? ctx = DependencyContext.Load(Assembly.GetExecutingAssembly())
    ?? DependencyContext.Default;

List<MetadataReference> references;
List<string> defines = ["DYNAMIC_BUILD"];

if (ctx is not null)
{
    references = (
        from l in ctx.CompileLibraries
        from r in TryResolveReferencePaths(l)
        select (MetadataReference)MetadataReference.CreateFromFile(r)).ToList();

    // CRITICAL: always capture defines — they include NETFRAMEWORK, NET481, etc.
    defines.AddRange(ctx.CompilationOptions.Defines.Where(d => d is not null)!);
}
else
{
    references = [];
}
```

Key points:
- `TryResolveReferencePaths` wraps `ResolveReferencePaths()` in try/catch for `InvalidOperationException`
- **Always capture defines** from `ctx.CompilationOptions.Defines` — they gate `#if NETFRAMEWORK` code
- DependencyContext may resolve NuGet packages with newer APIs (e.g., `AggressiveOptimization`) — these MUST be preserved

### Phase 2: Always supplement (AppDomain replaces facades)

```csharp
// ALWAYS call — no conditional gate
SupplementWithDirectoryAndAppDomain(references);
```

**CRITICAL: No `hasFrameworkAssembly` check.** An earlier approach checked whether mscorlib/System.Runtime/netstandard was already in the refs and skipped supplementing if so. This failed because DependencyContext can resolve a **facade** mscorlib (version 2.0.0.0) from the NuGet reference assemblies package, which passes the name check but causes CS1705 version mismatch at compile time.

Within `SupplementWithDirectoryAndAppDomain`, AppDomain assemblies **replace** any existing ref with the same name:

```csharp
foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
{
    if (!a.IsDynamic && !string.IsNullOrEmpty(a.Location))
    {
        string name = a.GetName().Name
            ?? Path.GetFileNameWithoutExtension(a.Location);

        if (!seenNames.Add(name))
        {
            // Replace DependencyContext ref — AppDomain has the real
            // GAC assembly (mscorlib 4.0.0.0), not a facade (2.0.0.0)
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

The key insight: `seenNames.Add(name)` returns `false` when the name already exists from DependencyContext. In that case we **remove** the old ref and add the AppDomain version. For new names (framework assemblies not resolved by DependencyContext), we just add them.

### Phase 3: Directory scan + transitive resolution

After AppDomain assemblies:

1. **Directory scan** — enumerate `*.dll` in `AppDomain.CurrentDomain.BaseDirectory`, validate each with `AssemblyName.GetAssemblyName()` (skips native DLLs), add if not yet seen.

2. **Transitive resolution** — iterate `GetReferencedAssemblies()` on all loaded assemblies, `Assembly.Load()` each, add if not yet seen. This catches `netstandard.dll` (a facade in the GAC, not loaded at startup, not in the output dir).

## DynamicCompiler Differences

The production `DynamicCompiler` in `Corvus.Text.Json.Validator` has extra complexity beyond the test fixtures:

- Takes a `hostAssembly` parameter (the assembly whose schema types are being validated)
- Caches `(MetadataReferences, Defines)` per host assembly via `ConcurrentDictionary`
- On .NET 8+, uses a collectible `AssemblyLoadContext` to avoid memory leaks
- Falls back to `hostAssembly.Location` directory if `BaseDirectory` is unavailable (shadow-copy scenarios)
- Has a separate `ResolveTransitiveReferences` method using a `Queue<AssemblyName>` BFS

Despite these differences, the three-phase pattern is identical.

## Common Errors and Root Causes

| Error | Root Cause |
|-------|-----------|
| `CS0012: The type 'ValueType' is defined in an assembly that is not referenced. You must add a reference to assembly 'netstandard'` | `netstandard.dll` missing — transitive resolution not running or not finding it |
| `CS1705: Assembly 'System.Text.Json' uses 'mscorlib, Version=4.0.0.0' which has a higher version than referenced assembly 'mscorlib' with identity 'mscorlib, Version=2.0.0.0'` | Facade mscorlib from NuGet ref assemblies; AppDomain replacement not running |
| `CS0117: 'MethodImplOptions' does not contain a definition for 'AggressiveOptimization'` | NuGet package refs were replaced instead of supplemented; DependencyContext refs with newer APIs lost |
| `CS0009: PE image doesn't contain managed metadata` | Native DLL added as MetadataReference; need `AssemblyName.GetAssemblyName()` validation |

## Anti-Patterns

### ❌ Conditional supplement gate

```csharp
// DON'T DO THIS — facade mscorlib passes the check
bool hasFramework = references.Any(r => /* name == "mscorlib" */);
if (!hasFramework)
{
    SupplementWithDirectoryAndAppDomain(references);
}
```

### ❌ Replace all DependencyContext refs

```csharp
// DON'T DO THIS — loses NuGet package refs with newer APIs
if (!hasFramework)
{
    references = BuildReferencesFromDirectoryAndAppDomain();
}
```

### ❌ Skip defines when supplementing

```csharp
// DON'T DO THIS — loses NETFRAMEWORK, NET481 etc.
if (refs.Count > 0 && hasSystemObject)
{
    return (refs, ["DYNAMIC_BUILD"]);  // Missing ctx.CompilationOptions.Defines
}
```

## Local Testing (WSL)

To reproduce the cross-OS CI scenario locally:

```powershell
# 1. Build in WSL (Linux)
wsl -d Ubuntu-22.04 -- bash -c "
  cd /mnt/d/source/corvus-dotnet/Corvus.JsonSchema &&
  dotnet restore --force Corvus.Text.Json.slnx &&
  dotnet build Corvus.Text.Json.slnx -c Release"

# 2. Test ALL FOUR fixtures + V4 on net481 (must all pass)
dotnet test --project tests\Corvus.Text.Json.JMESPath.CodeGeneration.Tests `
  -f net481 -c Release --filter "TestCategory!=outerloop" --no-build
dotnet test --project tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests `
  -f net481 -c Release --filter "TestCategory!=outerloop" --no-build
dotnet test --project tests\Corvus.Text.Json.JsonLogic.CodeGeneration.Tests `
  -f net481 -c Release --filter "TestCategory!=outerloop" --no-build
dotnet test --project tests\Corvus.Text.Json.JsonPath.CodeGeneration.Tests `
  -f net481 -c Release --filter "TestCategory!=outerloop" --no-build
dotnet test --project tests-v4\Corvus.Json.Specs.Tests `
  -f net481 -c Release --filter "TestCategory!=outerloop" --no-build
```

**CRITICAL**: test ALL five projects, not just one. Different fixtures exercise different generated code patterns. A fix that works for JMESPath may fail for Jsonata (different API usage in generated code).

## Adding a New Dynamic Compilation Site

If you need to add a new test fixture that dynamically compiles C#:

1. Copy the `BuildCompilationContext` and `SupplementWithDirectoryAndAppDomain` methods from one of the existing fixtures (JMESPath is the simplest template)
2. Include `TryResolveReferencePaths` (the try/catch wrapper)
3. Ensure `DYNAMIC_BUILD` is always in the defines list
4. Use `CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview).WithPreprocessorSymbols(defines)` for parsing
5. Test with WSL build + Windows net481 execution before pushing
6. Add the new site to the table in `docs/CrossPlatformDynamicCompilation.md`

## Cross-References

- Full documentation: `docs/CrossPlatformDynamicCompilation.md`
- CI workflow: `.github/workflows/build.yml`
- ZeroFailed config: `.zf/config.ps1`
- Build and test skill: `corvus-build-and-test`
