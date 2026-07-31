// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.OpenApi20;

/// <summary>
/// The openApi20 (Swagger) schema vocabulary.
/// </summary>
/// <remarks>
/// <para>
/// Draft-04 based, like the OpenApi30 vocabulary, with the OpenAPI 2.0
/// fixed fields and the <c>x-nullable</c> vendor convention. <c>oneOf</c>, <c>anyOf</c>,
/// <c>not</c>, <c>nullable</c>, and <c>definitions</c> are included as a practical
/// superset of the specification: real-world Swagger documents use them, and excluding
/// them would silently drop their subschemas (and any <c>$ref</c> targets inside them)
/// from the generated type graph.
/// </para>
/// <para>
/// <see cref="RequiredArrayOnlyKeyword"/> is used instead of <see cref="RequiredKeyword"/>
/// because the code generator points the type builder directly at Parameter Objects,
/// where <c>required</c> is a boolean; the array-only variant keeps that form inert.
/// <see cref="ItemsWithSchemaOrArrayOfSchemaKeyword"/> is used because the normative
/// Swagger 2.0 metaschema admits both the single-schema and tuple forms of <c>items</c>.
/// </para>
/// </remarks>
internal sealed class SchemaVocabulary : IVocabulary
{
    private static readonly IKeyword[] KeywordsBacking =
    [
        TitleKeyword.Instance,
        DefinitionsKeyword.Instance,
        MultipleOfKeyword.Instance,
        DollarRefHidesSiblingsKeyword.Instance,
        MaximumKeyword.Instance,
        ExclusiveMinimumBooleanKeyword.Instance,
        MinimumKeyword.Instance,
        ExclusiveMaximumBooleanKeyword.Instance,
        MaxLengthKeyword.Instance,
        MinLengthKeyword.Instance,
        PatternKeyword.Instance,
        MaxItemsKeyword.Instance,
        MinItemsKeyword.Instance,
        UniqueItemsKeyword.Instance,
        MaxPropertiesKeyword.Instance,
        MinPropertiesKeyword.Instance,
        RequiredArrayOnlyKeyword.Instance,
        EnumKeyword.Instance,
        TypeKeyword.Instance,
        NotKeyword.Instance,
        AllOfKeyword.Instance,
        OneOfKeyword.Instance,
        AnyOfKeyword.Instance,
        ItemsWithSchemaOrArrayOfSchemaKeyword.Instance,
        PropertiesKeyword.Instance,
        AdditionalPropertiesKeyword.Instance,
        DescriptionKeyword.Instance,
        FormatWithAssertionKeyword.Instance,
        DefaultKeyword.Instance,
        NullableKeyword.Instance,
        XNullableKeyword.Instance,
        DiscriminatorKeyword.Instance,
        ReadOnlyKeyword.Instance,
        ExampleKeyword.Instance,
        ExternalDocsKeyword.Instance,
        XmlKeyword.Instance,
    ];

    /// <summary>
    /// Gets the singleton instance of the OpenApi20 default vocabulary.
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