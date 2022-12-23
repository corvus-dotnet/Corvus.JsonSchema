// <copyright file="Anchor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// An anchor in a schema.
/// </summary>
internal class Anchor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Anchor"/> class.
    /// </summary>
    /// <param name="schema">The schema containing the anchor.</param>
    /// <param name="isDynamic">Whether the anchor is dynamic.</param>
    public Anchor(LocatedSchema schema, bool isDynamic)
    {
        this.Schema = schema;
        this.IsDynamic = isDynamic;
    }

    /// <summary>
    /// Gets the schema associated with the anchor.
    /// </summary>
    public LocatedSchema Schema { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a dynamic anchor.
    /// </summary>
    public bool IsDynamic { get; set; }
}