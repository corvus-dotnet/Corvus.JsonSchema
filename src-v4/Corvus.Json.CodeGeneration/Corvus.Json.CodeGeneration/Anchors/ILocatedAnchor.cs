// <copyright file="ILocatedAnchor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Represents a document anchor.
/// </summary>
public interface ILocatedAnchor : IEquatable<ILocatedAnchor>
{
    /// <summary>
    /// Gets the schema associated with the anchor.
    /// </summary>
    LocatedSchema Schema { get; }
}