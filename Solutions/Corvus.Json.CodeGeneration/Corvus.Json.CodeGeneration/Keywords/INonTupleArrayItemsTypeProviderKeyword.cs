// <copyright file="INonTupleArrayItemsTypeProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that can provide a single array type for a type declaration that is used to.
/// </summary>
public interface INonTupleArrayItemsTypeProviderKeyword : IArrayItemsTypeProviderKeyword
{
    /// <summary>
    /// Try to get the non-tuple <see cref="ArrayItemsTypeDeclaration"/> for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="arrayItemsType">The non-tuple array items type, or <see langword="null"/> if no
    /// non-tuple items type is found.</param>
    /// <returns><see langword="true"/> if an array items type value was found.</returns>
    bool TryGetNonTupleArrayItemsType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType);
}