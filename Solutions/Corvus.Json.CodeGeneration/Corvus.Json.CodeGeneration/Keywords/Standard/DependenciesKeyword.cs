// <copyright file="DependenciesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The dependencies keyword.
/// </summary>
public sealed class DependenciesKeyword
    : ISubschemaTypeBuilderKeyword, ILocalSubschemaRegistrationKeyword, IPropertyProviderKeyword, IObjectPropertyDependentSchemasValidationKeyword
{
    private const string KeywordPath = "#/dependencies";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private DependenciesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="DependenciesKeyword"/> keyword.
    /// </summary>
    public static DependenciesKeyword Instance { get; } = new DependenciesKeyword();

    /// <inheritdoc />
    public string Keyword => "dependencies";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "dependencies"u8;

    /// <inheritdoc />
    public uint PropertyProviderPriority => PropertyProviderPriorities.Default;

    /// <inheritdoc/>
    public uint ValidationPriority => ValidationPriorities.Default;

    /// <inheritdoc />
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForMapOfSchemaIfValueIsSchemaLikeProperty(registry, this.Keyword, value, currentLocation, vocabulary);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForMapOfSchemaIfValueIsSchemaLikeProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, value).ConfigureAwait(false);
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
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) =>
        typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
        (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False);

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration typeDeclaration, string propertyName)
    {
        return KeywordPathReference.AppendFragment(typeDeclaration.ReducedPathModifier).AppendUnencodedPropertyNameToFragment(propertyName);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<DependentSchemaDeclaration> GetDependentSchemaDeclarations(TypeDeclaration typeDeclaration)
    {
        List<DependentSchemaDeclaration> declarations = [];

        if (typeDeclaration.TryGetKeyword(this, out JsonElement value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in value.EnumerateObject())
            {
                JsonReference subschemaLocation = KeywordPathReference.AppendUnencodedPropertyNameToFragment(property.Name);
                if (typeDeclaration.SubschemaTypeDeclarations.TryGetValue(subschemaLocation.ToString(), out TypeDeclaration? subschemaTypeDeclaration))
                {
                    declarations.Add(new(this, property.Name, subschemaTypeDeclaration));
                }
            }
        }

        return declarations;
    }
}