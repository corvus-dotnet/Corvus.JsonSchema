// <copyright file="AdditionalItemsKeyword.cs" company="Endjin Limited">
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
    private const string KeywordPath = "#/aditionalItems";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private AdditionalItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="AdditionalItemsKeyword"/> keyword.
    /// </summary>
    public static AdditionalItemsKeyword Instance { get; } = new AdditionalItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "aditionalItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "aditionalItems"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        // The 2019-09 and earlier additionalItems keyword only applies if the schema is also has an array-of-schema Items keyword.
        if (schema.TryGetKeyword(this, out JsonElement value) &&
            schema.TryGetKeyword(ItemsWithSchemaOrArrayOfSchemaKeyword.Instance, out JsonElement itemsKeywordValue) &&
            itemsKeywordValue.ValueKind == JsonValueKind.Array)
        {
            Subschemas.AddSubschemasForSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        // The 2019-09 and earlier additionalItems keyword only applies if the schema is also has an array-of-schema Items keyword.
        if (this.HasKeyword(typeDeclaration))
        {
            await Subschemas.BuildSubschemaTypesForSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference).ConfigureAwait(false);
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
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType))
        {
            arrayItemsType = new(itemsType, isExplicit: true);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc/>
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    private bool HasKeyword(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.HasKeyword(this) &&
                    typeDeclaration.TryGetKeyword(ItemsWithSchemaOrArrayOfSchemaKeyword.Instance, out JsonElement itemsKeywordValue) &&
                    itemsKeywordValue.ValueKind == JsonValueKind.Array;
    }
}