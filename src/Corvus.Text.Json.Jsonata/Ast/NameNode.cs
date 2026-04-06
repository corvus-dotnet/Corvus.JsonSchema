// <copyright file="NameNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A field name reference in a path expression.
/// </summary>
internal sealed class NameNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Name;

    /// <summary>Gets or sets the field name.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Gets or sets the step annotations (predicates, group-by, focus/index bindings).</summary>
    public StepAnnotations? Annotations { get; set; }
}