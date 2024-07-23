// <copyright file="PropertiesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The properties keyword.
/// </summary>
public sealed class PropertiesKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        ISubschemaProviderKeyword,
        IPropertyProviderKeyword,
        IObjectPropertyValidationKeyword
{
    private const string KeywordPath = "#/properties";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private PropertiesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="PropertiesKeyword"/> keyword.
    /// </summary>
    public static PropertiesKeyword Instance { get; } = new PropertiesKeyword();

    /// <inheritdoc />
    public string Keyword => "properties";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "properties"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.Last;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.AfterComposition;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForMapOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForMapOfSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, value).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
    {
        if (source.TryGetKeyword(this, out JsonElement properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in properties.EnumerateObject())
            {
                string propertyName = property.Name;

                JsonReference propertyPath = KeywordPathReference.AppendUnencodedPropertyNameToFragment(propertyName);
                if (source.SubschemaTypeDeclarations.TryGetValue(propertyPath, out TypeDeclaration? propertyTypeDeclaration))
                {
                    target.AddOrUpdatePropertyDeclaration(
                        new(
                            target,
                            propertyName,
                            propertyTypeDeclaration,
                            RequiredOrOptional.Optional, // We always say optional; required will have been set by a "requiring" keyword
                            source == target ? LocalOrComposed.Local : LocalOrComposed.Composed,
                            this));
                }
                else
                {
                    throw new InvalidOperationException($"The subschema definition for the schema at '{source.LocatedSchema.Location}' with path '{propertyPath}' was not found.");
                }
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).OrderBy(k => k.Key).Select(t => t.Value).ToList();
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public string GetPathModifier(PropertyDeclaration property)
    {
        return KeywordPathReference.AppendUnencodedPropertyNameToFragment(property.JsonPropertyName).AppendFragment(property.UnreducedPropertyType.ReducedTypeDeclaration().ReducedPathModifier);
    }

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);
}