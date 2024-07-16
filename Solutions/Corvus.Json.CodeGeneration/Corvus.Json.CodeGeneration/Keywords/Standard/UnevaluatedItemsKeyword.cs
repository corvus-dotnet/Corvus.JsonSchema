// <copyright file="UnevaluatedItemsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The unevaluatedItems keyword.
/// </summary>
public sealed class UnevaluatedItemsKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        ISubschemaProviderKeyword,
        IArrayValidationKeyword,
        IUnevaluatedArrayItemsTypeProviderKeyword
{
    private const string KeywordPath = "#/unevaluatedItems";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private UnevaluatedItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="UnevaluatedItemsKeyword"/> keyword.
    /// </summary>
    public static UnevaluatedItemsKeyword Instance { get; } = new UnevaluatedItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "unevaluatedItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "unevaluatedItems"u8;

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
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Array
            : CoreTypes.None;

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            return [value];
        }

        return [];
    }

    /// <inheritdoc/>
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public bool RequiresArrayLength(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public bool TryGetArrayItemsType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? arrayItemsType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            // UnevaluatedItems is not an explicit definition of the array type - it is a fallback
            arrayItemsType = new(value, false, this);
            return true;
        }

        arrayItemsType = null;
        return false;
    }

    /// <inheritdoc/>
    public bool TryGetUnevaluatedArrayItemsType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out ArrayItemsTypeDeclaration? unevaluatedArrayItemsType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            // UnevaluatedItems *is* an explicit definition of the unevaluated items type.
            unevaluatedArrayItemsType = new(value, true, this);
            return true;
        }

        unevaluatedArrayItemsType = null;
        return false;
    }

    /// <inheritdoc/>
    public string GetPathModifier(ArrayItemsTypeDeclaration item)
    {
        return KeywordPathReference.AppendFragment(item.ReducedPathModifier);
    }
}