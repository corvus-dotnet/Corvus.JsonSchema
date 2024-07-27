// <copyright file="ElseKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The else keyword.
/// </summary>
public sealed class ElseKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        IPropertyProviderKeyword,
        ITernaryIfElseValidationKeyword
{
    private const string KeywordPath = "#/else";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private ElseKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ElseKeyword"/> keyword.
    /// </summary>
    public static ElseKeyword Instance { get; } = new ElseKeyword();

    /// <inheritdoc />
    public string Keyword => "else";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "else"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.Default;

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
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
    {
        if (source.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? elseTypeDeclaration))
        {
            PropertyProvider.CollectProperties(
                elseTypeDeclaration,
                target,
                visitedTypeDeclarations,
                true);
        }
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
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? value))
        {
            return [value];
        }

        return [];
    }

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration subschemaType)
    {
        return KeywordPathReference.AppendFragment(subschemaType.ReducedPathModifier);
    }

    /// <inheritdoc/>
    public bool TryGetElseDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? elseDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPathReference, out elseDeclaration);
    }
}