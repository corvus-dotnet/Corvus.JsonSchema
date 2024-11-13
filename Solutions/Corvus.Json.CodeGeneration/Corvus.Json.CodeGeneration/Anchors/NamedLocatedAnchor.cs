// <copyright file="NamedLocatedAnchor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A named located anchor.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="NamedLocatedAnchor"/> class.
/// </remarks>
/// <param name="name">The name of the anchor.</param>
/// <param name="schema">The located schema providing the anchor.</param>
public class NamedLocatedAnchor(string name, LocatedSchema schema) : ILocatedAnchor
{
    /// <summary>
    /// Gets the name of the anchor.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the <see cref="LocatedSchema"/> providing the anchor.
    /// </summary>
    public LocatedSchema Schema { get; } = schema;

    /// <inheritdoc/>
    public bool Equals(ILocatedAnchor? other)
    {
        if (other is NamedLocatedAnchor nla)
        {
            return this.Name == nla.Name;
        }

        return false;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is NamedLocatedAnchor nla)
        {
            return this.Name == nla.Name;
        }

        return false;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.Name.GetHashCode();
    }
}