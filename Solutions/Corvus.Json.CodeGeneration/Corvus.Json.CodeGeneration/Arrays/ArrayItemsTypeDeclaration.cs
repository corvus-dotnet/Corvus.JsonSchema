// <copyright file="ArrayItemsTypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Describes the type of the items in an array.
/// </summary>
/// <param name="itemsType">The type of the items in the array.</param>
/// <param name="isExplicit"><see langword="true"/> if the array items type is explicitly
/// defined on the type declaration.</param>
public sealed class ArrayItemsTypeDeclaration(TypeDeclaration itemsType, bool isExplicit)
{
    /// <summary>
    /// Gets a value indicating whether the array items type
    /// is explicitly defined on the
    /// type declaration.
    /// </summary>
    public bool IsExplicit { get; } = isExplicit;

    /// <summary>
    /// Gets the unreduced type of the items in the array.
    /// </summary>
    public TypeDeclaration ReducedType { get; } = itemsType.ReducedTypeDeclaration().ReducedType;

    /// <summary>
    /// Gets the reduced path modifier for the type of the items in the array.
    /// </summary>
    public JsonReference ReducedPathModifier { get; } = itemsType.ReducedTypeDeclaration().ReducedPathModifier;

    /// <summary>
    /// Gets the unreduced type of the items in the array.
    /// </summary>
    public TypeDeclaration UnreducedType { get; } = itemsType;
}