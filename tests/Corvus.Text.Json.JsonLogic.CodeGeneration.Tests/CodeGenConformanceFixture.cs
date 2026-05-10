// <copyright file="CodeGenConformanceFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Reflection;

#if NET
using System.Runtime.Loader;
#endif

using Corvus.Text.Json.JsonLogic.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyModel;

namespace Corvus.Text.Json.JsonLogic.CodeGeneration.Tests;

/// <summary>
/// Shared fixture that provides per-rule dynamic compilation of code-generated
/// JsonLogic rules. Each unique rule is generated, compiled to an in-memory
/// assembly, and cached for reuse.
/// </summary>
public sealed class CodeGenConformanceFixture : IDisposable
{
    private const string GeneratedNamespace = "Corvus.Text.Json.JsonLogic.CodeGeneration.Tests.Generated";

#if NET
    private static readonly DynamicAssemblyLoadContext LoadContext = new();
#endif
    private static readonly ConcurrentDictionary<string, CompiledRule> Cache = new();
    private static int s_classCounter;

    private static readonly Lazy<(IEnumerable<MetadataReference> References, CSharpParseOptions ParseOptions)>
        CompilationContext = new(BuildCompilationContext);

    private static readonly TimeSpan CompilationTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <summary>
    /// Generate C# for a JsonLogic rule, dynamically compile it, and return
    /// the <c>Evaluate(in JsonElement, JsonWorkspace)</c> method.
    /// </summary>
    /// <param name="ruleJson">The JsonLogic rule as a JSON string.</param>
    /// <returns>A <see cref="CompiledRule"/> containing the compiled method or error details.</returns>
    public CompiledRule GetOrCompile(string ruleJson)
    {
        return Cache.GetOrAdd(ruleJson, static rule =>
        {
            string? generatedCode = null;
            try
            {
                int id = Interlocked.Increment(ref s_classCounter);
                string className = $"Rule_{id}";

                var compileTask = Task.Run(() =>
                {
                    string code = JsonLogicCodeGenerator.Generate(rule, className, GeneratedNamespace);

                    var (references, parseOptions) = CompilationContext.Value;
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(code, options: parseOptions);

                    CSharpCompilation compilation = CSharpCompilation.Create(
                        $"Corvus.JsonLogic.Dynamic_{id}",
                        [tree],
                        references,
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                            .WithNullableContextOptions(NullableContextOptions.Enable));

                    using MemoryStream ms = new();
                    EmitResult result = compilation.Emit(ms);
                    if (!result.Success)
                    {
                        string errors = string.Join(
                            Environment.NewLine,
                            result.Diagnostics
                                .Where(d => d.Severity == DiagnosticSeverity.Error)
                                .Take(5)
                                .Select(d => d.ToString()));
                        return (code, (Assembly?)null, $"Compilation failed:\n{errors}");
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = LoadAssembly(ms);
                    return (code, (Assembly?)assembly, (string?)null);
                });

                if (!compileTask.Wait(CompilationTimeout))
                {
                    return new CompiledRule(
                        null, generatedCode,
                        $"Pipeline timed out after {CompilationTimeout.TotalSeconds}s for: {(rule.Length > 80 ? rule.Substring(0, 80) + "..." : rule)}");
                }

                var (genCode, asm, error) = compileTask.Result;
                generatedCode = genCode;

                if (error is not null)
                {
                    return new CompiledRule(null, generatedCode, error);
                }

                string fullTypeName = $"{GeneratedNamespace}.{className}";
                Type? type = asm!.GetType(fullTypeName);
                MethodInfo? method = type?.GetMethod(
                    "Evaluate",
                    [typeof(Corvus.Text.Json.JsonElement).MakeByRefType(), typeof(Corvus.Text.Json.JsonWorkspace)]);

                return new CompiledRule(method, generatedCode, method is null ? $"Type or method not found: {fullTypeName}" : null);
            }
            catch (AggregateException ae) when (ae.InnerException is not null)
            {
                return new CompiledRule(null, generatedCode, ae.InnerException.Message);
            }
            catch (Exception ex)
            {
                return new CompiledRule(null, generatedCode, ex.Message);
            }
        });
    }

    private static (IEnumerable<MetadataReference>, CSharpParseOptions) BuildCompilationContext()
    {
        DependencyContext? ctx = DependencyContext.Load(Assembly.GetExecutingAssembly()) ?? DependencyContext.Default;

        List<MetadataReference> references;
        List<string> defines = ["DYNAMIC_BUILD"];

        if (ctx is not null)
        {
            references = (
                from l in ctx.CompileLibraries
                from r in TryResolveReferencePaths(l)
                select (MetadataReference)MetadataReference.CreateFromFile(r)).ToList();

            bool hasFrameworkAssembly = references.Any(r =>
                r is PortableExecutableReference peRef && peRef.FilePath is string path &&
                (Path.GetFileNameWithoutExtension(path).Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(path).Equals("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(path).Equals("netstandard", StringComparison.OrdinalIgnoreCase)));

            if (references.Count > 0 && hasFrameworkAssembly)
            {
                defines.AddRange(ctx.CompilationOptions.Defines.Where(d => d is not null)!);
            }
            else
            {
                references = BuildReferencesFromDirectoryAndAppDomain();
            }
        }
        else
        {
            references = BuildReferencesFromDirectoryAndAppDomain();
        }

        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(defines);

        return (references, parseOptions);
    }

    private static List<MetadataReference> BuildReferencesFromDirectoryAndAppDomain()
    {
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        List<MetadataReference> references = [];

        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!a.IsDynamic && !string.IsNullOrEmpty(a.Location) &&
                seenNames.Add(a.GetName().Name ?? Path.GetFileNameWithoutExtension(a.Location)))
            {
                references.Add(MetadataReference.CreateFromFile(a.Location));
            }
        }

        string? dir = AppDomain.CurrentDomain.BaseDirectory;
        if (dir is not null && Directory.Exists(dir))
        {
            foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
            {
                string simpleName = Path.GetFileNameWithoutExtension(dll);
                if (seenNames.Add(simpleName))
                {
                    try
                    {
                        AssemblyName.GetAssemblyName(dll);
                        references.Add(MetadataReference.CreateFromFile(dll));
                    }
                    catch
                    {
                        seenNames.Remove(simpleName);
                    }
                }
            }
        }

        // Resolve transitive references (e.g. netstandard.dll) that may be in the
        // GAC but not loaded into the AppDomain or present in the output directory.
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!a.IsDynamic)
            {
                foreach (AssemblyName refName in a.GetReferencedAssemblies())
                {
                    try
                    {
                        Assembly resolved = Assembly.Load(refName);
                        if (!resolved.IsDynamic && !string.IsNullOrEmpty(resolved.Location) &&
                            seenNames.Add(resolved.GetName().Name ?? Path.GetFileNameWithoutExtension(resolved.Location)))
                        {
                            references.Add(MetadataReference.CreateFromFile(resolved.Location));
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        return references;
    }

    private static IEnumerable<string> TryResolveReferencePaths(CompilationLibrary library)
    {
        try
        {
            return library.ResolveReferencePaths();
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

#if NET
    private static Assembly LoadAssembly(MemoryStream stream)
    {
        return LoadContext.LoadFromStream(stream);
    }

    private sealed class DynamicAssemblyLoadContext : AssemblyLoadContext
    {
        public DynamicAssemblyLoadContext()
            : base($"JsonLogicCodeGenConformance_{Guid.NewGuid():N}", isCollectible: true)
        {
        }
    }
#else
    private static Assembly LoadAssembly(MemoryStream stream)
    {
        return Assembly.Load(stream.ToArray());
    }
#endif
}

/// <summary>
/// The result of generating and dynamically compiling a JsonLogic rule.
/// </summary>
/// <param name="Method">The compiled <c>Evaluate(in JsonElement, JsonWorkspace)</c> method, or null on failure.</param>
/// <param name="GeneratedCode">The generated C# source code, or null if generation failed.</param>
/// <param name="Error">An error message if generation or compilation failed.</param>
public record CompiledRule(MethodInfo? Method, string? GeneratedCode, string? Error);
