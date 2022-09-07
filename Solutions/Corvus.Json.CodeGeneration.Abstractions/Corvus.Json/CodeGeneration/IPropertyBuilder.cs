// <copyright file="IPropertyBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

/// <summary>
/// Supplied to property builders to help them build their properties.
/// </summary>
public interface IPropertyBuilder
{
    /// <summary>
    /// Gets a type declaration to use as the JSON "any" type instance.
    /// </summary>
    TypeDeclaration AnyTypeDeclarationInstance { get; }

    /// <summary>
    /// Gets a type declaration to use as the JSON "not any" type instance.
    /// </summary>
    TypeDeclaration NotAnyTypeDeclarationInstance { get; }

    /// <summary>
    /// Find and build properties for the given types.
    /// </summary>
    /// <param name="source">The source from which to find the properties.</param>
    /// <param name="target">The target to which to add the properties.</param>
    /// <param name="typesVisited">The types we have already visited to find properties.</param>
    /// <param name="treatRequiredAsOptional">Whether to treat required properties as optional when adding.</param>
    void FindAndBuildProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> typesVisited, bool treatRequiredAsOptional);
}