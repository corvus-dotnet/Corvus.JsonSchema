// <copyright file="IObjectPropertyTypeProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that can provide a single object property type for a type declaration.
/// </summary>
public interface IObjectPropertyTypeProviderKeyword : IKeyword
{
    /// <summary>
    /// Try to get the <see cref="ObjectPropertyTypeDeclaration"/> for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="objectPropertyType">The object property type, or <see langword="null"/> if no
    /// single object property type is found.</param>
    /// <returns><see langword="true"/> if an object property type value was found.</returns>
    bool TryGetObjectPropertyType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ObjectPropertyTypeDeclaration? objectPropertyType);
}