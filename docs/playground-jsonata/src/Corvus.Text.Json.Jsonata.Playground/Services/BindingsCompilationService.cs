using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Corvus.Text.Json.Jsonata.Playground.Services;

/// <summary>
/// Compiles user-defined bindings code into a <see cref="Dictionary{TKey,TValue}"/>
/// of <see cref="JsonataBinding"/> instances using Roslyn in the browser.
/// </summary>
public class BindingsCompilationService
{
    /// <summary>
    /// The file path used in <c>#line</c> directives to identify user-authored bindings code.
    /// Diagnostics with this mapped path are in the user's code region.
    /// </summary>
    internal const string UserFilePath = "UserBindings.cs";

    private readonly WorkspaceService workspaceService;

    public BindingsCompilationService(WorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService;
    }

    /// <summary>
    /// Compile and execute bindings code. The user writes the dictionary initializer body;
    /// this wraps it in a class with helper methods and invokes it.
    /// </summary>
    /// <param name="bindingsCode">
    /// The dictionary initializer body, e.g.:
    /// <code>
    /// {
    ///     ["pi"] = Value(3.1415926535898),
    ///     ["cosine"] = Function((args, ws) =&gt; ToElement(Math.Cos(args[0].GetDouble())), 1),
    /// }
    /// </code>
    /// </param>
    public async Task<BindingsCompilationResult> CompileAsync(string bindingsCode)
    {
        string trimmed = bindingsCode.Trim();

        // Empty or comment-only → no bindings
        if (string.IsNullOrWhiteSpace(trimmed) || IsCommentOnly(trimmed))
        {
            return new BindingsCompilationResult { Success = true };
        }

        await this.workspaceService.EnsureInitializedAsync();

        // Use the original (untrimmed) code so #line-mapped positions match the editor
        string source = WrapInClass(bindingsCode);

        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation =
            this.workspaceService.CreateCompilation(source);

        await Task.Yield();

        using var ms = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(ms);

        if (!result.Success)
        {
            var diagnostics = new List<BindingsDiagnostic>();
            var errorMessages = new List<string>();

            foreach (var d in result.Diagnostics.Where(d => d.Severity >= DiagnosticSeverity.Warning))
            {
                if (!d.Location.IsInSource)
                {
                    continue;
                }

                var mappedSpan = d.Location.GetMappedLineSpan();
                bool isUserCode = string.Equals(
                    mappedSpan.Path, UserFilePath, StringComparison.OrdinalIgnoreCase);

                if (isUserCode)
                {
                    var start = mappedSpan.StartLinePosition;
                    var end = mappedSpan.EndLinePosition;

                    diagnostics.Add(new BindingsDiagnostic
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

            return new BindingsCompilationResult
            {
                Success = false,
                ErrorMessage = string.Join("\n", errorMessages),
                Diagnostics = diagnostics,
            };
        }

        ms.Seek(0, SeekOrigin.Begin);

        try
        {
            var bindings = Execute(ms.ToArray());
            return new BindingsCompilationResult
            {
                Success = true,
                Bindings = bindings,
            };
        }
        catch (Exception ex)
        {
            string message = ex is TargetInvocationException { InnerException: { } inner }
                ? inner.Message
                : ex.Message;

            return new BindingsCompilationResult
            {
                Success = false,
                ErrorMessage = $"Runtime error: {message}",
            };
        }
    }

    /// <summary>
    /// Wraps the user's dictionary initializer body in a compilable class.
    /// Uses <c>#line</c> directives so Roslyn reports diagnostics with positions
    /// relative to the user's editor content (not the generated boilerplate).
    /// </summary>
    private static string WrapInClass(string body) =>
        $$"""
        public static class BindingsFactory
        {
            public static JsonataBinding Value(double v)
                => JsonataBinding.FromValue(v);

            public static JsonataBinding Value(string v)
                => JsonataBinding.FromValue(v);

            public static JsonataBinding Value(bool v)
                => JsonataBinding.FromValue(v);

            public static JsonataBinding Value(JsonElement v)
                => JsonataBinding.FromValue(v);

            public static JsonataBinding Function(Func<double, double> func)
                => JsonataBinding.FromFunction(func);

            public static JsonataBinding Function(Func<double, double, double> func)
                => JsonataBinding.FromFunction(func);

            public static JsonataBinding Function(
                SequenceFunction func,
                int parameterCount,
                string? signature = null)
                => JsonataBinding.FromFunction(func, parameterCount, signature);

            public static JsonataBinding ElementFunction(
                Func<JsonElement[], JsonWorkspace, JsonElement> func,
                int parameterCount,
                string? signature = null)
                => JsonataBinding.FromFunction(func, parameterCount, signature);

            public static Dictionary<string, JsonataBinding> Create()
            {
                return new Dictionary<string, JsonataBinding>
        #line 1 "{{UserFilePath}}"
        {{body}}
        #line default
                ;
            }
        }
        """;

    private static Dictionary<string, JsonataBinding>? Execute(byte[] assemblyBytes)
    {
        using var ms = new MemoryStream(assemblyBytes);
        var context = new CollectibleAssemblyLoadContext();

        try
        {
            Assembly assembly = context.LoadFromStream(ms);
            Type? type = assembly.GetType("BindingsFactory");
            MethodInfo? method = type?.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);

            return method?.Invoke(null, null) as Dictionary<string, JsonataBinding>;
        }
        finally
        {
            context.Unload();
        }
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
/// A single diagnostic from bindings compilation, with positions
/// mapped to the user's editor content.
/// </summary>
public sealed class BindingsDiagnostic
{
    /// <summary>Gets the diagnostic message.</summary>
    public string Message { get; init; } = "";

    /// <summary>Gets the 1-based start line in the user's bindings code.</summary>
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
/// Result of compiling bindings code.
/// </summary>
public class BindingsCompilationResult
{
    public bool Success { get; set; }

    public Dictionary<string, JsonataBinding>? Bindings { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets structured diagnostics with positions relative to the user's editor content.
    /// Only populated on compilation failure.
    /// </summary>
    public List<BindingsDiagnostic>? Diagnostics { get; set; }
}
