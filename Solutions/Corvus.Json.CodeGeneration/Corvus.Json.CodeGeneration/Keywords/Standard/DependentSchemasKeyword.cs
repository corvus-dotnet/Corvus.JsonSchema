// <copyright file="DependentSchemasKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The dependentSchemas keyword.
/// </summary>
public sealed class DependentSchemasKeyword
    : ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword, IPropertyProviderKeyword, IObjectValidationKeyword
{
    private const string KeywordPath = "#/dependentSchemas";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private DependentSchemasKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DependentSchemasKeyword"/> keyword.
    /// </summary>
    public static DependentSchemasKeyword Instance { get; } = new DependentSchemasKeyword();

    /// <inheritdoc />
    public string Keyword => "dependentSchemas";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "dependentSchemas"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.Default;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForMapOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForMapOfSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, value).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public bool CanReduce(in JsonElement schemaValue) => Reduction.CanReduceNonReducingKeyword(schemaValue, this.KeywordUtf8);

    /// <inheritdoc />
    public void CollectProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> visitedTypeDeclarations, bool treatRequiredAsOptional)
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
                true);
        }
    }

    /// <inheritdoc />
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);
}