// <copyright file="DynamicLocatedAnchor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A named located anchor.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DynamicLocatedAnchor"/> class.
/// </remarks>
/// <param name="name">The name of the anchor.</param>
/// <param name="schema">The located schema providing the anchor.</param>
public class DynamicLocatedAnchor(string name, LocatedSchema schema) : NamedLocatedAnchor(name, schema)
{
}