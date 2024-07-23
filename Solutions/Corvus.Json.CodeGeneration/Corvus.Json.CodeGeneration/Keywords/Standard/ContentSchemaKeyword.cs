// <copyright file="ContentSchemaKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The contentSchema keyword.
/// </summary>
public sealed class ContentSchemaKeyword
    : ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword
{
    private const string KeywordPath = "#/contentSchema";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private ContentSchemaKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ContentSchemaKeyword"/> keyword.
    /// </summary>
    public static ContentSchemaKeyword Instance { get; } = new ContentSchemaKeyword();

    /// <inheritdoc />
    public string Keyword => "contentSchema";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "contentSchema"u8;

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
            ? CoreTypes.String
            : CoreTypes.None;
}