// <copyright file="PathNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A path expression consisting of a sequence of navigation steps.
/// Produced during AST post-processing when dot-separated field accesses
/// are flattened into a linear step list.
/// </summary>
internal sealed class PathNode : JsonataNode
{
    /// <inheritdoc/>
    public override NodeType Type => NodeType.Path;

    /// <summary>Gets the ordered list of path steps.</summary>
    public List<JsonataNode> Steps { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether singleton results should be kept as arrays
    /// (when any step uses the <c>[]</c> keep-array modifier).
    /// </summary>
    public bool KeepSingletonArray { get; set; }
}