﻿// <copyright file="ItemsWithSchemaKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The items keyword for 2020-12.
/// </summary>
public sealed class ItemsWithSchemaKeyword
    : ISubschemaTypeBuilderKeyword,
      ILocalSubschemaRegistrationKeyword,
      INonTupleArrayItemsTypeProviderKeyword,
      IArrayValidationKeyword
{
    private const string KeywordPath = "#/items";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private ItemsWithSchemaKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ItemsWithSchemaKeyword"/> keyword.
    /// </summary>
    public static ItemsWithSchemaKeyword Instance { get; } = new ItemsWithSchemaKeyword();

    /// <inheritdoc />
    public string Keyword => "items";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "items"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        if (typeDeclaration.HasKeyword(this))
        {
            await Subschemas.BuildSubschemaTypesForSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, cancellationToken);
        }
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
           typeDeclaration.HasKeyword(this)
                ? CoreTypes.Array
                : CoreTypes.None;

    /// <inheritdoc />
    public bool TryGetArrayItemsType(
        TypeDeclaration typeDeclaration,
        [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType))
        {
            arrayItemsType = new(itemsType, isExplicit: false, this);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetNonTupleArrayItemsType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType))
        {
            arrayItemsType = new(itemsType, isExplicit: true, this);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc/>
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayLength(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public string GetPathModifier(ArrayItemsTypeDeclaration item)
    {
        return KeywordPathReference.AppendFragment(item.ReducedPathModifier);
    }
}