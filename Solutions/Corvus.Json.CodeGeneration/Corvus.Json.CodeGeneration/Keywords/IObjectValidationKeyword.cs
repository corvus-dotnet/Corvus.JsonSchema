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

    /// <summary>
    /// Gets a value indicating whether the keyword requires object enumeration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires object enumeration.</returns>
    bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Gets a value indicating whether the keyword requires property names as a string.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to test.</param>
    /// <returns><see langword="true"/> if the keyword requires property names as a string.</returns>
    /// <remarks>
    /// Although the keyword itself may process the property name as a string, it is up to the individual
    /// <see cref="ILanguageProvider"/> to decide how it implements these behaviours. For example, they may
    /// lazily evaluate the string. It does not imply any particular implementation approach.
    /// </remarks>
    bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration);
}