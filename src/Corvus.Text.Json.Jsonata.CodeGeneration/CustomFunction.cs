// <copyright file="CustomFunction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.CodeGeneration;

/// <summary>
/// Represents a custom function definition parsed from a <c>.jfn</c> file.
/// </summary>
/// <remarks>
/// <para>
/// Custom functions extend the standard JSONata built-in function set with user-defined
/// C# expressions or blocks. Parameters are <c>JsonElement</c> values; the implicit
/// <c>workspace</c> parameter is always available.
/// </para>
/// <para>
/// Expression form: <c>fn name(p1, p2) =&gt; expression;</c>
/// </para>
/// <para>
/// Block form:
/// <code>
/// fn name(p1, p2)
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
    /// <param name="name">The function name as used in JSONata expressions (without <c>$</c> prefix).</param>
    /// <param name="parameters">The parameter names.</param>
    /// <param name="body">The C# expression or block body.</param>
    /// <param name="isExpression">
    /// <c>true</c> if the body is a single expression (from <c>=&gt;</c> syntax);
    /// <c>false</c> if it is a block (from <c>{ }</c> syntax).
    /// </param>
    public CustomFunction(string name, string[] parameters, string body, bool isExpression)
    {
        this.Name = name;
        this.Parameters = parameters;
        this.Body = body;
        this.IsExpression = isExpression;
    }

    /// <summary>
    /// Gets the function name as used in JSONata expressions (e.g. <c>"celsius_to_fahrenheit"</c>).
    /// </summary>
    /// <remarks>
    /// This is the name without the <c>$</c> prefix. In a JSONata expression, the function
    /// is called as <c>$celsius_to_fahrenheit(temp)</c>.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter names. Each parameter receives a <c>JsonElement</c> at runtime.
    /// </summary>
    public string[] Parameters { get; }

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