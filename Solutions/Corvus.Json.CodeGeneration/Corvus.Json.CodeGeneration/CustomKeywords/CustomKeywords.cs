// <copyright file="CustomKeywords.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for custom keywords.
/// </summary>
public static class CustomKeywords
{
    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeScope(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeScopeCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeScopeCustomKeyword>(typeBuilderContext, typeDeclaration, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeEnteringScope(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeEnteringScopeCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeEnteringScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeEnteringScopeCustomKeyword>(typeBuilderContext, typeDeclaration, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyAfterEnteringScope(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        Apply<IApplyAfterEnteringScopeCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyAfterEnteringScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        Apply<IApplyAfterEnteringScopeCustomKeyword>(typeBuilderContext, typeDeclaration, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeAnchors(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeAnchorsCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeSubschemas(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeSubschemasCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyBeforeSubschemas(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        Apply<IApplyBeforeSubschemasCustomKeyword>(typeBuilderContext, typeDeclaration, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyAfterSubschemas(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        Apply<IApplyAfterSubschemasCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static void ApplyAfterSubschemas(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        Apply<IApplyAfterSubschemasCustomKeyword>(typeBuilderContext, typeDeclaration, cancellationToken);
    }

    private static void Apply<T>(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
        where T : notnull, ISchemaRegistrationCustomKeyword
    {
        foreach (T keyword in vocabulary.Keywords.OfType<T>())
        {
            keyword.Apply(schemaRegistry, schemaValue, currentLocation, vocabulary, cancellationToken);
        }
    }

    private static void Apply<T>(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
        where T : notnull, ITypeBuilderCustomKeyword
    {
        foreach (T keyword in typeDeclaration.Keywords().OfType<T>())
        {
            keyword.Apply(typeBuilderContext, typeDeclaration, cancellationToken);
        }
    }
}