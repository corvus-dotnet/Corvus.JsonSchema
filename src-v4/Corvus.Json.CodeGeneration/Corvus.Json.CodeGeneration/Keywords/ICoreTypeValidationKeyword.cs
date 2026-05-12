// <copyright file="ICoreTypeValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A keyword validating core types.
/// </summary>
public interface ICoreTypeValidationKeyword : IValueKindValidationKeyword
{
    /// <summary>
    /// Gets a value indicating which core types are allowed by the keyword given its value.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <returns>The core types allowed by the keyword.</returns>
    CoreTypes AllowedCoreTypes(TypeDeclaration typeDeclaration);
}