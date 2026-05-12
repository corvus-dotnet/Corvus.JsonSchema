// <copyright file="PrefixItemsKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The prefixItems keyword.
/// </summary>
public sealed class PrefixItemsKeyword
    : ISubschemaTypeBuilderKeyword,
      ILocalSubschemaRegistrationKeyword,
      ISubschemaProviderKeyword,
      IArrayValidationKeyword,
      ITupleTypeProviderKeyword
{
    private const string KeywordPath = "#/prefixItems";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private PrefixItemsKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="PrefixItemsKeyword"/> keyword.
    /// </summary>
    public static PrefixItemsKeyword Instance { get; } = new PrefixItemsKeyword();

    /// <inheritdoc />
    public string Keyword => "prefixItems";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "prefixItems"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

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
            ? CoreTypes.Array
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresItemsEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayLength(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresArrayEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).OrderBy(k => k.Key).Select(t => t.Value).ToList();
    }

    /// <inheritdoc/>
    public bool TryGetTupleType(TypeDeclaration typeDeclaration, [MaybeNullWhen(false)] out TupleTypeDeclaration? tupleType)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
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
    public string GetPathModifier(ReducedTypeDeclaration item, int tupleIndex)
    {
        return KeywordPathReference.AppendArrayIndexToFragment(tupleIndex).AppendFragment(item.ReducedPathModifier);
    }
}