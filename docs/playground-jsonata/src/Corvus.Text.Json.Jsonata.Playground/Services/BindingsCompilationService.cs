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

        string source = WrapInClass(trimmed);

        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation =
            this.workspaceService.CreateCompilation(source);

        await Task.Yield();

        using var ms = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(ms);

        if (!result.Success)
        {
            var errors = result.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToList();

            return new BindingsCompilationResult
            {
                Success = false,
                ErrorMessage = string.Join("\n", errors),
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

    private static string WrapInClass(string body)
    {
        return $$"""
            public static class BindingsFactory
            {
                /// <summary>
                /// Creates a value binding from a number.
                /// </summary>
                public static JsonataBinding Value(double v)
                    => JsonataBinding.FromValue(v);

                /// <summary>
                /// Creates a value binding from a string.
                /// </summary>
                public static JsonataBinding Value(string v)
                    => JsonataBinding.FromValue(v);

                /// <summary>
                /// Creates a value binding from a boolean.
                /// </summary>
                public static JsonataBinding Value(bool v)
                    => JsonataBinding.FromValue(v);

                /// <summary>
                /// Creates a value binding from a JsonElement.
                /// </summary>
                public static JsonataBinding Value(JsonElement v)
                    => JsonataBinding.FromValue(v);

                /// <summary>
                /// Creates a unary double→double function binding.
                /// <example><c>Function((v) => Math.Cos(v))</c></example>
                /// </summary>
                public static JsonataBinding Function(Func<double, double> func)
                    => JsonataBinding.FromFunction(
                        (args, ws) => Sequence.FromDouble(func(args[0].AsDouble()), ws), 1);

                /// <summary>
                /// Creates a binary (double, double)→double function binding.
                /// <example><c>Function((a, b) => Math.Min(a, b) + 1)</c></example>
                /// </summary>
                public static JsonataBinding Function(Func<double, double, double> func)
                    => JsonataBinding.FromFunction(
                        (args, ws) => Sequence.FromDouble(func(args[0].AsDouble(), args[1].AsDouble()), ws), 2);

                /// <summary>
                /// Creates a Sequence-native function binding for full control.
                /// </summary>
                public static JsonataBinding Function(
                    Func<Sequence[], JsonWorkspace, Sequence> func,
                    int parameterCount,
                    string? signature = null)
                    => JsonataBinding.FromFunction(func, parameterCount, signature);

                /// <summary>
                /// Creates a JsonElement-based function binding (legacy API).
                /// </summary>
                public static JsonataBinding ElementFunction(
                    Func<JsonElement[], JsonWorkspace, JsonElement> func,
                    int parameterCount,
                    string? signature = null)
                    => JsonataBinding.FromFunction(func, parameterCount, signature);

                public static Dictionary<string, JsonataBinding> Create()
                {
                    return new Dictionary<string, JsonataBinding>
                    {{body}};
                }
            }
            """;
    }

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
/// Result of compiling bindings code.
/// </summary>
public class BindingsCompilationResult
{
    public bool Success { get; set; }

    public Dictionary<string, JsonataBinding>? Bindings { get; set; }

    public string? ErrorMessage { get; set; }
}
