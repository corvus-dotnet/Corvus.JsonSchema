﻿// <copyright file="UnevaluatedPropertiesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The unevaluatedProperties keyword.
/// </summary>
public sealed class UnevaluatedPropertiesKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        ISubschemaProviderKeyword,
        ILocalAndAppliedEvaluatedPropertyValidationKeyword
{
    private const string KeywordPath = "#/unevaluatedProperties";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private UnevaluatedPropertiesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="UnevaluatedPropertiesKeyword"/> keyword.
    /// </summary>
    public static UnevaluatedPropertiesKeyword Instance { get; } = new UnevaluatedPropertiesKeyword();

    /// <inheritdoc />
    public string Keyword => "unevaluatedProperties";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "unevaluatedProperties"u8;

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
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

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

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            return [value];
        }

        return [];
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);
}