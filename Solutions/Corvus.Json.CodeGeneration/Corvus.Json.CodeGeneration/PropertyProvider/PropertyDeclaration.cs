﻿// <copyright file="PropertyDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Represents a property defined on a type.
/// </summary>
/// <param name="jsonPropertyName">The JSON property name.</param>
/// <param name="propertyType">The JSON property type.</param>
/// <param name="requiredOrOptional">Determines whether the property is required or optional.</param>
/// <param name="localOrComposed">
/// Determines whether the property is defined on the local schema,
/// or composed from a subschema.
/// </param>
/// <param name="keyword">The keyword that provided the property.</param>
public sealed class PropertyDeclaration(
    string jsonPropertyName,
    TypeDeclaration propertyType,
    RequiredOrOptional requiredOrOptional,
    LocalOrComposed localOrComposed,
    IObjectPropertyValidationKeyword? keyword)
{
    private readonly Dictionary<string, object?> metadata = [];

    /// <summary>
    /// Gets the JSON property name.
    /// </summary>
    public string JsonPropertyName { get; } = jsonPropertyName;

    /// <summary>
    /// Gets the fully reduced property type declaration.
    /// </summary>
    public TypeDeclaration ReducedPropertyType { get; } = propertyType.ReducedTypeDeclaration().ReducedType;

    /// <summary>
    /// Gets the full keyword path modifier for the property declaration.
    /// </summary>
    public string? KeywordPathModifier => this.Keyword?.GetPathModifier(this);

    /// <summary>
    /// Gets the unreduced type of the property.
    /// </summary>
    public TypeDeclaration UnreducedPropertyType { get; } = propertyType;

    /// <summary>
    /// Gets a value indicating whether the property is required or optional.
    /// </summary>
    public RequiredOrOptional RequiredOrOptional { get; } = requiredOrOptional;

    /// <summary>
    /// Gets a value indicating whether the property is defined on the local schema, or composed from a subschema.
    /// </summary>
    public LocalOrComposed LocalOrComposed { get; } = localOrComposed;

    /// <summary>
    /// Gets the keyword that provided the property, if it is a validation property.
    /// </summary>
    public IObjectPropertyValidationKeyword? Keyword { get; } = keyword;

    /// <summary>
    /// Sets a metadata value for the property declaration.
    /// </summary>
    /// <typeparam name="T">The type of the metadata value.</typeparam>
    /// <param name="key">The key for the metadata value.</param>
    /// <param name="value">The metadata value.</param>
    public void SetMetadata<T>(string key, T value)
    {
        this.metadata[key] = (object?)value;
    }

    /// <summary>
    /// Gets a metadata value set for the property declaration.
    /// </summary>
    /// <typeparam name="T">The type of the metadata value.</typeparam>
    /// <param name="key">The key for the metadata value.</param>
    /// <param name="value">The metadata value, if found.</param>
    /// <returns><see langword="true"/> if the metadata value was found.</returns>
    public bool TryGetMetadata<T>(string key, [MaybeNullWhen(false)] out T? value)
    {
        bool result = this.metadata.TryGetValue(key, out object? candidate);
        if (result)
        {
            value = (T?)candidate;
            return true;
        }

        value = default;
        return false;
    }
}