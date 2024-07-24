// <copyright file="ISingleSubschemaProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides a single subschema.
/// </summary>
public interface ISingleSubschemaProviderKeyword : ISubschemaProviderKeyword
{
    /// <summary>
    /// Get the path modifier for the single subschema type.
    /// </summary>
    /// <param name="subschemaType">The subschema type.</param>
    /// <returns>The path modifier for the subschema type.</returns>
    string GetPathModifier(ReducedTypeDeclaration subschemaType);
}