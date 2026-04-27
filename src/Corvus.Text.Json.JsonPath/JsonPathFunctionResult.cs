// <copyright file="JsonPathFunctionResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// The result of a custom <see cref="IJsonPathFunction"/> evaluation.
/// </summary>
/// <remarks>
/// Use the static factory methods to create results matching the function's
/// declared <see cref="IJsonPathFunction.ReturnType"/>:
/// <list type="bullet">
/// <item><see cref="FromValue"/> — for <see cref="JsonPathFunctionType.ValueType"/> returns.</item>
/// <item><see cref="FromLogical"/> — for <see cref="JsonPathFunctionType.LogicalType"/> returns.</item>
/// <item><see cref="Nothing"/> — when the function produces no meaningful result
/// (e.g. type mismatch on an argument).</item>
/// </list>
/// </remarks>
public readonly struct JsonPathFunctionResult
{
    private readonly JsonElement _value;
    private readonly bool _logical;
    private readonly JsonPathFunctionResultKind _kind;

    private JsonPathFunctionResult(JsonPathFunctionResultKind kind, JsonElement value, bool logical)
    {
        _kind = kind;
        _value = value;
        _logical = logical;
    }

    /// <summary>
    /// Gets a result representing "nothing" — the function produced no output.
    /// </summary>
    public static JsonPathFunctionResult Nothing { get; } = new(JsonPathFunctionResultKind.Nothing, default, false);

    /// <summary>
    /// Creates a <see cref="JsonPathFunctionType.ValueType"/> result.
    /// </summary>
    /// <param name="value">The JSON element to return.</param>
    /// <returns>A value result.</returns>
    public static JsonPathFunctionResult FromValue(JsonElement value)
        => new(JsonPathFunctionResultKind.Value, value, false);

    /// <summary>
    /// Creates a <see cref="JsonPathFunctionType.LogicalType"/> result.
    /// </summary>
    /// <param name="value">The boolean value to return.</param>
    /// <returns>A logical result.</returns>
    public static JsonPathFunctionResult FromLogical(bool value)
        => new(JsonPathFunctionResultKind.Logical, default, value);

    /// <summary>
    /// Gets the kind of this result.
    /// </summary>
    internal JsonPathFunctionResultKind Kind => _kind;

    /// <summary>
    /// Gets the JSON element value. Valid when <see cref="Kind"/> is
    /// <see cref="JsonPathFunctionResultKind.Value"/>.
    /// </summary>
    internal JsonElement Value => _value;

    /// <summary>
    /// Gets the logical value. Valid when <see cref="Kind"/> is
    /// <see cref="JsonPathFunctionResultKind.Logical"/>.
    /// </summary>
    internal bool Logical => _logical;

    /// <summary>
    /// The kind of a <see cref="JsonPathFunctionResult"/>.
    /// </summary>
    internal enum JsonPathFunctionResultKind : byte
    {
        /// <summary>No result.</summary>
        Nothing,

        /// <summary>A JSON element value.</summary>
        Value,

        /// <summary>A logical (boolean) value.</summary>
        Logical,
    }
}
