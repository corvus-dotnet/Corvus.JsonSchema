// <copyright file="IUnevaluatedArrayItemsTypeProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that can provide a single array type for a type declaration.
/// </summary>
public interface IUnevaluatedArrayItemsTypeProviderKeyword : INonTupleArrayItemsTypeProviderKeyword
{
    /// <summary>
    /// Try to get the unevaluated <see cref="ArrayItemsTypeDeclaration"/> for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="arrayItemsType">The unevaluated array items type, or <see langword="null"/> if no
    /// single items type is found.</param>
    /// <returns><see langword="true"/> if an array items type value was found.</returns>
    bool TryGetUnevaluatedArrayItemsType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType);
}