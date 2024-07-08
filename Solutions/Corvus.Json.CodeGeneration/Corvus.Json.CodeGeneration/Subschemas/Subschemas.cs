// <copyright file="Subschemas.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for working with subschemas.
/// </summary>
public static class Subschemas
{
    /// <summary>
    /// Register subschema from the given schema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schema">The anchoring schema.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void RegisterLocalSubschemas(JsonSchemaRegistry jsonSchemaRegistry, in JsonElement schema, in JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        // Capture the schema value.
        JsonElement schemaValue = schema;
        IKeyword? hidesSiblingsKeyword = vocabulary.Keywords
            .FirstOrDefault(k => k is IHidesSiblingsKeyword && schemaValue.HasKeyword(k));

        if (hidesSiblingsKeyword is ILocalSubschemaRegistrationKeyword k)
        {
            // We have a keyword that hides its siblings
            // So we just add it and return
            k.RegisterLocalSubschema(jsonSchemaRegistry, schemaValue, currentLocation, vocabulary);
            return;
        }

        // Otherwise we are going to work through all the keywords.
        foreach (ILocalSubschemaRegistrationKeyword keyword in
            vocabulary.Keywords.OfType<ILocalSubschemaRegistrationKeyword>())
        {
            keyword.RegisterLocalSubschema(jsonSchemaRegistry, schema, currentLocation, vocabulary);
        }
    }

    /// <summary>
    /// Add subschemas from the given schema, and build types.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A <see cref="ValueTask"/> which completes when the subschema types are built.</returns>
    public static async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        IKeyword? hidesSiblingsKeyword = typeDeclaration.LocatedSchema.Vocabulary.Keywords
            .FirstOrDefault(k => k is IHidesSiblingsKeyword && typeDeclaration.HasKeyword(k));

        if (hidesSiblingsKeyword is ISubschemaTypeBuilderKeyword k)
        {
            // We have a keyword that hides its siblings
            // So we just add it and return
            await BuildSubschemaTypes(typeBuilderContext, typeDeclaration, k).ConfigureAwait(false);
            return;
        }

        // Otherwise we are going to work through all the keywords.
        foreach (ISubschemaTypeBuilderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<ISubschemaTypeBuilderKeyword>())
        {
            await BuildSubschemaTypes(typeBuilderContext, typeDeclaration, keyword).ConfigureAwait(false);
        }

        static async Task BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, ISubschemaTypeBuilderKeyword k)
        {
            typeBuilderContext.EnterSubschemaScopeForUnencodedPropertyName(k.Keyword);
            await k.BuildSubschemaTypes(typeBuilderContext, typeDeclaration).ConfigureAwait(false);
            typeBuilderContext.LeaveScope();
        }
    }

    /// <summary>
    /// Build subschema types for the given property.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the subschema types have been built.</returns>
    /// <exception cref="InvalidOperationException">The subschema.</exception>
    public static async ValueTask BuildSubschemaTypesForSchemaProperty(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath)
    {
        TypeDeclaration subschemaTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);
        typeDeclaration.AddSubschemaTypeDeclaration(subschemaPath, subschemaTypeDeclaration);
    }

    /// <summary>
    /// Build subschema types for the given property.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="value">The subschema value.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the subschema types have been built.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    public static async ValueTask BuildSubschemaTypesForArrayOfSchemaProperty(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"The value at {typeBuilderContext.SubschemaLocation} was not an array.");
        }

        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            typeBuilderContext.EnterSubschemaScopeForArrayIndex(index);
            TypeDeclaration subschemaTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);
            typeDeclaration.AddSubschemaTypeDeclaration(subschemaPath.AppendArrayIndexToFragment(index), subschemaTypeDeclaration);
            typeBuilderContext.LeaveScope();
            ++index;
        }
    }

    /// <summary>
    /// Build subschema types for the given property.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="value">The subschema value.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the subschema types have been built.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    public static async ValueTask BuildSubschemaTypesForMapOfSchemaProperty(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The value at {typeBuilderContext.SubschemaLocation} was not an object.");
        }

        foreach (JsonProperty item in value.EnumerateObject())
        {
            string name = item.Name;
            typeBuilderContext.EnterSubschemaScopeForUnencodedPropertyName(name);
            await BuildSubschemaTypesForSchemaProperty(
                typeBuilderContext,
                typeDeclaration,
                subschemaPath.AppendUnencodedPropertyNameToFragment(name)).ConfigureAwait(false);
            typeBuilderContext.LeaveScope();
        }
    }

    /// <summary>
    /// Build subschema types for the given property.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="value">The subschema value.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the subschema types have been built.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    public static async ValueTask BuildSubschemaTypesForMapOfSchemaIfValueIsSchemaLikeProperty(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The value at {typeBuilderContext.SubschemaLocation} was not an object.");
        }

        foreach (JsonProperty item in value.EnumerateObject())
        {
            if (item.Value.ValueKind == JsonValueKind.Object || item.Value.ValueKind == JsonValueKind.True || item.Value.ValueKind == JsonValueKind.False)
            {
                string name = item.Name;
                typeBuilderContext.EnterSubschemaScopeForUnencodedPropertyName(name);
                await BuildSubschemaTypesForSchemaProperty(
                    typeBuilderContext,
                    typeDeclaration,
                    subschemaPath.AppendUnencodedPropertyNameToFragment(name)).ConfigureAwait(false);
                typeBuilderContext.LeaveScope();
            }
        }
    }

    /// <summary>
    /// Build subschema types for the given property.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="value">The subschema value.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the subschema types have been built.</returns>
    /// <exception cref="InvalidOperationException">The value was not an array.</exception>
    public static async ValueTask BuildSubschemaTypesForSchemaIfValueIsSchemaLikeProperty(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, JsonElement value)
    {
        if (!typeDeclaration.LocatedSchema.Vocabulary.ValidateSchemaInstance(value))
        {
            // If our schema isn't a valid schema, we just ignore it.
            return;
        }

        TypeDeclaration subschemaTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);
        typeDeclaration.AddSubschemaTypeDeclaration(subschemaPath, subschemaTypeDeclaration);
    }

    /// <summary>
    /// Build subschema types for the given property.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="value">The subschema value.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the subschema types have been built.</returns>
    public static async ValueTask BuildSubschemaTypesForSchemaOrArrayOfSchemaProperty(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            await BuildSubschemaTypesForArrayOfSchemaProperty(typeBuilderContext, typeDeclaration, subschemaPath, value).ConfigureAwait(false);
        }
        else
        {
            await BuildSubschemaTypesForSchemaProperty(typeBuilderContext, typeDeclaration, subschemaPath).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Add the given property name and value as a subschema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The JSON schema registry.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="propertyValue"/> was not a valid schema instance.</exception>
    public static void AddSubschemasForSchemaProperty(JsonSchemaRegistry jsonSchemaRegistry, string propertyName, in JsonElement propertyValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);

        if (!vocabulary.ValidateSchemaInstance(propertyValue))
        {
            throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a schema object.");
        }

        jsonSchemaRegistry.AddSchemaAndSubschema(propertyLocation, propertyValue, vocabulary);
    }

    /// <summary>
    /// Add the given property name and value as a subschema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The JSON schema registry.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="propertyValue"/> was not a valid schema instance.</exception>
    public static void AddSubschemasForArrayOfSchemaProperty(JsonSchemaRegistry jsonSchemaRegistry, string propertyName, in JsonElement propertyValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
        if (propertyValue.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"The property at {propertyLocation} was expected to be an array of schema objects.");
        }

        int index = 0;
        foreach (JsonElement subschema in propertyValue.EnumerateArray())
        {
            JsonReference subschemaLocation = propertyLocation.AppendArrayIndexToFragment(index);
            jsonSchemaRegistry.AddSchemaAndSubschema(subschemaLocation, subschema, vocabulary);
            ++index;
        }
    }

    /// <summary>
    /// Add the given property name and value as a subschema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The JSON schema registry.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="propertyValue"/> was not a valid schema instance.</exception>
    public static void AddSubschemasForSchemaOrArrayOfSchemaProperty(JsonSchemaRegistry jsonSchemaRegistry, string propertyName, in JsonElement propertyValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (propertyValue.ValueKind == JsonValueKind.Object || propertyValue.ValueKind == JsonValueKind.True || propertyValue.ValueKind == JsonValueKind.False)
        {
            AddSubschemasForSchemaProperty(jsonSchemaRegistry, propertyName, propertyValue, currentLocation, vocabulary);
        }
        else if (propertyValue.ValueKind == JsonValueKind.Array)
        {
            AddSubschemasForArrayOfSchemaProperty(jsonSchemaRegistry, propertyName, propertyValue, currentLocation, vocabulary);
        }
        else
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
            throw new InvalidOperationException($"The property at {propertyLocation} was expected to be either a schema object, or an array of schema objects.");
        }
    }

    /// <summary>
    /// Add the given property name and value as a subschema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The JSON schema registry.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="propertyValue"/> was not a valid schema instance.</exception>
    public static void AddSubschemasForMapOfSchemaIfValueIsSchemaLikeProperty(JsonSchemaRegistry jsonSchemaRegistry, string propertyName, in JsonElement propertyValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
        if (propertyValue.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a map of schema objects.");
        }

        foreach (JsonProperty property in propertyValue.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.True || property.Value.ValueKind == JsonValueKind.False)
            {
                JsonReference subschemaLocation = propertyLocation.AppendUnencodedPropertyNameToFragment(property.Name);
                jsonSchemaRegistry.AddSchemaAndSubschema(subschemaLocation, property.Value, vocabulary);
            }
        }
    }

    /// <summary>
    /// Add the given property name and value as a subschema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The JSON schema registry.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="propertyValue"/> was not a valid schema instance.</exception>
    public static void AddSubschemasForMapOfSchemaProperty(JsonSchemaRegistry jsonSchemaRegistry, string propertyName, in JsonElement propertyValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
        if (propertyValue.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a map of schema objects.");
        }

        foreach (JsonProperty property in propertyValue.EnumerateObject())
        {
            JsonReference subschemaLocation = propertyLocation.AppendUnencodedPropertyNameToFragment(property.Name);
            jsonSchemaRegistry.AddSchemaAndSubschema(subschemaLocation, property.Value, vocabulary);
        }
    }

    /// <summary>
    /// Add the given property name and value as a subschema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The JSON schema registry.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="propertyValue">The property value.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <exception cref="InvalidOperationException">The <paramref name="propertyValue"/> was not a valid schema instance.</exception>
    public static void AddSubschemasForSchemaIfValueIsASchemaLikeProperty(JsonSchemaRegistry jsonSchemaRegistry, string propertyName, in JsonElement propertyValue, JsonReference currentLocation, IVocabulary vocabulary)
    {
        JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);

        if (propertyValue.ValueKind != JsonValueKind.Object && propertyValue.ValueKind != JsonValueKind.False && propertyValue.ValueKind != JsonValueKind.True)
        {
            // If we are not an object, that's OK - we just ignore it in this case.
            return;
        }

        jsonSchemaRegistry.AddSchemaAndSubschema(propertyLocation, propertyValue, vocabulary);
    }
}