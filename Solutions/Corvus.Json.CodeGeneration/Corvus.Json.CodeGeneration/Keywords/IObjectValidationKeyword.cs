// <copyright file="IObjectValidationKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Validates <see cref="System.Text.Json.JsonValueKind.Object"/> values.
/// </summary>
public interface IObjectValidationKeyword : IValueKindValidationKeyword
{
    /// <summary>
    /// Gets a value indicating whether the keyword requires that property evaluation is tracked.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires property evaluations to be tracked.</returns>
    bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether the keyword requires the count of properties to be tracked.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires the count of properties to be tracked.</returns>
    /// <remarks>
    /// <para>
    /// Note that some language provider implementations may use property counting to implement property evaluation tracking
    /// (rather than, for example, annotation collection.)
    /// </para>
    /// <para>
    /// This member must not return <see langword="true"/> unless it explicitly needs property counting for its
    /// own validation purposes. The implementation must be language agnostic.
    /// </para>
    /// </remarks>
    bool RequiresPropertyCount(TypeDeclaration typeDeclaration);
}