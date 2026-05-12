// <copyright file="IObjectPropertyNameSubschemaValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Object"/> property names against a subschema.
/// </summary>
public interface IObjectPropertyNameSubschemaValidationKeyword : IObjectPropertyNameValidationKeyword, ISingleSubschemaProviderKeyword
{
    /// <summary>
    /// Tries to get the type declaration for a property name subschema validation keyword.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the property name type declaration.</param>
    /// <param name="propertyNameType">The type declaration for the property name.</param>
    /// <returns><see langword="true"/> if the <paramref name="propertyNameType"/>/> is present.</returns>
    bool TryGetPropertyNameDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? propertyNameType);
}