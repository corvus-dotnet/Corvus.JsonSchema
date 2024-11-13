// <copyright file="ITupleTypeProviderKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that can provide a tuple definition.
/// </summary>
public interface ITupleTypeProviderKeyword : IKeyword
{
    /// <summary>
    /// Gets the reduced path modifier for the item at a tuple index.
    /// </summary>
    /// <param name="item">The tuple type declaration.</param>
    /// <param name="i">The index of the item.</param>
    /// <returns>The path modifier for this item from this keyword.</returns>
    string GetPathModifier(ReducedTypeDeclaration item, int i);

    /// <summary>
    /// Try to get the <see cref="TupleTypeDeclaration"/> for the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="tupleType">The type of the tuple, or <see langword="null"/> if no
    /// single items type is found.</param>
    /// <returns><see langword="true"/> if an array items type value was found.</returns>
    bool TryGetTupleType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out TupleTypeDeclaration? tupleType);
}