// <copyright file="AnyOfKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The anyOf keyword.
/// </summary>
public sealed class AnyOfKeyword
    : IAnyOfSubschemaValidationKeyword, ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword, ICompositionKeyword
{
    private const string KeywordPath = "#/anyOf";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private AnyOfKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="AnyOfKeyword"/> keyword.
    /// </summary>
    public static AnyOfKeyword Instance { get; } = new AnyOfKeyword();

    /// <inheritdoc />
    public string Keyword => "anyOf";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "anyOf"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Composition;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForArrayOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForArrayOfSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, value).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).OrderBy(k => k.Key).Select(t => t.Value).ToList();
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? Composition.UnionImpliesCoreTypeForSubschema(
                typeDeclaration,
                KeywordPath,
                CoreTypes.None)
            : CoreTypes.None;

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration subschema, int index)
    {
        return KeywordPathReference.AppendArrayIndexToFragment(index).AppendFragment(subschema.ReducedPathModifier);
    }
}