// <copyright file="IJsonPathFunction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Defines a custom JSONPath function extension that can be registered on a
/// <see cref="JsonPathEvaluator"/>.
/// </summary>
/// <remarks>
/// <para>
/// Custom functions participate in RFC 9535 well-typedness checking: the
/// <see cref="ParameterTypes"/> and <see cref="ReturnType"/> are validated
/// at expression parse time. The built-in functions (<c>length</c>, <c>count</c>,
/// <c>value</c>, <c>match</c>, <c>search</c>) cannot be overridden.
/// </para>
/// <para>
/// Implementations must be <b>thread-safe</b>. A single instance may be called
/// concurrently from multiple threads when the <see cref="JsonPathEvaluator"/>
/// is shared.
/// </para>
/// </remarks>
public interface IJsonPathFunction
{
    /// <summary>
    /// Gets the return type of this function.
    /// </summary>
    JsonPathFunctionType ReturnType { get; }

    /// <summary>
    /// Gets the parameter types of this function. The length of this span
    /// defines the required arity.
    /// </summary>
    ReadOnlySpan<JsonPathFunctionType> ParameterTypes { get; }

    /// <summary>
    /// Evaluates the function with the given arguments.
    /// </summary>
    /// <param name="arguments">
    /// The evaluated arguments, one per declared parameter. For
    /// <see cref="JsonPathFunctionType.ValueType"/> parameters, use
    /// <see cref="JsonPathFunctionArgument.Value"/>. For
    /// <see cref="JsonPathFunctionType.LogicalType"/> parameters, use
    /// <see cref="JsonPathFunctionArgument.Logical"/>. For
    /// <see cref="JsonPathFunctionType.NodesType"/> parameters, use
    /// <see cref="JsonPathFunctionArgument.Nodes"/>.
    /// </param>
    /// <returns>The function result.</returns>
    JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments);
}
