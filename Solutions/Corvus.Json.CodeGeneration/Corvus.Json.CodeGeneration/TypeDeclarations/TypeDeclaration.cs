// <copyright file="TypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A type declaration built from a JSON schema, by calling
/// <see cref="JsonSchemaTypeBuilder.AddTypeDeclarationsAsync(Corvus.Json.JsonReference, Corvus.Json.CodeGeneration.IVocabulary, bool, CancellationToken?)"/>
/// or <see cref="JsonSchemaTypeBuilder.AddTypeDeclarations(Corvus.Json.JsonReference, Corvus.Json.CodeGeneration.IVocabulary, bool, CancellationToken?)"/>.
/// </summary>
/// <remarks>
/// <para>
/// The type declaration build goes through two phases.
/// </para>
/// <para>
/// First it is constructed from a <see cref="LocatedSchema"/>, obtained from the
/// <see cref="JsonSchemaRegistry"/>.
/// </para>
/// <para>
/// Subschema are discovered from any <see cref="ISubschemaTypeBuilderKeyword"/> in the <see cref="LocatedSchema.Vocabulary"/>, and
/// similarly, any <see cref="IReferenceKeyword"/> are resolved. This will involve loading any newly discovered base schema into
/// the <see cref="JsonSchemaRegistry"/>.
/// </para>
/// <para>
/// Then, its internal <see cref="BuildComplete"/> flag is set to <see langword="true"/> - all schema and subschema
/// are fully resolved and the <see cref="TypeDeclaration"/> can now be used for any further analysis.
/// </para>
/// </remarks>
/// <param name="locatedSchema">The located schema for the type declaration.</param>
[DebuggerDisplay("{LocatedSchema.Location}")]
public sealed class TypeDeclaration(LocatedSchema locatedSchema)
{
    private readonly Dictionary<string, TypeDeclaration> subschemaTypeDeclarations = [];
    private readonly ConcurrentDictionary<string, object?> metadata = [];
    private readonly Dictionary<string, PropertyDeclaration> properties = [];

    /// <summary>
    /// Gets the subschema type declarations.
    /// </summary>
    public IReadOnlyDictionary<string, TypeDeclaration> SubschemaTypeDeclarations => this.subschemaTypeDeclarations;

    /// <summary>
    /// Gets the located schema for the type declaration.
    /// </summary>
    public LocatedSchema LocatedSchema { get; private set; } = locatedSchema;

    /// <summary>
    /// Gets the schema location relative to the root schema.
    /// </summary>
    public JsonReference RelativeSchemaLocation { get; internal set; }

    /// <summary>
    /// Gets the property declarations for this type declaration.
    /// </summary>
    public IEnumerable<PropertyDeclaration> PropertyDeclarations => this.properties.Values.OrderBy(p => p.JsonPropertyName);

    /// <summary>
    /// Gets a value indicating whether the type has any property declarations.
    /// </summary>
    public bool HasPropertyDeclarations => this.properties.Count > 0;

    /// <summary>
    /// Gets or sets a value indicating whether the basic build process is complete.
    /// </summary>
    public bool BuildComplete { get; set; }

    /// <summary>
    /// Update the location.
    /// </summary>
    /// <param name="jsonReference">The new location.</param>
    public void UpdateLocation(JsonReference jsonReference)
    {
        this.LocatedSchema = this.LocatedSchema.WithLocation(jsonReference);
    }

    /// <summary>
    /// Adds a type declaration for a subschema to this type declaration.
    /// </summary>
    /// <param name="subschemaPath">The path to the subschema.</param>
    /// <param name="subschemaTypeDeclaration">The type declaration for the subschema.</param>
    public void AddSubschemaTypeDeclaration(JsonReference subschemaPath, TypeDeclaration subschemaTypeDeclaration)
    {
        this.subschemaTypeDeclarations.Add(subschemaPath, subschemaTypeDeclaration);
    }

    /// <summary>
    /// Sets a metadata value.
    /// </summary>
    /// <typeparam name="T">The type of the metadata value.</typeparam>
    /// <param name="key">The key for the metadata value.</param>
    /// <param name="value">The metadata value.</param>
    public void SetMetadata<T>(string key, T value)
    {
        this.metadata[key] = (object?)value;
    }

    /// <summary>
    /// Gets a metadata value set for the type declaration.
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

    /// <summary>
    /// Add or update a <see cref="PropertyDeclaration"/>.
    /// </summary>
    /// <param name="propertyDeclaration">The property declaration to add or update.</param>
    public void AddOrUpdatePropertyDeclaration(PropertyDeclaration propertyDeclaration)
    {
        if (this.properties.TryGetValue(propertyDeclaration.JsonPropertyName, out PropertyDeclaration? existingProperty))
        {
            // Merge whether this is a required property with the parent
            this.properties[propertyDeclaration.JsonPropertyName] =
                new(
                    this,
                    propertyDeclaration.JsonPropertyName,
                    propertyDeclaration.UnreducedPropertyType,
                    MergeRequiredOrOptional(propertyDeclaration, existingProperty),
                    propertyDeclaration.LocalOrComposed,
                    propertyDeclaration.Keyword ?? existingProperty.Keyword,
                    propertyDeclaration.RequiredKeyword ?? existingProperty.RequiredKeyword);
        }
        else if (propertyDeclaration.Owner != this)
        {
            this.properties[propertyDeclaration.JsonPropertyName] =
                new(
                    this,
                    propertyDeclaration.JsonPropertyName,
                    propertyDeclaration.UnreducedPropertyType,
                    propertyDeclaration.RequiredOrOptional,
                    LocalOrComposed.Composed,
                    propertyDeclaration.Keyword,
                    propertyDeclaration.RequiredKeyword);
        }
        else
        {
            this.properties.Add(propertyDeclaration.JsonPropertyName, propertyDeclaration);
        }

        static RequiredOrOptional MergeRequiredOrOptional(PropertyDeclaration propertyDeclaration, PropertyDeclaration existingProperty)
        {
            if (propertyDeclaration.RequiredOrOptional == RequiredOrOptional.Required ||
                existingProperty.RequiredOrOptional == RequiredOrOptional.Required)
            {
                return RequiredOrOptional.Required;
            }

            return RequiredOrOptional.Optional;
        }
    }

    /// <summary>
    /// Gets a value indicating if the keyword is
    /// present on the type declaration.
    /// </summary>
    /// <typeparam name="T">The type of the keyword.</typeparam>
    /// <param name="keyword">The keyword to test.</param>
    /// <returns><see langword="true"/> if the keyword is present on the type declaration.</returns>
    public bool HasKeyword<T>(T keyword)
        where T : notnull, IKeyword
    {
        return this.LocatedSchema.Schema.HasKeyword(keyword);
    }

    /// <summary>
    /// Tries to get the value of the keyword.
    /// </summary>
    /// <typeparam name="T">The type of the keyword.</typeparam>
    /// <param name="keyword">The keyword to get.</param>
    /// <param name="value">The value of the keyword.</param>
    /// <returns><see langword="true"/> if the keyword is present on the type declaration.</returns>
    public bool TryGetKeyword<T>(T keyword, out JsonElement value)
        where T : notnull, IKeyword
    {
        return this.LocatedSchema.Schema.TryGetKeyword(keyword, out value);
    }
}