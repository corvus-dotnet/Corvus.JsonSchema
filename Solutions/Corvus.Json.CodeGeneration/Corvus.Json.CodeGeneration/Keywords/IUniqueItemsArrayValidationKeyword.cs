// <copyright file="IUniqueItemsArrayValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Ensures <see cref="System.Text.Json.JsonValueKind.Array"/> values contain only unqiue items.
/// </summary>
public interface IUniqueItemsArrayValidationKeyword : IArrayValidationKeyword
{
    /// <summary>
    /// Indicates whether the array requires unique items.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the type declaration requires unique array items.</returns>
    bool RequiresUniqueItems(TypeDeclaration typeDeclaration);
}