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
    public void RegisterLocalSubschema(JsonSchemaRegistry registry, JsonElement schema, JsonReference currentLocation, IVocabulary vocabulary, CancellationToken cancellationToken)
    {
        if (schema.TryGetKeyword(this, out JsonElement value))
        {
            Subschemas.AddSubschemasForMapOfSchemaProperty(registry, this.Keyword, value, currentLocation, vocabulary, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async ValueTask BuildSubschemaTypes(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        if (typeDeclaration.TryGetKeyword(this, out JsonElement value))
        {
            await Subschemas.BuildSubschemaTypesForMapOfSchemaProperty(typeBuilderContext, typeDeclaration, KeywordPathReference, value, cancellationToken);
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
        // Callers pair this list positionally with GetSubschemaTypeDeclarations(), so both must
        // sort identically. That holds because the subschema key is the keyword path terminated
        // with the property name, that property name *is* the regular expression used here, and
        // BOTH sorts are ordinal. A culture-sensitive sort on either side breaks the pairing:
        // linguistic comparison orders punctuation and case differently from ordinal (and
        // differently between ICU and NLS), cross-binding patterns to the wrong subschemas.
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
        return typeDeclaration.SubschemaTypeDeclarations.Where(t => t.Key.StartsWith(KeywordPath)).OrderBy(k => k.Key, StringComparer.Ordinal).Select(t => t.Value).ToList();
    }

    /// <inheritdoc/>
    public bool RequiresObjectEnumeration(TypeDeclaration typeDeclaration) => typeDeclaration.HasKeyword(this);

    /// <inheritdoc/>
    public string GetPathModifier(string pattern, ReducedTypeDeclaration propertyTypeDeclaration)
    {
        return KeywordPathReference.AppendUnencodedPropertyNameToFragment(pattern).AppendFragment(propertyTypeDeclaration.ReducedPathModifier);
    }
}