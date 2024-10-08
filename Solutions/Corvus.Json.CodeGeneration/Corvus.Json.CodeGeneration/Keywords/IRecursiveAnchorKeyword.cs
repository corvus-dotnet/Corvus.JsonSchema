// <copyright file="IRecursiveAnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// An anchor keyword that indicates that its containing schema behaves as a recursive anchor.
/// for an anchor.
/// </summary>
public interface IRecursiveAnchorKeyword : IAnchorKeyword
{
    /// <summary>
    /// Determines whether the given schema has a recursive anchor.
    /// </summary>
    /// <param name="schema">The schema to check.</param>
    /// <returns><see langword="true"/> if the schema has a recursive anchor.</returns>
    bool IsRecursiveAnchor(in JsonElement schema);

    /// <summary>
    /// Try to get the base scope location for the first recursive anchor.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="baseScopeLocation">The base scope location.</param>
    /// <returns><see langword="true"/> if a base scope location could be found.</returns>
    bool TryGetScopeForFirstRecursiveAnchor(TypeBuilderContext typeBuilderContext, [NotNullWhen(true)] out JsonReference? baseScopeLocation);
}