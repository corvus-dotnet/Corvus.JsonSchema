// <copyright file="IArrayContainsValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates that an array contains a particular number of a keyword.
/// </summary>
public interface IArrayContainsValidationKeyword : IArrayValidationKeyword, IArrayItemKeyword
{
    /// <summary>
    /// Try to get the items type for the contains validation.
    /// </summary>
    /// <param name="typeDeclaration">The <see cref="TypeDeclaration"/> from which to get the contains items type.</param>
    /// <param name="itemsTypeDeclaration">The contains items type, or <see langword="null"/> if no contains items type is present.</param>
    /// <returns><see langword="true"/> if a contains items type was available.</returns>
    bool TryGetContainsItemType(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out ArrayItemsTypeDeclaration? itemsTypeDeclaration);
}