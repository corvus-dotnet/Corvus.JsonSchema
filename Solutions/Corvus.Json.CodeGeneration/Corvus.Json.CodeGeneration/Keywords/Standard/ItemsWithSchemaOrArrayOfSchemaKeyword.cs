// <copyright file="ItemsWithSchemaOrArrayOfSchemaKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The items keyword for 2019-09 and earlier.
/// </summary>
public sealed class ItemsWithSchemaOrArrayOfSchemaKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        INonTupleArrayItemsTypeProviderKeyword,
        IArrayValidationKeyword,
        ITupleTypeProviderKeyword
{
    private const string KeywordPath = "#/items";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private ItemsWithSchemaOrArrayOfSchemaKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ItemsWithSchemaOrArrayOfSchemaKeyword"/> keyword.
    /// </summary>
    public static ItemsWithSchemaOrArrayOfSchemaKeyword Instance { get; } = new ItemsWithSchemaOrArrayOfSchemaKeyword();

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
            Subschemas.AddSubschemasForSchemaOrArrayOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForSchemaOrArrayOfSchemaProperty(
                typeBuilderContext,
                typeDeclaration,
                KeywordPathReference,
                value,
                cancellationToken);
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
        // If we have a single array items type subschema that matches our keyword
        // then we are a single array items type.
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType))
        {
            arrayItemsType = new(itemsType, isExplicit: false, this);
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
    public bool TryGetTupleType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out TupleTypeDeclaration? tupleType)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Array)
        {
            TypeDeclaration[] tupleTypes =
                typeDeclaration.SubschemaTypeDeclarations
                    .Where(t => t.Key.StartsWith(KeywordPath))
                    .Select(kvp => kvp.Value).ToArray();

            tupleType = new(tupleTypes, true, this);
            return true;
        }

        tupleType = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetNonTupleArrayItemsType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType)
    {
        // If we have a single array items type subschema that matches our keyword
        // then we are a single array items type.
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType))
        {
            arrayItemsType = new(itemsType, isExplicit: true, this);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration item, int tupleIndex)
    {
        return KeywordPathReference.AppendArrayIndexToFragment(tupleIndex).AppendFragment(item.ReducedPathModifier);
    }

    /// <inheritdoc/>
    public string GetPathModifier(ArrayItemsTypeDeclaration item)
    {
        return KeywordPathReference.AppendFragment(item.ReducedPathModifier);
    }
}