// <copyright file="PatternPropertiesKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// The patternProperties keyword.
/// </summary>
public sealed class PatternPropertiesKeyword
    : ISubschemaTypeBuilderKeyword,
      ILocalSubschemaRegistrationKeyword,
      IObjectPatternPropertyValidationKeyword
{
    private const string KeywordPath = "#/patternProperties";
    private static readonly JsonReference KeywordPathReference = new(KeywordPath);

    private PatternPropertiesKeyword()
    {
    }

    /// <summary>
    /// Gets an instance of the <see cref="PatternPropertiesKeyword"/> keyword.
    /// </summary>
    public static PatternPropertiesKeyword Instance { get; } = new PatternPropertiesKeyword();

    /// <inheritdoc />
    public string Keyword => "patternProperties";

    /// <inheritdoc />
    public ReadOnlySpan<byte> KeywordUtf8 => "patternProperties"u8;

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
    public CoreTypes ImpliesCoreTypes(TypeDeclaration typeDeclaration) =>
        typeDeclaration.HasKeyword(this)
            ? CoreTypes.Object
            : CoreTypes.None;

    /// <inheritdoc/>
    public bool RequiresPropertyCount(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool RequiresPropertyEvaluationTracking(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool TryGetValidationRegularExpressions(TypeDeclaration typeDeclaration, [NotNullWhen(true)] out IReadOnlyList<string>? regexes)
    {
        // You, like me, may have been wondering how the TryGetValidationRegularExpressions()  and the SubschemaTypeDeclarations()
        // manage to magicaly produce collections that are ordered in the same way.
        // Well, that's easy! The SubschemaTypeDeclarations() method is using the same ordering as the TryGetValidationRegularExpressions()
        // because the keyword path for the subschema is terminated with the property name, and that property name *is* the regular
        // expresion used here. So they sort the same.
        List<string>? regexBuilder;

        if (typeDeclaration.TryGetKeyword(this, out JsonElement regexMap) &&
            regexMap.ValueKind == JsonValueKind.Object)
        {
            regexBuilder = [];
            foreach (JsonProperty property in regexMap.EnumerateObject())
            {
                regexBuilder.Add(property.Name);
            }

            regexBuilder.Sort(StringComparer.Ordinal);
            regexes = regexBuilder;
            return true;
        }

        regexes = null;
        return false;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetSubschemaTypeDeclarations(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).OrderBy(k => k.Key).Select(t => t.Value).ToList();
    }

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public string GetPathModifier(ReducedTypeDeclaration propertyTypeDeclaration)
    {
        return KeywordPathReference.AppendFragment(propertyTypeDeclaration.ReducedPathModifier);
    }
}