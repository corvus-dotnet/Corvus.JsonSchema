﻿// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.Draft4;

/// <summary>
/// The draft 4 schema vocabulary.
/// </summary>
internal sealed class SchemaVocabulary : IVocabulary
{
    private static readonly IKeyword[] KeywordsBacking =
    [
        IdKeyword.Instance,
        DollarRefHidesSiblingsKeyword.Instance,
        DollarSchemaKeyword.Instance,
        TitleKeyword.Instance,
        DescriptionKeyword.Instance,
        DefaultKeyword.Instance,
        MultipleOfKeyword.Instance,
        MaximumKeyword.Instance,
        ExclusiveMaximumBooleanKeyword.Instance,
        MinimumKeyword.Instance,
        ExclusiveMinimumBooleanKeyword.Instance,
        MaxLengthKeyword.Instance,
        MinLengthKeyword.Instance,
        PatternKeyword.Instance,
        AdditionalItemsKeyword.Instance,
        ItemsWithSchemaOrArrayOfSchemaKeyword.Instance,
        MaxItemsKeyword.Instance,
        MinItemsKeyword.Instance,
        UniqueItemsKeyword.Instance,
        MaxPropertiesKeyword.Instance,
        MinPropertiesKeyword.Instance,
        RequiredKeyword.Instance,
        AdditionalPropertiesKeyword.Instance,
        DefinitionsKeyword.Instance,
        PropertiesKeyword.Instance,
        PatternPropertiesKeyword.Instance,
        DependenciesKeyword.Instance,
        EnumKeyword.Instance,
        TypeKeyword.Instance,
        FormatWithAssertionKeyword.Instance,
        AllOfKeyword.Instance,
        AnyOfKeyword.Instance,
        OneOfKeyword.Instance,
        NotKeyword.Instance,
    ];

    /// <summary>
    /// Gets the singleton instance of the Draft 4 default vocabulary.
    /// </summary>
    public static SchemaVocabulary DefaultInstance { get; } = new SchemaVocabulary();

    /// <inheritdoc/>
    public string Uri => "http://json-schema.org/draft-04/schema#";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> UriUtf8 => "http://json-schema.org/draft-04/schema#"u8;

    /// <inheritdoc/>
    public IEnumerable<IKeyword> Keywords => KeywordsBacking;

    /// <inheritdoc/>
    public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
    {
        return JsonDocument.Parse(
            $$"""
            {
                "$ref": "{{jsonSchemaPath}}"
            }
            """);
    }

    /// <inheritdoc/>
    public bool ValidateSchemaInstance(JsonElement schemaInstance)
    {
        // TODO: Validate using the generate types
        return true;
    }
}