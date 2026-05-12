// <copyright file="JsonPathFunctionArgument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// An evaluated argument passed to a custom <see cref="IJsonPathFunction"/>.
/// </summary>
/// <remarks>
/// <para>
/// Access the typed payload via the property that matches the declared parameter type:
/// </para>
/// <list type="bullet">
/// <item><see cref="Value"/> — for <see cref="JsonPathFunctionType.ValueType"/> parameters.</item>
/// <item><see cref="Logical"/> — for <see cref="JsonPathFunctionType.LogicalType"/> parameters.</item>
/// <item><see cref="Nodes"/> — for <see cref="JsonPathFunctionType.NodesType"/> parameters.
/// The span is backed by a pooled array whose lifetime is managed by the evaluator;
/// it is valid for the duration of the <see cref="IJsonPathFunction.Evaluate"/> call.</item>
/// </list>
/// </remarks>
public readonly struct JsonPathFunctionArgument
{
    private readonly JsonElement _value;
    private readonly JsonElement[]? _nodesArray;
    private readonly int _nodesCount;
    private readonly bool _logical;
    private readonly JsonPathFunctionType _type;

    private JsonPathFunctionArgument(
        JsonPathFunctionType type,
        JsonElement value,
        bool logical,
        JsonElement[]? nodesArray,
        int nodesCount)
    {
        _type = type;
        _value = value;
        _logical = logical;
        _nodesArray = nodesArray;
        _nodesCount = nodesCount;
    }

    /// <summary>
    /// Gets the declared type of this argument.
    /// </summary>
    public JsonPathFunctionType Type => _type;

    /// <summary>
    /// Gets the JSON element value. Valid when <see cref="Type"/> is
    /// <see cref="JsonPathFunctionType.ValueType"/>.
    /// </summary>
    public JsonElement Value => _value;

    /// <summary>
    /// Gets the logical (boolean) value. Valid when <see cref="Type"/> is
    /// <see cref="JsonPathFunctionType.LogicalType"/>.
    /// </summary>
    public bool Logical => _logical;

    /// <summary>
    /// Gets the node list as a read-only span. Valid when <see cref="Type"/> is
    /// <see cref="JsonPathFunctionType.NodesType"/>.
    /// </summary>
    /// <remarks>
    /// The span is backed by a pooled array whose lifetime is managed by the
    /// evaluator. Do not capture or store the span beyond the
    /// <see cref="IJsonPathFunction.Evaluate"/> call.
    /// </remarks>
    public ReadOnlySpan<JsonElement> Nodes =>
        _nodesArray is not null ? _nodesArray.AsSpan(0, _nodesCount) : default;

    /// <summary>
    /// Gets the number of nodes. Valid when <see cref="Type"/> is
    /// <see cref="JsonPathFunctionType.NodesType"/>.
    /// </summary>
    public int NodeCount => _nodesCount;

    /// <summary>
    /// Creates a <see cref="JsonPathFunctionType.ValueType"/> argument.
    /// </summary>
    internal static JsonPathFunctionArgument CreateValue(JsonElement value)
        => new(JsonPathFunctionType.ValueType, value, false, null, 0);

    /// <summary>
    /// Creates a <see cref="JsonPathFunctionType.LogicalType"/> argument.
    /// </summary>
    internal static JsonPathFunctionArgument CreateLogical(bool value)
        => new(JsonPathFunctionType.LogicalType, default, value, null, 0);

    /// <summary>
    /// Creates a <see cref="JsonPathFunctionType.NodesType"/> argument backed by
    /// a pooled array. The caller must keep the array alive for the duration of
    /// the function call.
    /// </summary>
    internal static JsonPathFunctionArgument CreateNodes(JsonElement[] backingArray, int count)
        => new(JsonPathFunctionType.NodesType, default, false, backingArray, count);
}
