// <copyright file="ISubschemaProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that provides subschema.
/// </summary>
public interface ISubschemaProviderKeyword : IKeyword
{
    /// <summary>
    /// Gets the subschema type declarations for the reference.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the referenced subschema type declaration.</param>
    /// <returns>The referenced subschema type declaration.</returns>
    IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration);
}