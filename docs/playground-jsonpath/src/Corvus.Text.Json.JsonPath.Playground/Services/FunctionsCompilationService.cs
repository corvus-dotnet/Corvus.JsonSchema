using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Corvus.Text.Json.JsonPath.Playground.Services;

/// <summary>
/// Compiles user-defined custom function code into a <see cref="Dictionary{TKey,TValue}"/>
/// of <see cref="IJsonPathFunction"/> instances using Roslyn in the browser.
/// </summary>
public class FunctionsCompilationService
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
    {
        "length", "count", "value", "match", "search",
    };

    private readonly WorkspaceService workspaceService;
    private AssemblyLoadContext? previousContext;

    public FunctionsCompilationService(WorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService;
    }

    /// <summary>
    /// Compile and execute custom function code. The user writes the dictionary initializer body;
    /// this wraps it in a class with helper methods and invokes it.
    /// </summary>
    /// <param name="functionsCode">
    /// The dictionary initializer body, e.g.:
    /// <code>
    /// {
    ///     ["ceil"] = JsonPathFunction.Value((v, ws) =&gt; JsonPathFunctionResult.FromValue((int)Math.Ceiling(v.GetDouble()), ws)),
    /// }
    /// </code>
    /// </param>
    public async Task<FunctionsCompilationResult> CompileAsync(string functionsCode)
    {
        string trimmed = functionsCode.Trim();

        // Empty or comment-only → no functions
        if (string.IsNullOrWhiteSpace(trimmed) || IsCommentOnly(trimmed))
        {
            return new FunctionsCompilationResult { Success = true };
        }

        await this.workspaceService.EnsureInitializedAsync();

        // Use the original (untrimmed) code so #line-mapped positions match the editor
        string source = FunctionsWrapper.ForCompilation(functionsCode);

        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation =
            this.workspaceService.CreateCompilation(source);

        await Task.Yield();

        using var ms = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(ms);

        if (!result.Success)
        {
            var diagnostics = new List<FunctionsDiagnostic>();
            var errorMessages = new List<string>();

            foreach (var d in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning))
            {
                if (!d.Location.IsInSource)
                {
                    continue;
                }

                var mappedSpan = d.Location.GetMappedLineSpan();
                bool isUserCode = string.Equals(
                    mappedSpan.Path, FunctionsWrapper.UserFilePath, StringComparison.OrdinalIgnoreCase);

                if (isUserCode)
                {
                    var start = mappedSpan.StartLinePosition;
                    var end = mappedSpan.EndLinePosition;

                    diagnostics.Add(new FunctionsDiagnostic
                    {
                        Message = d.GetMessage(),
                        StartLine = start.Line + 1,
                        StartColumn = start.Character + 1,
                        EndLine = end.Line + 1,
                        EndColumn = end.Character + 1,
                        IsError = d.Severity == DiagnosticSeverity.Error,
                    });

                    string severity = d.Severity == DiagnosticSeverity.Error ? "error" : "warning";
                    errorMessages.Add(
                        $"({start.Line + 1},{start.Character + 1}): {severity}: {d.GetMessage()}");
                }
                else if (d.Severity == DiagnosticSeverity.Error)
                {
                    errorMessages.Add(d.GetMessage());
                }
            }

            return new FunctionsCompilationResult
            {
                Success = false,
                ErrorMessage = string.Join("\n", errorMessages),
                Diagnostics = diagnostics,
            };
        }

        ms.Seek(0, SeekOrigin.Begin);

        try
        {
            var (functions, newContext) = Execute(ms.ToArray());

            // Validate reserved names
            if (functions is not null)
            {
                foreach (string name in functions.Keys)
                {
                    if (ReservedNames.Contains(name))
                    {
                        newContext?.Unload();
                        return new FunctionsCompilationResult
                        {
                            Success = false,
                            ErrorMessage = $"'{name}' is a reserved built-in function name and cannot be overridden.",
                        };
                    }
                }
            }

            // Unload the previous context now that we have a successful replacement
            var old = this.previousContext;
            this.previousContext = newContext;
            if (old is CollectibleAssemblyLoadContext collectible)
            {
                collectible.Unload();
            }

            return new FunctionsCompilationResult
            {
                Success = true,
                Functions = functions,
            };
        }
        catch (Exception ex)
        {
            string message = ex is TargetInvocationException { InnerException: { } inner }
                ? inner.Message
                : ex.Message;

            return new FunctionsCompilationResult
            {
                Success = false,
                ErrorMessage = $"Runtime error: {message}",
            };
        }
    }

    private static (Dictionary<string, IJsonPathFunction>?, AssemblyLoadContext?) Execute(byte[] assemblyBytes)
    {
        using var ms = new MemoryStream(assemblyBytes);
        var context = new CollectibleAssemblyLoadContext();

        Assembly assembly = context.LoadFromStream(ms);
        Type? type = assembly.GetType("FunctionsFactory");
        MethodInfo? method = type?.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);

        var result = method?.Invoke(null, null) as Dictionary<string, IJsonPathFunction>;

        // Return the context so the caller can manage its lifetime
        return (result, context);
    }

    private static bool IsCommentOnly(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            string t = line.Trim();
            if (t.Length == 0 || t.StartsWith("//"))
            {
                continue;
            }

            // Block comment only
            if (t.StartsWith("/*") && t.EndsWith("*/"))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext()
            : base(isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}

/// <summary>
/// A single diagnostic from custom functions compilation, with positions
/// mapped to the user's editor content.
/// </summary>
public sealed class FunctionsDiagnostic
{
    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; init; } = "";

    /// <summary>Gets the 1-based start line in the user's functions code.</summary>
    public int StartLine { get; init; }

    /// <summary>Gets the 1-based start column.</summary>
    public int StartColumn { get; init; }

    /// <summary>Gets the 1-based end line.</summary>
    public int EndLine { get; init; }

    /// <summary>Gets the 1-based end column.</summary>
    public int EndColumn { get; init; }

    /// <summary>Gets whether this is an error (true) or warning (false).</summary>
    public bool IsError { get; init; }
}

/// <summary>
/// Result of compiling custom functions code.
/// </summary>
public class FunctionsCompilationResult
{
    public bool Success { get; set; }

    public Dictionary<string, IJsonPathFunction>? Functions { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets structured diagnostics with positions relative to the user's editor content.
    /// Only populated on compilation failure.
    /// </summary>
    public List<FunctionsDiagnostic>? Diagnostics { get; set; }
}
