﻿// <copyright file="AdditionalItemsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The items keyword for 2020-12.
/// </summary>
public sealed class AdditionalItemsKeyword
    : ISubschemaTypeBuilderKeyword,
      ILocalSubschemaRegistrationKeyword,
      INonTupleArrayItemsTypeProviderKeyword,
      IArrayValidationKeyword
{
    private const string KeywordPath = "#/additionalItems";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private AdditionalItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="AdditionalItemsKeyword"/> keyword.
    /// </summary>
    public static AdditionalItemsKeyword Instance { get; } = new AdditionalItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "additionalItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "additionalItems"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        // The 2019-09 and earlier additionalItems keyword only applies if the schema is also has an array-of-schema Items keyword.
        if (schema.TryGetKeyword(this, out JsonElement value) &&
            schema.TryGetKeyword(ItemsWithSchemaOrArrayOfSchemaKeyword.Instance, out JsonElement itemsKeywordValue) &&
            itemsKeywordValue.ValueKind == JsonValueKind.Array)
        {
            Subschemas.AddSubschemasForSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        // The 2019-09 and earlier additionalItems keyword only applies if the schema is also has an array-of-schema Items keyword.
        if (this.HasKeyword(typeDeclaration) && AllowsAdditionalItems(typeDeclaration))
        {
            await Subschemas.BuildSubschemaTypesForSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, cancellationToken);
        }
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
           this.HasKeyword(typeDeclaration)
                ? CoreTypes.Array
                : CoreTypes.None;

    /// <inheritdoc />
    public bool TryGetArrayItemsType(
        TypeDeclaration typeDeclaration,
        [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType) &&
            AllowsAdditionalItems(typeDeclaration))
        {
            arrayItemsType = new(itemsType, isExplicit: false, this);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNonTupleArrayItemsType(
        TypeDeclaration typeDeclaration,
        [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType) &&
            AllowsAdditionalItems(typeDeclaration))
        {
            arrayItemsType = new(itemsType, isExplicit: true, this);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc/>
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public bool RequiresArrayLength(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public string GetPathModifier(ArrayItemsTypeDeclaration item)
    {
        return KeywordPathReference.AppendFragment(item.ReducedPathModifier);
    }

    private static bool AllowsAdditionalItems(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.TryGetKeyword(ItemsWithSchemaOrArrayOfSchemaKeyword.Instance, out JsonElement itemsKeywordValue) &&
                        itemsKeywordValue.ValueKind == JsonValueKind.Array;
    }

    private bool HasKeyword(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.HasKeyword(this);
    }
}