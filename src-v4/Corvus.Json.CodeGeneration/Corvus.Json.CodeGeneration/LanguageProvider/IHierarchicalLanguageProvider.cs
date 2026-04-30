// <copyright file="IHierarchicalLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A language provider that supports parent/child relationships
/// in type generation.
/// </summary>
public interface IHierarchicalLanguageProvider : ILanguageProvider
{
    /// <summary>
    /// Sets the parent of a child.
    /// </summary>
    /// <param name="child">The child.</param>
    /// <param name="parent">The parent.</param>
    void SetParent(TypeDeclaration child, TypeDeclaration? parent);

    /// <summary>
    /// Gets the parent for a child.
    /// </summary>
    /// <param name="child">The child for which to get the parent.</param>
    /// <returns>The parent <see cref="TypeDeclaration"/>, or <see langword="null"/> if no parent exists.</returns>
    TypeDeclaration? GetParent(TypeDeclaration child);

    /// <summary>
    /// Gets the children for a parent type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The parent type declaration.</param>
    /// <returns>The children of the type declaration.</returns>
    IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration);
}