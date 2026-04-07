// <copyright file="LambdaNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A lambda (anonymous function) definition:
/// <c>function($x, $y){ body }</c> or <c>λ($x, $y){ body }</c>.
/// </summary>
internal sealed class LambdaNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Lambda;

    /// <summary>Gets the parameter names (without the <c>$</c> prefix).</summary>
    public List<string> Parameters { get; } = [];

    /// <summary>Gets or sets the function body expression.</summary>
    public JsonataNode Body { get; set; } = null!;

    /// <summary>Gets or sets the parsed function signature string (e.g. <c>&lt;s-:n&gt;</c>), if specified.</summary>
    public string? Signature { get; set; }
}