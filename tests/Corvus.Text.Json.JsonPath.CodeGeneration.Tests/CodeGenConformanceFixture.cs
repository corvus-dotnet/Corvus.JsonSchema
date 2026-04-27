// <copyright file="CodeGenConformanceFixture.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using Corvus.Text.Json.JsonPath;
using Corvus.Text.Json.JsonPath.CodeGeneration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Extensions.DependencyModel;

namespace Corvus.Text.Json.JsonPath.CodeGeneration.Tests;

/// <summary>
/// Shared fixture that provides per-test dynamic compilation of code-generated
/// JSONPath expressions. Each unique expression is generated, compiled to an
/// in-memory assembly, and cached for reuse.
/// </summary>
public sealed class CodeGenConformanceFixture : IDisposable
{
    private const string GeneratedNamespace = "Corvus.Text.Json.JsonPath.CodeGeneration.Tests.Generated";

    private static readonly DynamicAssemblyLoadContext LoadContext = new();
    private static readonly ConcurrentDictionary<string, CompiledJsonPathExpression> Cache = new();
    private static int s_classCounter;

    private static readonly Lazy<(IEnumerable<MetadataReference> References, CSharpParseOptions ParseOptions)>
        CompilationContext = new(BuildCompilationContext);

    private static readonly TimeSpan CompilationTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    /// <summary>
    /// Generate C# for a JSONPath expression, dynamically compile it, and return
    /// the <c>Evaluate(in JsonElement, JsonWorkspace)</c> method.
    /// </summary>
    /// <param name="expression">The JSONPath expression text.</param>
    /// <returns>
    /// A <see cref="CompiledJsonPathExpression"/> containing the compiled method (or null
    /// if the expression could not be parsed/generated/compiled).
    /// </returns>
    public CompiledJsonPathExpression GetOrCompile(string expression)
    {
        return Cache.GetOrAdd(expression, static expr => CompileExpression(expr));
    }

    private static CompiledJsonPathExpression CompileExpression(string expr)
    {
        string? generatedCode = null;
        try
        {
            int id = Interlocked.Increment(ref s_classCounter);
            string className = $"Expr_{id}";

            var compileTask = Task.Run(() =>
            {
                string code = JsonPathCodeGenerator.Generate(expr, className, GeneratedNamespace);

                var (references, parseOptions) = CompilationContext.Value;
                SyntaxTree tree = CSharpSyntaxTree.ParseText(code, options: parseOptions);

                CSharpCompilation compilation = CSharpCompilation.Create(
                    $"Corvus.JsonPath.Dynamic_{id}",
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
                            .Take(10)
                            .Select(d => d.ToString()));
                    return (code, (Assembly?)null, $"Compilation failed:\n{errors}");
                }

                ms.Seek(0, SeekOrigin.Begin);
                Assembly assembly = LoadContext.LoadFromStream(ms);
                return (code, (Assembly?)assembly, (string?)null);
            });

            if (!compileTask.Wait(CompilationTimeout))
            {
                return new CompiledJsonPathExpression(
                    null, generatedCode,
                    $"Pipeline timed out after {CompilationTimeout.TotalSeconds}s for: {(expr.Length > 80 ? expr[..80] + "..." : expr)}");
            }

            var (genCode, asm, error) = compileTask.Result;
            generatedCode = genCode;

            if (error is not null)
            {
                return new CompiledJsonPathExpression(null, generatedCode, error);
            }

            string fullTypeName = $"{GeneratedNamespace}.{className}";
            Type? type = asm!.GetType(fullTypeName);
            MethodInfo? method = type?.GetMethod(
                "Evaluate",
                [typeof(Corvus.Text.Json.JsonElement).MakeByRefType(), typeof(Corvus.Text.Json.JsonWorkspace)]);

            return new CompiledJsonPathExpression(method, generatedCode, method is null ? $"Type or method not found: {fullTypeName}" : null);
        }
        catch (AggregateException ae) when (ae.InnerException is not null)
        {
            return new CompiledJsonPathExpression(null, generatedCode, ae.InnerException.Message, IsParseError: true);
        }
        catch (JsonPathException ex)
        {
            return new CompiledJsonPathExpression(null, generatedCode, ex.Message, IsParseError: true);
        }
        catch (Exception ex)
        {
            return new CompiledJsonPathExpression(null, generatedCode, ex.Message);
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
        }
        else
        {
            references = [];
            string? dir = AppDomain.CurrentDomain.BaseDirectory;
            if (dir is not null && Directory.Exists(dir))
            {
                foreach (string dll in Directory.EnumerateFiles(dir, "*.dll"))
                {
                    try
                    {
                        references.Add(MetadataReference.CreateFromFile(dll));
                    }
                    catch
                    {
                        // Skip native DLLs.
                    }
                }
            }
        }

        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols(defines);

        return (references, parseOptions);
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

    private sealed class DynamicAssemblyLoadContext : AssemblyLoadContext
    {
        public DynamicAssemblyLoadContext()
            : base($"JsonPathCodeGenConformance_{Guid.NewGuid():N}", isCollectible: true)
        {
        }
    }
}

/// <summary>
/// The result of generating and dynamically compiling a JSONPath expression.
/// </summary>
/// <param name="Method">The compiled <c>Evaluate(in JsonElement, JsonWorkspace)</c> method, or null on failure.</param>
/// <param name="GeneratedCode">The generated C# source code, or null if generation failed.</param>
/// <param name="Error">An error message if generation or compilation failed.</param>
/// <param name="IsParseError">True if the failure was a parse/syntax error in the expression.</param>
public record CompiledJsonPathExpression(
    MethodInfo? Method,
    string? GeneratedCode,
    string? Error,
    bool IsParseError = false);
