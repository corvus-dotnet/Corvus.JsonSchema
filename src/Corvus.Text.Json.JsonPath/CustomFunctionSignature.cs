// <copyright file="CustomFunctionSignature.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Describes the type signature of a custom JSONPath function for well-typedness
/// validation during parsing.
/// </summary>
/// <remarks>
/// This is a simple metadata type that the <see cref="Parser"/> uses to validate
/// function calls at parse time. It is independent of the runtime
/// <see cref="IJsonPathFunction"/> interface and the code-generation
/// <c>CustomFunction</c> class — both construct this signature from their own
/// domain-specific types.
/// </remarks>
public readonly struct CustomFunctionSignature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFunctionSignature"/> struct.
    /// </summary>
    /// <param name="returnType">The return type of the function.</param>
    /// <param name="parameterTypes">The parameter types. The length defines the required arity.</param>
    public CustomFunctionSignature(
        JsonPathFunctionType returnType,
        JsonPathFunctionType[] parameterTypes)
    {
        this.ReturnType = returnType;
        this.ParameterTypes = parameterTypes;
    }

    /// <summary>
    /// Gets the return type of the function.
    /// </summary>
    public JsonPathFunctionType ReturnType { get; }

    /// <summary>
    /// Gets the parameter types. The length defines the required arity.
    /// </summary>
    public JsonPathFunctionType[] ParameterTypes { get; }
}