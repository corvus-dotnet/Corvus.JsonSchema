// <copyright file="IArrayValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Array"/> values.
/// </summary>
public interface IArrayValidationKeyword : IValueKindValidationKeyword
{
    /// <summary>
    /// Gets a value indicating whether the keyword requires that items evaluation is tracked.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires items evaluations to be tracked.</returns>
    bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether the keyword requires the array length.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires the array length.</returns>
    bool RequiresArrayLength(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether the keyword requires array enumeration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires array enumeration.</returns>
    bool RequiresArrayEnumeration(TypeDeclaration typeDeclaration);
}