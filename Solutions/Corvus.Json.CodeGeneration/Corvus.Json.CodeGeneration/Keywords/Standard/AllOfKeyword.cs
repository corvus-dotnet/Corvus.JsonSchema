﻿// <copyright file="AllOfKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The allOf keyword.
/// </summary>
public sealed class AllOfKeyword
    : IPropertyProviderKeyword, IAllOfSubschemaValidationKeyword, ILocalSubschemaRegistrationKeyword, ISubschemaTypeBuilderKeyword
{
    private const string KeywordPath = "#/allOf";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private AllOfKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="AllOfKeyword"/> keyword.
    /// </summary>
    public static AllOfKeyword Instance { get; } = new AllOfKeyword();

    /// <inheritdoc />
    public string Keyword => "allOf";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "allOf"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.Default;

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

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public void CollectProperties(
        TypeDeclaration source,
        TypeDeclaration target,
        HashSet<TypeDeclaration> visitedTypeDeclarations,
        bool treatRequiredAsOptional)
    {
        foreach (TypeDeclaration subschema in
            source.SubschemaTypeDeclarations
                .Where(kvp => kvp.Key.StartsWith(KeywordPath))
                .Select(kvp => kvp.Value))
        {
            PropertyProvider.CollectProperties(
                subschema,
                target,
                visitedTypeDeclarations,
                treatRequiredAsOptional);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).Select(t => t.Value).ToList();
    }

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