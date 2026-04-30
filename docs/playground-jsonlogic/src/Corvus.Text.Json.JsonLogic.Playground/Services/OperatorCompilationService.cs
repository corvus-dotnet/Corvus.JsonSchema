using System.Reflection;
using System.Runtime.Loader;
using Corvus.Text.Json.JsonLogic.Playground.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.JsonLogic.Playground.Services;

/// <summary>
/// Compiles user-written C# code into custom JsonLogic operators.
/// Discovers operators via a static factory method returning
/// <c>Dictionary&lt;string, CustomOperatorDefinition&gt;</c>.
/// </summary>
public sealed class OperatorCompilationService
{
    private readonly WorkspaceService workspaceService;
    private CollectibleAssemblyLoadContext? previousContext;

    /// <summary>
    /// Built-in operator names that cannot be overridden by custom operators.
    /// </summary>
    private static readonly HashSet<string> BuiltInOperators = new(StringComparer.Ordinal)
    {
        "var", "missing", "missing_some",
        "if", "?:",
        "==", "===", "!=", "!==", "<", ">", "<=", ">=",
        "and", "or", "!", "!!",
        "+", "-", "*", "/", "%",
        "max", "min",
        "merge", "in",
        "cat", "substr",
        "map", "filter", "reduce", "all", "some", "none",
        "log",
    };

    public OperatorCompilationService(WorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService;
    }

    /// <summary>
    /// Compiles user code and discovers custom operator definitions.
    /// Returns the previous result on failure if <paramref name="keepLastGood"/> is true.
    /// </summary>
    public async Task<OperatorCompilationResult> CompileAsync(string userCode)
    {
        await this.workspaceService.EnsureInitializedAsync();

        // Yield before heavy work so the UI can update
        await Task.Yield();

        CSharpCompilation compilation = this.workspaceService.CreateCompilation(userCode);

        using var ms = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            return new OperatorCompilationResult
            {
                Success = false,
                Diagnostics = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList(),
            };
        }

        ms.Seek(0, SeekOrigin.Begin);

        // Load the compiled assembly in a collectible context for proper unloading
        var context = new CollectibleAssemblyLoadContext();
        Assembly assembly;
        try
        {
            assembly = context.LoadFromStream(ms);
        }
        catch (Exception ex)
        {
            context.Unload();
            return new OperatorCompilationResult
            {
                Success = false,
                ErrorMessage = $"Failed to load compiled assembly: {ex.Message}",
            };
        }

        // Discover the factory method
        Dictionary<string, CustomOperatorDefinition>? operators = null;
        string? discoveryError = null;

        foreach (Type type in assembly.GetExportedTypes())
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.ReturnType == typeof(Dictionary<string, CustomOperatorDefinition>) &&
                    method.GetParameters().Length == 0)
                {
                    try
                    {
                        operators = (Dictionary<string, CustomOperatorDefinition>?)method.Invoke(null, null);
                    }
                    catch (Exception ex)
                    {
                        discoveryError = $"Factory method '{type.Name}.{method.Name}()' threw: {(ex.InnerException ?? ex).Message}";
                    }

                    break;
                }
            }

            if (operators is not null || discoveryError is not null)
            {
                break;
            }
        }

        if (discoveryError is not null)
        {
            context.Unload();
            return new OperatorCompilationResult
            {
                Success = false,
                ErrorMessage = discoveryError,
            };
        }

        if (operators is null || operators.Count == 0)
        {
            context.Unload();
            return new OperatorCompilationResult
            {
                Success = false,
                ErrorMessage = "No factory method found. Define a public static method returning Dictionary<string, CustomOperatorDefinition> with no parameters.",
            };
        }

        // Validate no built-in collisions
        List<string> collisions = operators.Keys
            .Where(name => BuiltInOperators.Contains(name))
            .ToList();

        if (collisions.Count > 0)
        {
            context.Unload();
            return new OperatorCompilationResult
            {
                Success = false,
                ErrorMessage = $"Cannot override built-in operators: {string.Join(", ", collisions)}",
            };
        }

        // Success — unload the previous context
        this.previousContext?.Unload();
        this.previousContext = context;

        return new OperatorCompilationResult
        {
            Success = true,
            Operators = operators,
            Diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .ToList(),
        };
    }

    private sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext()
            : base(isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return null; // Fall back to default context
        }
    }
}

/// <summary>
/// Result of compiling custom operator code.
/// </summary>
public sealed class OperatorCompilationResult
{
    public bool Success { get; init; }

    public Dictionary<string, CustomOperatorDefinition>? Operators { get; init; }

    public string? ErrorMessage { get; init; }

    public List<Diagnostic> Diagnostics { get; init; } = [];
}
