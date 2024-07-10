// <copyright file="ObjectPropertyTypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Describes the type of the items in an array.
/// </summary>
/// <param name="propertiesType">The type of the properties in the object.</param>
/// <param name="isExplicit"><see langword="true"/> if the array items type is explicitly
/// defined on the type declaration.</param>
public sealed class ObjectPropertyTypeDeclaration(TypeDeclaration propertiesType, bool isExplicit)
{
    /// <summary>
    /// Gets a value indicating whether the array items type
    /// is explicitly defined on the
    /// type declaration.
    /// </summary>
    public bool IsExplicit { get; } = isExplicit;

    /// <summary>
    /// Gets the unreduced type of the properties in the object.
    /// </summary>
    public TypeDeclaration ReducedType { get; } = propertiesType.ReducedTypeDeclaration().ReducedType;

    /// <summary>
    /// Gets the reduced path modifier for the type of the properties in the object.
    /// </summary>
    public JsonReference ReducedPathModifier { get; } = propertiesType.ReducedTypeDeclaration().ReducedPathModifier;

    /// <summary>
    /// Gets the unreduced type of the properties in the object.
    /// </summary>
    public TypeDeclaration UnreducedType { get; } = propertiesType;
}