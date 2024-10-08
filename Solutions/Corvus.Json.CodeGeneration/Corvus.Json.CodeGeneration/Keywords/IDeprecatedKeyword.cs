// <copyright file="IDeprecatedKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A deprecated keyword.
/// </summary>
public interface IDeprecatedKeyword : IKeyword
{
    /// <summary>
    /// Gets a value indicating whether the entity is deprecated.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <param name="message">The deprecation message, if any.</param>
    /// <returns><see langword="true"/> when the entity is deprecated.</returns>
    public bool IsDeprecated(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out string? message);
}