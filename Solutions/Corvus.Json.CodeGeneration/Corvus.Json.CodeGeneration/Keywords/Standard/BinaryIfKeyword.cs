// <copyright file="BinaryIfKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The if keyword with a corresponding then keyword.
/// </summary>
public sealed class BinaryIfKeyword : ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword, IBinaryIfValidationKeyword
{
    private const string KeywordPath = "#/if";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private BinaryIfKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="BinaryIfKeyword"/> keyword.
    /// </summary>
    public static BinaryIfKeyword Instance { get; } = new BinaryIfKeyword();

    /// <inheritdoc />
    public string Keyword => "if";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "if"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Composition;

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
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc/>
    public bool TryGetIfDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? ifDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPathReference, out ifDeclaration);
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
}