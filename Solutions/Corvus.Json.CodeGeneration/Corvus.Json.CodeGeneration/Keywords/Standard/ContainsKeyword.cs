// <copyright file="ContainsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The contains keyword.
/// </summary>
public sealed class ContainsKeyword
    : ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword, IArrayContainsValidationKeyword
{
    private const string KeywordPath = "#/contains";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private ContainsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ContainsKeyword"/> keyword.
    /// </summary>
    public static ContainsKeyword Instance { get; } = new ContainsKeyword();

    /// <inheritdoc />
    public string Keyword => "contains";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "contains"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

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
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayLength(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public bool TryGetContainsItemType(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out ArrayItemsTypeDeclaration? itemsTypeDeclaration)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            itemsTypeDeclaration = new(value, true);
            return true;
        }

        itemsTypeDeclaration = null;
        return false;
    }
}