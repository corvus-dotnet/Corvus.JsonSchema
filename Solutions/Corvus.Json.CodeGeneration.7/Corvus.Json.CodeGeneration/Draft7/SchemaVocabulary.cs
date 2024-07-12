// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.Draft7;

/// <summary>
/// The draft 7 schema vocabulary.
/// </summary>
internal sealed class SchemaVocabulary : IVocabulary
{
    private static readonly IKeyword[] KeywordsBacking =
    [
        DollarIdKeyword.Instance,
        DollarSchemaKeyword.Instance,
        DollarRefKeyword.Instance,
        DollarCommentKeyword.Instance,
        TitleKeyword.Instance,
        DescriptionKeyword.Instance,
        DefaultKeyword.Instance,
        ReadOnlyKeyword.Instance,
        WriteOnlyKeyword.Instance,
        ExamplesKeyword.Instance,
        MultipleOfKeyword.Instance,
        MaximumKeyword.Instance,
        ExclusiveMaximumKeyword.Instance,
        MinimumKeyword.Instance,
        ExclusiveMinimumKeyword.Instance,
        MaxLengthKeyword.Instance,
        MinLengthKeyword.Instance,
        PatternKeyword.Instance,
        AdditionalItemsKeyword.Instance,
        ItemsWithSchemaOrArrayOfSchemaKeyword.Instance,
        MaxItemsKeyword.Instance,
        MinItemsKeyword.Instance,
        UniqueItemsKeyword.Instance,
        ContainsKeyword.Instance,
        MaxPropertiesKeyword.Instance,
        MinPropertiesKeyword.Instance,
        RequiredKeyword.Instance,
        DefinitionsKeyword.Instance,
        PropertiesKeyword.Instance,
        PatternPropertiesKeyword.Instance,
        DependenciesKeyword.Instance,
        PropertyNamesKeyword.Instance,
        ConstKeyword.Instance,
        EnumKeyword.Instance,
        TypeKeyword.Instance,
        FormatWithAssertionKeyword.Instance,
        ContentMediaTypePre201909Keyword.Instance,
        ContentEncodingKeyword.Instance,
        TernaryIfKeyword.Instance,
        ThenKeyword.Instance,
        ElseKeyword.Instance,
        AllOfKeyword.Instance,
        AnyOfKeyword.Instance,
        OneOfKeyword.Instance,
        NotKeyword.Instance,
    ];

    /// <summary>
    /// Gets the singleton instance of the Draft 7 default vocabulary.
    /// </summary>
    public static SchemaVocabulary DefaultInstance { get; } = new SchemaVocabulary();

    /// <inheritdoc/>
    public string Uri => "http://json-schema.org/draft-07/schema#";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> UriUtf8 => "http://json-schema.org/draft-07/schema#"u8;

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