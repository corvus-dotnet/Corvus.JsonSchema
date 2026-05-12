// <copyright file="IAnnotationProducingKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Implemented by keywords that produce annotations per the JSON Schema specification.
/// </summary>
/// <remarks>
/// <para>
/// Keywords implementing this interface will have their annotation values collected
/// during standalone schema evaluation when annotation collection is enabled.
/// </para>
/// <para>
/// The annotation value is the raw JSON representation of the keyword value from the schema.
/// </para>
/// </remarks>
public interface IAnnotationProducingKeyword : IKeyword
{
    /// <summary>
    /// Gets the raw JSON representation of the annotation value for this keyword
    /// from the given type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <param name="rawJsonValue">The raw JSON value of the annotation, if present.</param>
    /// <returns><see langword="true"/> if the keyword is present and has an annotation value.</returns>
    bool TryGetAnnotationJsonValue(TypeDeclaration typeDeclaration, out string rawJsonValue);

    /// <summary>
    /// Gets the core types to which this annotation applies.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <returns>
    /// The core types for which this annotation is applicable.
    /// Return <see cref="CoreTypes.None"/> if the annotation applies to all instance types.
    /// </returns>
    CoreTypes AnnotationAppliesToCoreTypes(TypeDeclaration typeDeclaration);

    /// <summary>
    /// Determines whether this annotation keyword's preconditions are met.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration containing the keyword.</param>
    /// <returns>
    /// <see langword="true"/> if the annotation should be emitted; <see langword="false"/> if
    /// a required sibling keyword is not present (e.g., <c>contentSchema</c> requires
    /// <c>contentMediaType</c> to also be present).
    /// </returns>
    bool AnnotationPreconditionsMet(TypeDeclaration typeDeclaration);
}