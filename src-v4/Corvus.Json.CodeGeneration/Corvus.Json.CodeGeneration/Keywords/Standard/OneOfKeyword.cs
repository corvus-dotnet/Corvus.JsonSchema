// <copyright file="OneOfKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The oneOf keyword.
/// </summary>
public sealed class OneOfKeyword
    : IOneOfSubschemaValidationKeyword, ILocalSubschemaRegistrationKeyword, ISubschemaTypeBuilderKeyword, ICompositionKeyword
{
    private const string KeywordPath = "#/oneOf";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private OneOfKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="OneOfKeyword"/> keyword.
    /// </summary>
    public static OneOfKeyword Instance { get; } = new OneOfKeyword();

    /// <inheritdoc />
    public string Keyword => "oneOf";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "oneOf"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Composition;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForArrayOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForArrayOfSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, value, cancellationToken);
        }
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
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).OrderBy(k => k.Key).Select(t => t.Value).ToList();
    }

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration subschema, int index)
    {
        return KeywordPathReference.AppendArrayIndexToFragment(index).AppendFragment(subschema.ReducedPathModifier);
    }
}