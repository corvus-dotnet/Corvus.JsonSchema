// <copyright file="NotKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The not keyword.
/// </summary>
public sealed class NotKeyword
    : ILocalSubschemaRegistrationKeyword, INotValidationKeyword
{
    private const string KeywordPath = "#/not";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private NotKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="NotKeyword"/> keyword.
    /// </summary>
    public static NotKeyword Instance { get; } = new NotKeyword();

    /// <inheritdoc />
    public string Keyword => "not";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "not"u8;

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
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) => CoreTypes.None;

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            return [value];
        }

        return [];
    }

    /// <inheritdoc />
    public bool TryGetNotType(
        TypeDeclaration typeDeclaration,
        [MaybeNullWhen(false)] out ReducedTypeDeclaration? notType)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? itemsType))
        {
            notType = itemsType.ReducedTypeDeclaration();
            return true;
        }

        notType = null;
        return false;
    }

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration notType)
    {
        return KeywordPathReference.AppendFragment(notType.ReducedPathModifier);
    }
}