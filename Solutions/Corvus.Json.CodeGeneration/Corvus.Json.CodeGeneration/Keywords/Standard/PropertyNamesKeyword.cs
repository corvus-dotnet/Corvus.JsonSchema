// <copyright file="PropertyNamesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The propertyNames keyword.
/// </summary>
public sealed class PropertyNamesKeyword
    :   ISubschemaTypeBuilderKeyword,
        ILocalSubschemaRegistrationKeyword,
        ISubschemaProviderKeyword,
        IObjectValidationKeyword
{
    private const string KeywordPath = "#/propertyNames";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private PropertyNamesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="PropertyNamesKeyword"/> keyword.
    /// </summary>
    public static PropertyNamesKeyword Instance { get; } = new PropertyNamesKeyword();

    /// <inheritdoc />
    public string Keyword => "propertyNames";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "propertyNames"u8;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

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
            ? CoreTypes.Object
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
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;
}