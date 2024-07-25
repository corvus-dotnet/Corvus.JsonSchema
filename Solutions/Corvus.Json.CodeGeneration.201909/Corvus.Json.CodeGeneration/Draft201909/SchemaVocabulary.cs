// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.Draft201909;

/// <summary>
/// The draft 2019-09 schema vocabulary.
/// </summary>
internal sealed class SchemaVocabulary : IVocabulary
{
    private readonly IVocabulary[] vocabularies;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaVocabulary"/> class.
    /// </summary>
    /// <param name="vocabularies">The composite vocabularies.</param>
    internal SchemaVocabulary(
        IVocabulary[] vocabularies)
    {
        this.vocabularies = vocabularies;
    }

    /// <summary>
    /// Gets the singleton instance of the Draft 2019-09 default vocabulary.
    /// </summary>
    public static SchemaVocabulary DefaultInstance { get; } = new SchemaVocabulary(
        [
            Core.Instance,
            Applicator.Instance,
            Content.Instance,
            Format.Instance,
            MetaData.Instance,
            Validation.Instance,
        ]);

    /// <inheritdoc/>
    public string Uri => "https://json-schema.org/draft/2019-09/schema";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/schema"u8;

    /// <inheritdoc/>
    public IEnumerable<IKeyword> Keywords => this.vocabularies.SelectMany(v => v.Keywords)
        .Union(
        [
            DefinitionsKeyword.Instance,
            DependenciesKeyword.Instance,
        ]);

    /// <inheritdoc/>
    public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
    {
        foreach (IVocabulary vocabulary in this.vocabularies)
        {
            JsonDocument? optionalReferenceSchemaInstance = vocabulary.BuildReferenceSchemaInstance(jsonSchemaPath);
            if (optionalReferenceSchemaInstance is JsonDocument referenceSchemaInstance)
            {
                return referenceSchemaInstance;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public bool ValidateSchemaInstance(JsonElement schemaInstance)
    {
        return this.vocabularies.All(v => v.ValidateSchemaInstance(schemaInstance));
    }

    /// <summary>
    /// The core vocabulary for draft 2019-09.
    /// </summary>
    public sealed class Core : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                DollarIdKeyword.Instance,
                DollarSchemaKeyword.Instance,
                DollarAnchorKeyword.Instance,
                DollarRefKeyword.Instance,
                DollarRecursiveRefKeyword.Instance,
                DollarRecursiveAnchorKeyword.Instance,
                DollarVocabularyKeyword.Instance,
                DollarCommentKeyword.Instance,
                DollarDefsKeyword.Instance,
            ];

        private Core()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2019-09 core vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Core();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2019-09/vocab/core";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/vocab/core"u8;

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
            // TODO: use the generated 2019-09 core validator.
            return true;
        }
    }

    /// <summary>
    /// The applicator vocabulary for draft 2019-09.
    /// </summary>
    public sealed class Applicator : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                AdditionalItemsKeyword.Instance,
                UnevaluatedItemsKeyword.Instance,
                ItemsWithSchemaOrArrayOfSchemaKeyword.Instance,
                ContainsKeyword.Instance,
                AdditionalPropertiesKeyword.Instance,
                UnevaluatedPropertiesKeyword.Instance,
                PropertiesKeyword.Instance,
                PatternPropertiesKeyword.Instance,
                DependentSchemasKeyword.Instance,
                PropertyNamesKeyword.Instance,
                TernaryIfKeyword.Instance,
                ThenKeyword.Instance,
                ElseKeyword.Instance,
                AllOfKeyword.Instance,
                AnyOfKeyword.Instance,
                OneOfKeyword.Instance,
                NotKeyword.Instance,
            ];

        private Applicator()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2019-09 applicator vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Applicator();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2019-09/vocab/applicator";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/vocab/applicator"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2019-09 applicator validator.
            return true;
        }

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }
    }

    /// <summary>
    /// The content vocabulary for draft 2019-09.
    /// </summary>
    public sealed class Content : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                ContentEncodingKeyword.Instance,
                ContentMediaTypeKeyword.Instance,
                ContentSchemaKeyword.Instance,
            ];

        private Content()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2019-09 content vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Content();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2019-09/vocab/content";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/vocab/content"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2019-09 content validator.
            return true;
        }
    }

    /// <summary>
    /// The format-annotation vocabulary for draft 2019-09.
    /// </summary>
    public sealed class Format : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                FormatWithAssertionKeyword.Instance,
            ];

        private Format()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2019-09 format-annotation vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Format();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2019-09/vocab/format-annotation";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/vocab/format-annotation"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2019-09 format-annotation validator.
            return true;
        }

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }
    }

    /// <summary>
    /// The meta-data vocabulary for draft 2019-09.
    /// </summary>
    public sealed class MetaData : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                TitleKeyword.Instance,
                DescriptionKeyword.Instance,
                DefaultKeyword.Instance,
                DeprecatedKeyword.Instance,
                ReadOnlyKeyword.Instance,
                WriteOnlyKeyword.Instance,
                ExamplesKeyword.Instance,
            ];

        private MetaData()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2019-09 meta-data vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new MetaData();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2019-09/vocab/meta-data";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/vocab/meta-data"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2019-09 meta-data validator.
            return true;
        }
    }

    /// <summary>
    /// The validation vocabulary for draft 2019-09.
    /// </summary>
    public sealed class Validation : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                MultipleOfKeyword.Instance,
                MaximumKeyword.Instance,
                ExclusiveMaximumKeyword.Instance,
                MinimumKeyword.Instance,
                ExclusiveMinimumKeyword.Instance,
                MaxLengthKeyword.Instance,
                MinLengthKeyword.Instance,
                PatternKeyword.Instance,
                MaxItemsKeyword.Instance,
                MinItemsKeyword.Instance,
                UniqueItemsKeyword.Instance,
                MaxContainsKeyword.Instance,
                MinContainsKeyword.Instance,
                MaxPropertiesKeyword.Instance,
                MinPropertiesKeyword.Instance,
                RequiredKeyword.Instance,
                DependentRequiredKeyword.Instance,
                ConstKeyword.Instance,
                EnumKeyword.Instance,
                TypeKeyword.Instance,
            ];

        private Validation()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2019-09 validation vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Validation();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2019-09/vocab/validation";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2019-09/vocab/validation"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2019-09 unevaluated validator.
            return true;
        }
    }
}