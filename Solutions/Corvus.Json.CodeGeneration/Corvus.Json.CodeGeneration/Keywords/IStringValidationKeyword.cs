// <copyright file="IStringValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.String"/> values.
/// </summary>
public interface IStringValidationKeyword : IValueKindValidationKeyword
{
    /// <summary>
    /// Gets a value indicating whether the keyword requires
    /// the string length.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to test the keyword.</param>
    /// <returns><see langword="true"/> if the keyword requires the string length for validation.</returns>
    bool RequiresStringLength(TypeDeclaration typeDeclaration);
}