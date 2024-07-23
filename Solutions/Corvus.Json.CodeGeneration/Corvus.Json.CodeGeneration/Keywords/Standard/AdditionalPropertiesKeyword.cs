// <copyright file="AdditionalPropertiesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The additionalProperties keyword.
/// </summary>
public sealed class AdditionalPropertiesKeyword
    : ILocalSubschemaRegistrationKeyword,
      ISubschemaTypeBuilderKeyword,
      ILocalEvaluatedPropertyValidationKeyword
{
    private const string KeywordPath = "#/additionalProperties";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private AdditionalPropertiesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="AdditionalPropertiesKeyword"/> keyword.
    /// </summary>
    public static AdditionalPropertiesKeyword Instance { get; } = new AdditionalPropertiesKeyword();

    /// <inheritdoc />
    public string Keyword => "additionalProperties";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "additionalProperties"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Last;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.HasKeyword(this))
        {
            await Subschemas.BuildSubschemaTypesForSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public bool TryGetFallbackObjectPropertyType(
        TypeDeclaration typeDeclaration,
        [MaybeNullWhen(false)] out FallbackObjectPropertyType? objectPropertiesType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            objectPropertiesType = new(value, this, isExplicit: true);
            return true;
        }

        objectPropertiesType = null;
        return false;
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.HasKeyword(this);
    }

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public string GetPathModifier()
    {
        return KeywordPathReference;
    }
}