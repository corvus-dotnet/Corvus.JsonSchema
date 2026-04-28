// <copyright file="JsonPathFunction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// A delegate for evaluating a custom JSONPath function with full access
/// to the raw arguments and workspace.
/// </summary>
/// <param name="arguments">The evaluated arguments.</param>
/// <param name="workspace">The workspace for creating intermediate JSON elements.</param>
/// <returns>The function result.</returns>
public delegate JsonPathFunctionResult JsonPathFunctionEvaluator(
    ReadOnlySpan<JsonPathFunctionArgument> arguments,
    JsonWorkspace workspace);

/// <summary>
/// Factory methods for creating <see cref="IJsonPathFunction"/> instances from
/// delegates, avoiding the need to implement the interface manually for simple
/// function signatures.
/// </summary>
/// <example>
/// <code>
/// var evaluator = new JsonPathEvaluator(new Dictionary&lt;string, IJsonPathFunction&gt;
/// {
///     ["ceil"] = JsonPathFunction.Value((v, ws) =&gt;
///         JsonPathFunctionResult.FromValue((int)Math.Ceiling(v.GetDouble()), ws)),
///     ["is_fiction"] = JsonPathFunction.Logical(v =&gt;
///         v.ValueKind == JsonValueKind.String &amp;&amp; v.ValueEquals("fiction"u8)),
/// });
/// </code>
/// </example>
public static class JsonPathFunction
{
    /// <summary>
    /// Creates a function with signature <c>(ValueType) → ValueType</c>.
    /// </summary>
    /// <param name="func">
    /// A delegate that receives a single <see cref="JsonElement"/> value argument
    /// and a <see cref="JsonWorkspace"/>, and returns a <see cref="JsonElement"/> result.
    /// </param>
    /// <returns>A new <see cref="IJsonPathFunction"/>.</returns>
    public static IJsonPathFunction Value(Func<JsonElement, JsonWorkspace, JsonElement> func)
        => new DelegateFunction(
            JsonPathFunctionType.ValueType,
            [JsonPathFunctionType.ValueType],
            (args, ws) => JsonPathFunctionResult.FromValue(func(args[0].Value, ws)));

    /// <summary>
    /// Creates a function with signature <c>(ValueType, ValueType) → ValueType</c>.
    /// </summary>
    /// <param name="func">
    /// A delegate that receives two <see cref="JsonElement"/> value arguments
    /// and a <see cref="JsonWorkspace"/>, and returns a <see cref="JsonElement"/> result.
    /// </param>
    /// <returns>A new <see cref="IJsonPathFunction"/>.</returns>
    public static IJsonPathFunction Value(Func<JsonElement, JsonElement, JsonWorkspace, JsonElement> func)
        => new DelegateFunction(
            JsonPathFunctionType.ValueType,
            [JsonPathFunctionType.ValueType, JsonPathFunctionType.ValueType],
            (args, ws) => JsonPathFunctionResult.FromValue(func(args[0].Value, args[1].Value, ws)));

    /// <summary>
    /// Creates a function with signature <c>(ValueType) → LogicalType</c>.
    /// </summary>
    /// <param name="func">
    /// A delegate that receives a single <see cref="JsonElement"/> value argument
    /// and returns a boolean.
    /// </param>
    /// <returns>A new <see cref="IJsonPathFunction"/>.</returns>
    public static IJsonPathFunction Logical(Func<JsonElement, bool> func)
        => new DelegateFunction(
            JsonPathFunctionType.LogicalType,
            [JsonPathFunctionType.ValueType],
            (args, ws) => JsonPathFunctionResult.FromLogical(func(args[0].Value)));

    /// <summary>
    /// Creates a function with signature <c>(NodesType) → ValueType</c>.
    /// </summary>
    /// <param name="func">
    /// A delegate that receives the nodes as a <see cref="JsonElement"/> array
    /// and a <see cref="JsonWorkspace"/>, and returns a <see cref="JsonElement"/> result.
    /// </param>
    /// <returns>A new <see cref="IJsonPathFunction"/>.</returns>
    /// <remarks>
    /// The nodes are copied to an array for delegate compatibility.
    /// For zero-allocation node access, implement <see cref="IJsonPathFunction"/>
    /// directly and use <see cref="JsonPathFunctionArgument.Nodes"/>.
    /// </remarks>
    public static IJsonPathFunction NodesValue(Func<JsonElement[], JsonWorkspace, JsonElement> func)
        => new DelegateFunction(
            JsonPathFunctionType.ValueType,
            [JsonPathFunctionType.NodesType],
            (args, ws) => JsonPathFunctionResult.FromValue(func(args[0].Nodes.ToArray(), ws)));

    /// <summary>
    /// Creates a function with signature <c>(NodesType) → LogicalType</c>.
    /// </summary>
    /// <param name="func">
    /// A delegate that receives the nodes as a <see cref="JsonElement"/> array
    /// and returns a boolean.
    /// </param>
    /// <returns>A new <see cref="IJsonPathFunction"/>.</returns>
    /// <remarks>
    /// The nodes are copied to an array for delegate compatibility.
    /// For zero-allocation node access, implement <see cref="IJsonPathFunction"/>
    /// directly and use <see cref="JsonPathFunctionArgument.Nodes"/>.
    /// </remarks>
    public static IJsonPathFunction NodesLogical(Func<JsonElement[], bool> func)
        => new DelegateFunction(
            JsonPathFunctionType.LogicalType,
            [JsonPathFunctionType.NodesType],
            (args, ws) => JsonPathFunctionResult.FromLogical(func(args[0].Nodes.ToArray())));

    /// <summary>
    /// Creates a function with an arbitrary signature defined by explicit type arrays.
    /// </summary>
    /// <param name="returnType">The function's return type.</param>
    /// <param name="parameterTypes">The function's parameter types.</param>
    /// <param name="evaluate">
    /// A delegate that receives the evaluated arguments and a <see cref="JsonWorkspace"/>,
    /// and returns a <see cref="JsonPathFunctionResult"/>.
    /// </param>
    /// <returns>A new <see cref="IJsonPathFunction"/>.</returns>
    public static IJsonPathFunction Create(
        JsonPathFunctionType returnType,
        JsonPathFunctionType[] parameterTypes,
        JsonPathFunctionEvaluator evaluate)
        => new DelegateFunction(returnType, parameterTypes, evaluate);

    private sealed class DelegateFunction : IJsonPathFunction
    {
        private readonly JsonPathFunctionType[] parameterTypes;
        private readonly JsonPathFunctionEvaluator evaluate;

        public DelegateFunction(
            JsonPathFunctionType returnType,
            JsonPathFunctionType[] parameterTypes,
            JsonPathFunctionEvaluator evaluate)
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
