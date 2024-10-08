// <copyright file="IArrayItemKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that interacts with array items.
/// </summary>
public interface IArrayItemKeyword
{
    /// <summary>
    /// Gets the reduced path modifier for the item.
    /// </summary>
    /// <param name="item">The items type declaration.</param>
    /// <returns>The path modifier for this item from this keyword.</returns>
    string GetPathModifier(ArrayItemsTypeDeclaration item);
}