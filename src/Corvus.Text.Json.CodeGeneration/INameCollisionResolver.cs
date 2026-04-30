// <copyright file="INameCollisionResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Defines a collision resolver that creates a unique name for a child type.
/// </summary>
public interface INameCollisionResolver
{
    /// <summary>
    /// Gets a value indicating whether this is an optional collision resolver.
    /// </summary>
    bool IsOptional { get; }

    /// <summary>
    /// Gets the priority of the collision resolver (lower number is higher priority).
    /// </summary>
    uint Priority { get; }

    /// <summary>
    /// Try to resolve a name collision.
    /// </summary>
    /// <param name="languageProvider">The language provider for the collision resolver.</param>
    /// <param name="typeDeclaration">The type declaration for which to get the name.</param>
    /// <param name="parent">The fully resolved parent for the type.</param>
    /// <param name="parentName">The parent's name.</param>
    /// <param name="targetNameBuffer">The working buffer for the into which the name will be written.</param>
    /// <param name="length">The current length of the buffer.</param>
    /// <param name="written">The number of characters written.</param>
    /// <returns><see langword="true"/> if the collision resolver was able to generate a name.</returns>
    bool TryResolveNameCollision(
        CSharpLanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        TypeDeclaration parent,
        ReadOnlySpan<char> parentName,
        Span<char> targetNameBuffer,
        int length,
        out int written);
}