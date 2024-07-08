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
    public static void ApplyBeforeScope(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        Apply<IApplyBeforeScopeCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    public static void ApplyBeforeScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        Apply<IApplyBeforeScopeCustomKeyword>(typeBuilderContext, typeDeclaration);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void ApplyBeforeEnteringScope(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        Apply<IApplyBeforeEnteringScopeCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    public static void ApplyBeforeEnteringScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        Apply<IApplyBeforeEnteringScopeCustomKeyword>(typeBuilderContext, typeDeclaration);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void ApplyAfterEnteringScope(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        Apply<IApplyAfterEnteringScopeCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    public static void ApplyAfterEnteringScope(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        Apply<IApplyAfterEnteringScopeCustomKeyword>(typeBuilderContext, typeDeclaration);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void ApplyBeforeAnchors(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        Apply<IApplyBeforeAnchorsCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void ApplyBeforeSubschemas(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        Apply<IApplyBeforeSubschemasCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary);
    }

    /// <summary>
    /// Apply the custom keyword during type build.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    public static void ApplyBeforeSubschemas(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        Apply<IApplyBeforeSubschemasCustomKeyword>(typeBuilderContext, typeDeclaration);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="schemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schemaValue">The current schema value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void ApplyAfterSubschemas(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        Apply<IApplyAfterSubschemasCustomKeyword>(schemaRegistry, schemaValue, currentLocation, vocabulary);
    }

    /// <summary>
    /// Apply the custom keyword during schema registration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    public static void ApplyAfterSubschemas(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        Apply<IApplyAfterSubschemasCustomKeyword>(typeBuilderContext, typeDeclaration);
    }

    private static void Apply<T>(JsonSchemaRegistry schemaRegistry, in JsonElement schemaValue, JsonReference currentLocation, IVocabulary vocabulary)
        where T : notnull, ISchemaRegistrationCustomKeyword
    {
        foreach (T keyword in vocabulary.Keywords.OfType<T>())
        {
            keyword.Apply(schemaRegistry, schemaValue, currentLocation, vocabulary);
        }
    }

    private static void Apply<T>(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
        where T : notnull, ITypeBuilderCustomKeyword
    {
        foreach (T keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<T>())
        {
            keyword.Apply(typeBuilderContext, typeDeclaration);
        }
    }
}