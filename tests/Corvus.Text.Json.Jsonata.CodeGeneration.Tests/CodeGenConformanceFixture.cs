// <copyright file="CodeGenConformanceFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Reflection;

#if NET
using System.Runtime.Loader;
#endif

using Corvus.Text.Json.Jsonata;
using Corvus.Text.Json.Jsonata.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyModel;

namespace Corvus.Text.Json.Jsonata.CodeGeneration.Tests;

/// <summary>
/// Shared fixture that provides per-test dynamic compilation of code-generated
/// JSONata expressions. Each unique expression is generated, compiled to an
/// in-memory assembly, and cached for reuse.
/// </summary>
/// <remarks>
/// <para>
/// This follows the same dynamic-compilation pattern used by the JSON Schema
/// test suite (see <c>DynamicCompiler</c> and <c>TestJsonSchemaCodeGenerator</c>):
/// generate C# → compile per expression via Roslyn → execute the result.
/// Results are cached by expression string so tests sharing the same expression
/// pay the compilation cost only once.
/// </para>
/// </remarks>
public sealed class CodeGenConformanceFixture : IDisposable
{
    private const string GeneratedNamespace = "Corvus.Text.Json.Jsonata.CodeGeneration.Tests.Generated";

#if NET
    private static readonly DynamicAssemblyLoadContext LoadContext = new();
#endif
    private static readonly ConcurrentDictionary<string, CompiledExpression> Cache = new();
    private static int s_classCounter;

    private static readonly Lazy<(IEnumerable<MetadataReference> References, CSharpParseOptions ParseOptions)>
        CompilationContext = new(BuildCompilationContext);

    /// <inheritdoc/>
    public void Dispose()
    {
        // Nothing to dispose — assemblies live in the shared load context.
    }

    /// <summary>
    /// Generate C# for a JSONata expression, dynamically compile it, and return
    /// the two-arg <c>Evaluate(in JsonElement, JsonWorkspace)</c> method.
    /// </summary>
    /// <param name="expression">The JSONata expression text.</param>
    /// <returns>
    /// A <see cref="CompiledExpression"/> containing the compiled method (or null
    /// if the expression could not be parsed/generated/compiled), plus the generated
    /// source text for diagnostics.
    /// </returns>
    /// <summary>
    /// Gets the timeout for the entire compile pipeline (code gen + Roslyn emit).
    /// </summary>
    private static readonly TimeSpan CompilationTimeout = TimeSpan.FromSeconds(30);

    public CompiledExpression GetOrCompile(string expression)
    {
        return GetOrCompile(expression, customFunctions: null);
    }

    /// <summary>
    /// Generate C# for a JSONata expression with custom functions, dynamically compile it,
    /// and return the <c>Evaluate</c> methods.
    /// </summary>
    /// <param name="expression">The JSONata expression text.</param>
    /// <param name="customFunctions">Optional custom function definitions to include.</param>
    /// <returns>
    /// A <see cref="CompiledExpression"/> containing the compiled method (or null
    /// if the expression could not be parsed/generated/compiled), plus the generated
    /// source text for diagnostics.
    /// </returns>
    public CompiledExpression GetOrCompile(string expression, IReadOnlyList<CustomFunction>? customFunctions)
    {
        if (customFunctions is not null)
        {
            return CompileExpression(expression, customFunctions);
        }

        return Cache.GetOrAdd(expression, static expr => CompileExpression(expr, null));
    }

    private static CompiledExpression CompileExpression(string expr, IReadOnlyList<CustomFunction>? customFunctions)
        {
            string? generatedCode = null;
            try
            {
                int id = Interlocked.Increment(ref s_classCounter);
                string className = $"Expr_{id}";

                // Wrap the entire pipeline in a timeout — code gen AND Roslyn emit.
                var compileTask = Task.Run(() =>
                {
                    string code = JsonataCodeGenerator.Generate(expr, className, GeneratedNamespace, customFunctions);

                    var (references, parseOptions) = CompilationContext.Value;
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(code, options: parseOptions);

                    CSharpCompilation compilation = CSharpCompilation.Create(
                        $"Corvus.Jsonata.Dynamic_{id}",
                        [tree],
                        references,
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                            .WithNullableContextOptions(NullableContextOptions.Enable));

                    using MemoryStream ms = new();
                    EmitResult result = compilation.Emit(ms);
                    if (!result.Success)
                    {
                        string errors = string.Join(
                            System.Environment.NewLine,
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
                    return new CompiledExpression(
                        null, null, generatedCode,
                        $"Pipeline timed out after {CompilationTimeout.TotalSeconds}s for: {(expr.Length > 80 ? expr.Substring(0, 80) + "..." : expr)}");
                }

                var (genCode, asm, error) = compileTask.Result;
                generatedCode = genCode;

                if (error is not null)
                {
                    return new CompiledExpression(null, null, generatedCode, error);
                }

                string fullTypeName = $"{GeneratedNamespace}.{className}";
                Type? type = asm!.GetType(fullTypeName);
                MethodInfo? method = type?.GetMethod(
                    "Evaluate",
                    [typeof(Corvus.Text.Json.JsonElement).MakeByRefType(), typeof(Corvus.Text.Json.JsonWorkspace)]);

                // Also resolve the 5-parameter overload for bindings/depth/timeout.
                MethodInfo? bindingsMethod = type?.GetMethod(
                    "Evaluate",
                    [
                        typeof(Corvus.Text.Json.JsonElement).MakeByRefType(),
                        typeof(Corvus.Text.Json.JsonWorkspace),
                        typeof(IReadOnlyDictionary<string, Corvus.Text.Json.JsonElement>),
                        typeof(int),
                        typeof(int),
                    ]);

                return new CompiledExpression(method, bindingsMethod, generatedCode, method is null ? $"Type or method not found: {fullTypeName}" : null);
            }
            catch (AggregateException ae) when (ae.InnerException is not null)
            {
                Exception inner = ae.InnerException;
                string? errorCode = inner is JsonataException jex ? jex.Code : null;
                return new CompiledExpression(null, null, generatedCode, inner.Message, errorCode);
            }
            catch (Exception ex)
            {
                string? errorCode = ex is JsonataException jex ? jex.Code : null;
                return new CompiledExpression(null, null, generatedCode, ex.Message, errorCode);
            }
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

            defines.AddRange(ctx.CompilationOptions.Defines.Where(d => d is not null)!);

            // When the build ran on a different OS (e.g. Linux build, Windows net481 test),
            // DependencyContext may partially resolve — NuGet packages in the output dir
            // resolve, but framework assemblies (mscorlib, netstandard) do not. Supplement
            // with AppDomain/directory/transitive refs to fill the gaps.
            bool hasFrameworkAssembly = references.Any(r =>
                r is PortableExecutableReference peRef && peRef.FilePath is string path &&
                (Path.GetFileNameWithoutExtension(path).Equals("mscorlib", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(path).Equals("System.Runtime", StringComparison.OrdinalIgnoreCase) ||
                 Path.GetFileNameWithoutExtension(path).Equals("netstandard", StringComparison.OrdinalIgnoreCase)));

            if (!hasFrameworkAssembly)
            {
                SupplementWithDirectoryAndAppDomain(references);
            }
        }
        else
        {
            references = [];
            SupplementWithDirectoryAndAppDomain(references);
        }

        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(defines);

        return (references, parseOptions);
    }

    private static void SupplementWithDirectoryAndAppDomain(List<MetadataReference> references)
    {
        // Seed seenNames from references already resolved by DependencyContext
        // so we don't add duplicates.
        HashSet<string> seenNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (MetadataReference r in references)
        {
            if (r is PortableExecutableReference peRef && peRef.FilePath is string path)
            {
                seenNames.Add(Path.GetFileNameWithoutExtension(path));
            }
        }

        // AppDomain assemblies — these include mscorlib from the GAC.
        foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!a.IsDynamic && !string.IsNullOrEmpty(a.Location) &&
                seenNames.Add(a.GetName().Name ?? Path.GetFileNameWithoutExtension(a.Location)))
            {
                references.Add(MetadataReference.CreateFromFile(a.Location));
            }
        }

        // DLLs in the output directory not yet seen.
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
            : base($"JsonataCodeGenConformance_{Guid.NewGuid():N}", isCollectible: true)
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
/// The result of generating and dynamically compiling a JSONata expression.
/// </summary>
/// <param name="Method">The compiled <c>Evaluate(in JsonElement, JsonWorkspace)</c> method, or null on failure.</param>
/// <param name="BindingsMethod">The compiled <c>Evaluate(in JsonElement, JsonWorkspace, IReadOnlyDictionary, int, int)</c> method, or null on failure.</param>
/// <param name="GeneratedCode">The generated C# source code, or null if generation failed.</param>
/// <param name="Error">An error message if generation or compilation failed.</param>
/// <param name="ErrorCode">The JSONata error code (e.g. <c>S0201</c>) if the failure was a parse error, or null otherwise.</param>
public record CompiledExpression(MethodInfo? Method, MethodInfo? BindingsMethod, string? GeneratedCode, string? Error, string? ErrorCode = null)
{
    /// <summary>
    /// Gets a value indicating whether the expression uses inlined code (not the runtime fallback).
    /// </summary>
    public bool IsInlined =>
        this.GeneratedCode is not null &&
        !this.GeneratedCode.Contains("s_evaluator.Evaluate(Expression, data, workspace)");
}
