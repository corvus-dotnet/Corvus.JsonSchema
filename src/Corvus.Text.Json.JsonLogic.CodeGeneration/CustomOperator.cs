// <copyright file="CustomOperator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonLogic.CodeGeneration;

/// <summary>
/// Represents a custom operator definition parsed from a <c>.jlops</c> file.
/// </summary>
/// <remarks>
/// <para>
/// Custom operators extend the standard JsonLogic operator set with user-defined
/// C# expressions or blocks. Parameters are <c>JsonElement</c> values; the implicit
/// <c>workspace</c> parameter is always available.
/// </para>
/// <para>
/// Expression form: <c>op name(p1, p2) =&gt; expression;</c>
/// </para>
/// <para>
/// Block form:
/// <code>
/// op name(p1, p2)
/// {
///     statements;
///     return result;
/// }
/// </code>
/// </para>
/// </remarks>
public sealed class CustomOperator
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CustomOperator"/> class.
    /// </summary>
    /// <param name="name">The operator name as used in JsonLogic rules.</param>
    /// <param name="parameters">The parameter names.</param>
    /// <param name="body">The C# expression or block body.</param>
    /// <param name="isExpression">
    /// <c>true</c> if the body is a single expression (from <c>=&gt;</c> syntax);
    /// <c>false</c> if it is a block (from <c>{ }</c> syntax).
    /// </param>
    public CustomOperator(string name, string[] parameters, string body, bool isExpression)
    {
        this.Name = name;
        this.Parameters = parameters;
        this.Body = body;
        this.IsExpression = isExpression;
    }

    /// <summary>
    /// Gets the operator name as used in JsonLogic rules (e.g. <c>"discount"</c>).
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter names. Each parameter receives a <c>JsonElement</c> at runtime.
    /// </summary>
    public string[] Parameters { get; }

    /// <summary>
    /// Gets the C# body. For expression operators, this is the expression text
    /// (without trailing semicolon). For block operators, this is the full block
    /// content (without the outer braces).
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// Gets a value indicating whether this is an expression operator (<c>=&gt;</c> form)
    /// or a block operator (<c>{ }</c> form).
    /// </summary>
    public bool IsExpression { get; }
}