// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.OpenApi30;

/// <summary>
/// The openApi30 schema vocabulary.
/// </summary>
internal sealed class SchemaVocabulary : IVocabulary
{
    private static readonly IKeyword[] KeywordsBacking =
    [
        TitleKeyword.Instance,
        MultipleOfKeyword.Instance,
        MaximumWithBooleanExclusiveKeyword.Instance,
        ExclusiveMaximumBooleanKeyword.Instance,
        MinimumWithBooleanExclusiveKeyword.Instance,
        ExclusiveMaximumBooleanKeyword.Instance,
        MaxLengthKeyword.Instance,
        MinLengthKeyword.Instance,
        PatternKeyword.Instance,
        MaxItemsKeyword.Instance,
        MinItemsKeyword.Instance,
        UniqueItemsKeyword.Instance,
        MaxPropertiesKeyword.Instance,
        MinPropertiesKeyword.Instance,
        RequiredKeyword.Instance,
        EnumKeyword.Instance,
        TypeKeyword.Instance,
        NotKeyword.Instance,
        AllOfKeyword.Instance,
        OneOfKeyword.Instance,
        AnyOfKeyword.Instance,
        ItemsWithSchemaKeyword.Instance,
        PropertiesKeyword.Instance,
        AdditionalPropertiesKeyword.Instance,
        DescriptionKeyword.Instance,
        FormatWithAssertionKeyword.Instance,
        DefaultKeyword.Instance,
        NullableKeyword.Instance,
        DiscriminatorKeyword.Instance,
        ReadOnlyKeyword.Instance,
        WriteOnlyKeyword.Instance,
        ExampleKeyword.Instance,
        ExternalDocsKeyword.Instance,
        DeprecatedKeyword.Instance,
        XmlKeyword.Instance,
    ];

    /// <summary>
    /// Gets the singleton instance of the OpenApi30 default vocabulary.
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