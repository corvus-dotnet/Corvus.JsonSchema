// <copyright file="CustomFunction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath.CodeGeneration;

/// <summary>
/// Represents a custom function definition parsed from a <c>.jpfn</c> file.
/// </summary>
/// <remarks>
/// <para>
/// Custom functions extend the standard JSONPath built-in function set with user-defined
/// C# expressions or blocks. Each parameter has a declared type (value, logical, or nodes)
/// and the function has a declared return type.
/// </para>
/// <para>
/// Expression form: <c>fn name(value p1, nodes p2) : value =&gt; expression;</c>
/// </para>
/// <para>
/// Block form:
/// <code>
/// fn name(value p1) : logical
/// {
///     statements;
///     return result;
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class CustomFunction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomFunction"/> class.
    /// </summary>
    /// <param name="name">The function name as used in JSONPath filter expressions.</param>
    /// <param name="parameters">The parameter definitions (type + name pairs).</param>
    /// <param name="returnType">The return type of the function.</param>
    /// <param name="body">The C# expression or block body.</param>
    /// <param name="isExpression">
    /// <c>true</c> if the body is a single expression (from <c>=&gt;</c> syntax);
    /// <c>false</c> if it is a block (from <c>{ }</c> syntax).
    /// </param>
    public CustomFunction(
        string name,
        FunctionParameter[] parameters,
        FunctionParamType returnType,
        string body,
        bool isExpression)
    {
        this.Name = name;
        this.Parameters = parameters;
        this.ReturnType = returnType;
        this.Body = body;
        this.IsExpression = isExpression;
    }

    /// <summary>
    /// Gets the function name (e.g. <c>"ceil"</c>, <c>"is_isbn"</c>).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter definitions.
    /// </summary>
    public FunctionParameter[] Parameters { get; }

    /// <summary>
    /// Gets the return type.
    /// </summary>
    public FunctionParamType ReturnType { get; }

    /// <summary>
    /// Gets the C# body. For expression functions, this is the expression text
    /// (without trailing semicolon). For block functions, this is the full block
    /// content (without the outer braces).
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// Gets a value indicating whether this is an expression function (<c>=&gt;</c> form)
    /// or a block function (<c>{ }</c> form).
    /// </summary>
    public bool IsExpression { get; }
}

/// <summary>
/// A typed function parameter.
/// </summary>
public readonly struct FunctionParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionParameter"/> struct.
    /// </summary>
    /// <param name="type">The parameter type.</param>
    /// <param name="name">The parameter name used in the generated C# code.</param>
    public FunctionParameter(FunctionParamType type, string name)
    {
        this.Type = type;
        this.Name = name;
    }

    /// <summary>Gets the parameter type.</summary>
    public FunctionParamType Type { get; }

    /// <summary>Gets the parameter name.</summary>
    public string Name { get; }
}

/// <summary>
/// The type of a function parameter or return value in a <c>.jpfn</c> file.
/// </summary>
public enum FunctionParamType
{
    /// <summary>A single JSON value (<c>JsonElement</c>).</summary>
    Value,

    /// <summary>A boolean logical value (<c>bool</c>).</summary>
    Logical,

    /// <summary>A node list (<c>ReadOnlySpan&lt;JsonElement&gt;</c>).</summary>
    Nodes,
}