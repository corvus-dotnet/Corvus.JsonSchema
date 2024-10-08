// <copyright file="IExclusiveMaximumBooleanKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword that represents the boolean exclusivity modifier for a maximum keyword.
/// </summary>
public interface IExclusiveMaximumBooleanKeyword : IKeyword
{
    /// <summary>
    /// Gets a value indicating if this keyword applies an exclusive maximum modifier
    /// to the type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the modifier should be applied.</returns>
    bool HasModifier(TypeDeclaration typeDeclaration);
}