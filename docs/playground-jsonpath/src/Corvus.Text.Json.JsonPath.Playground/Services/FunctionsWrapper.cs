namespace Corvus.Text.Json.JsonPath.Playground.Services;

/// <summary>
/// Shared wrapper template for custom function code. Used by both the compilation
/// service (with <c>#line</c> directives for diagnostic mapping) and the IntelliSense
/// service (without directives, for position mapping).
/// </summary>
internal static class FunctionsWrapper
{
    /// <summary>
    /// Marker that identifies where user code is inserted in the wrapper.
    /// </summary>
    internal const string UserCodeMarker = "/* __USER_CODE__ */";

    /// <summary>
    /// The file path used in <c>#line</c> directives to identify user-authored function code.
    /// Diagnostics with this mapped path are in the user's code region.
    /// </summary>
    internal const string UserFilePath = "UserFunctions.cs";

    /// <summary>
    /// The helper methods and class skeleton shared between compilation and IntelliSense.
    /// Contains a <see cref="UserCodeMarker"/> placeholder where the user's dictionary
    /// initializer body is inserted.
    /// </summary>
    private const string Template =
        """
        public static class FunctionsFactory
        {
            // ── Result helpers (workspace-aware) ──
            public static JsonElement Value(double v, JsonWorkspace workspace) => JsonPathCodeGenHelpers.DoubleToElement(v, workspace);
            public static JsonElement Value(int v, JsonWorkspace workspace) => JsonPathCodeGenHelpers.IntToElement(v, workspace);
            public static JsonElement Value(string v) => JsonElement.ParseValue(System.Text.Encoding.UTF8.GetBytes("\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
            public static JsonElement Value(bool v) => v ? JsonElement.ParseValue("true"u8) : JsonElement.ParseValue("false"u8);

            // ── Factory helpers (common function patterns) ──

            /// <summary>ValueType → ValueType function from a delegate.</summary>
            public static IJsonPathFunction ValueFunction(Func<JsonElement, JsonWorkspace, JsonElement> func)
                => new DelegateFunction(
                    JsonPathFunctionType.ValueType,
                    [JsonPathFunctionType.ValueType],
                    (args, ws) => JsonPathFunctionResult.FromValue(func(args[0].Value, ws)));

            /// <summary>ValueType → LogicalType function from a delegate.</summary>
            public static IJsonPathFunction LogicalFunction(Func<JsonElement, bool> func)
                => new DelegateFunction(
                    JsonPathFunctionType.LogicalType,
                    [JsonPathFunctionType.ValueType],
                    (args, ws) => JsonPathFunctionResult.FromLogical(func(args[0].Value)));

            /// <summary>(ValueType, ValueType) → ValueType function from a delegate.</summary>
            public static IJsonPathFunction ValueFunction(Func<JsonElement, JsonElement, JsonWorkspace, JsonElement> func)
                => new DelegateFunction(
                    JsonPathFunctionType.ValueType,
                    [JsonPathFunctionType.ValueType, JsonPathFunctionType.ValueType],
                    (args, ws) => JsonPathFunctionResult.FromValue(func(args[0].Value, args[1].Value, ws)));

            /// <summary>NodesType → ValueType function from a delegate (receives array copy of nodes).</summary>
            public static IJsonPathFunction NodesValueFunction(Func<JsonElement[], JsonWorkspace, JsonElement> func)
                => new DelegateFunction(
                    JsonPathFunctionType.ValueType,
                    [JsonPathFunctionType.NodesType],
                    (args, ws) => JsonPathFunctionResult.FromValue(func(args[0].Nodes.ToArray(), ws)));

            /// <summary>NodesType → LogicalType function from a delegate (receives array copy of nodes).</summary>
            public static IJsonPathFunction NodesLogicalFunction(Func<JsonElement[], bool> func)
                => new DelegateFunction(
                    JsonPathFunctionType.LogicalType,
                    [JsonPathFunctionType.NodesType],
                    (args, ws) => JsonPathFunctionResult.FromLogical(func(args[0].Nodes.ToArray())));

            /// <summary>
            /// General-purpose custom function with explicit types.
            /// </summary>
            public static IJsonPathFunction CustomFunction(
                JsonPathFunctionType returnType,
                JsonPathFunctionType[] parameterTypes,
                Func<ReadOnlySpan<JsonPathFunctionArgument>, JsonWorkspace, JsonPathFunctionResult> evaluate)
                => new DelegateFunction(returnType, parameterTypes, evaluate);

            public static Dictionary<string, IJsonPathFunction> Create()
            {
                return new Dictionary<string, IJsonPathFunction>
        /* __USER_CODE__ */
                ;
            }

            private sealed class DelegateFunction : IJsonPathFunction
            {
                private readonly JsonPathFunctionType[] parameterTypes;
                private readonly Func<ReadOnlySpan<JsonPathFunctionArgument>, JsonWorkspace, JsonPathFunctionResult> evaluate;

                public DelegateFunction(
                    JsonPathFunctionType returnType,
                    JsonPathFunctionType[] parameterTypes,
                    Func<ReadOnlySpan<JsonPathFunctionArgument>, JsonWorkspace, JsonPathFunctionResult> evaluate)
                {
                    this.ReturnType = returnType;
                    this.parameterTypes = parameterTypes;
                    this.evaluate = evaluate;
                }

                public JsonPathFunctionType ReturnType { get; }
                public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => this.parameterTypes;

                public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments, JsonWorkspace workspace)
                    => this.evaluate(arguments, workspace);
            }
        }
        """;

    /// <summary>
    /// Gets the wrapper source for compilation, with <c>#line</c> directives
    /// around the user code for diagnostic position mapping.
    /// </summary>
    public static string ForCompilation(string userBody)
    {
        string replacement = $"""
        #line 1 "{UserFilePath}"
        {userBody}
        #line default
        """;
        return Template.Replace(UserCodeMarker, replacement);
    }

    /// <summary>
    /// Gets the wrapper source for IntelliSense (no <c>#line</c> directives).
    /// Returns the character offset where user code starts.
    /// </summary>
    public static (string Source, int UserCodeOffset) ForIntelliSense(string userBody)
    {
        int markerIndex = Template.IndexOf(UserCodeMarker);
        string source = Template.Replace(UserCodeMarker, userBody);
        return (source, markerIndex);
    }
}
