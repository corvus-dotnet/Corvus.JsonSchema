// <copyright file="ThenKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The then keyword.
/// </summary>
public sealed class ThenKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        IPropertyProviderKeyword,
        IIfThenValidationKeyword
{
    private const string KeywordPath = "#/then";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private ThenKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="ThenKeyword"/> keyword.
    /// </summary>
    public static ThenKeyword Instance { get; } = new ThenKeyword();

    /// <inheritdoc />
    public string Keyword => "then";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "then"u8;

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

    /// <inheritdoc/>
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
    {
        if (source.SubschemaTypeDeclarations.TryGetValue(KeywordPath, out TypeDeclaration? thenTypeDeclaration))
        {
            PropertyProvider.CollectProperties(
                thenTypeDeclaration,
                target,
                visitedTypeDeclarations,
                true);
        }
    }

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

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration subschemaType)
    {
        return KeywordPathReference.AppendFragment(subschemaType.ReducedPathModifier);
    }

    /// <inheritdoc/>
    public bool TryGetThenDeclaration(TypeDeclaration typeDeclaration, out TypeDeclaration? thenDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.TryGetValue(KeywordPathReference, out thenDeclaration);
    }
}