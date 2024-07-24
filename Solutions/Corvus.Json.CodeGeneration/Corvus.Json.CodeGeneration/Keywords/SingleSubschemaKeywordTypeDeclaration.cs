// <copyright file="SingleSubschemaKeywordTypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Describes the type of the items in an array.
/// </summary>
/// <param name="subschemaType">The type of the subschema.</param>
/// <param name="keyword">The keyword that provided the subschema type declaration.</param>
public sealed class SingleSubschemaKeywordTypeDeclaration(TypeDeclaration subschemaType, ISingleSubschemaProviderKeyword keyword)
{
    /// <summary>
    /// Gets the keyword that provided the array items type declaration.
    /// </summary>
    public ISingleSubschemaProviderKeyword Keyword { get; } = keyword;

    /// <summary>
    /// Gets the unreduced type of the items in the array.
    /// </summary>
    public TypeDeclaration ReducedType { get; } = subschemaType.ReducedTypeDeclaration().ReducedType;

    /// <summary>
    /// Gets the reduced path modifier for the type of the items in the array.
    /// </summary>
    public string KeywordPathModifier { get; } = keyword.GetPathModifier(subschemaType.ReducedTypeDeclaration());

    /// <summary>
    /// Gets the unreduced type of the items in the array.
    /// </summary>
    public TypeDeclaration UnreducedType { get; } = subschemaType;
}