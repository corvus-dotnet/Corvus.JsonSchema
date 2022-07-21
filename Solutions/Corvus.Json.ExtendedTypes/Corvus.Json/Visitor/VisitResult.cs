// <copyright file="VisitResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Visitor;

/// <summary>
/// Records the result of visiting the node.
/// </summary>
public ref struct VisitResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VisitResult"/> struct.
    /// </summary>
    /// <param name="output">The output from visiting the node. This will be the original node if <see cref="IsTransformed"/> is <c>false</c>.</param>
    /// <param name="transformed">Indicates whether the node was transformed or not.</param>
    /// <param name="walk">The action for the parent to take after visiting this node.</param>
    public VisitResult(JsonAny output, Transformed transformed, Walk walk)
    {
        this.Output = output;
        this.Transformed = transformed;
        this.Walk = walk;
    }

    /// <summary>
    /// Gets a value indicating whether the node was transformed.
    /// </summary>
    public bool IsTransformed => this.Transformed == Transformed.Yes;

    /// <summary>
    /// Gets or sets the result of performing the operation.
    /// </summary>
    public JsonAny Output { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the node was transformed.
    /// </summary>
    public Transformed Transformed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to continue the walk.
    /// </summary>
    public Walk Walk { get; set; }
}