// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.Draft202012;

/// <summary>
/// The draft 2020-12 schema vocabulary.
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
    /// Gets the singleton instance of the Draft 2020-12 default vocabulary.
    /// </summary>
    public static SchemaVocabulary DefaultInstance { get; } = new SchemaVocabulary(
        [
            Core.Instance,
            Applicator.Instance,
            Content.Instance,
            FormatAnnotation.Instance,
            MetaData.Instance,
            Unevaluated.Instance,
            Validation.Instance,
        ]);

    /// <inheritdoc/>
    public string Uri => "https://json-schema.org/draft/2020-12/schema";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/schema"u8;

    /// <inheritdoc/>
    public IEnumerable<IKeyword> Keywords => this.vocabularies.SelectMany(v => v.Keywords)
        .Union(
        [
            DefinitionsKeyword.Instance,
            DependenciesKeyword.Instance,
            DollarRecursiveRefKeyword.Instance,
            DollarRecursiveRefKeyword.Instance,
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
    /// The core vocabulary for draft 2020-12.
    /// </summary>
    public sealed class Core : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                DollarIdKeyword.Instance,
                DollarSchemaKeyword.Instance,
                DollarRefKeyword.Instance,
                DollarAnchorKeyword.Instance,
                DollarDynamicRefKeyword.Instance,
                DollarDynamicAnchorKeyword.Instance,
                DollarVocabularyKeyword.Instance,
                DollarCommentKeyword.Instance,
                DollarDefsKeyword.Instance,
            ];

        private Core()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2020-12 core vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Core();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/core";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/core"u8;

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
            // TODO: use the generated 2020-12 core validator.
            return true;
        }
    }

    /// <summary>
    /// The applicator vocabulary for draft 2020-12.
    /// </summary>
    public sealed class Applicator : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                PrefixItemsKeyword.Instance,
                ItemsWithSchemaKeyword.Instance,
                ContainsKeyword.Instance,
                AdditionalPropertiesKeyword.Instance,
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
        /// Gets the singleton instance of the Draft 2020-12 applicator vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Applicator();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/applicator";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/applicator"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2020-12 applicator validator.
            return true;
        }

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }
    }

    /// <summary>
    /// The content vocabulary for draft 2020-12.
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
        /// Gets the singleton instance of the Draft 2020-12 content vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Content();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/content";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/content"u8;

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
            // TODO: use the generated 2020-12 content validator.
            return true;
        }
    }

    /// <summary>
    /// The format-annotation vocabulary for draft 2020-12.
    /// </summary>
    public sealed class FormatAnnotation : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                FormatWithAnnotationKeyword.Instance,
            ];

        private FormatAnnotation()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2020-12 format-annotation vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new FormatAnnotation();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/format-annotation";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/format-annotation"u8;

        /// <inheritdoc/>
        public IEnumerable<IKeyword> Keywords => KeywordsBacking;

        /// <inheritdoc/>
        public bool ValidateSchemaInstance(JsonElement schemaInstance)
        {
            // TODO: use the generated 2020-12 format-annotation validator.
            return true;
        }

        /// <inheritdoc/>
        public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
        {
            return null;
        }
    }

    /// <summary>
    /// The format-assertion vocabulary for draft 2020-12.
    /// </summary>
    public sealed class FormatAssertion : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                FormatWithAssertionKeyword.Instance,
            ];

        private FormatAssertion()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2020-12 format-assertion vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new FormatAssertion();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/format-assertion";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/format-assertion"u8;

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
            // TODO: use the generated 2020-12 format-assertion validator.
            return true;
        }
    }

    /// <summary>
    /// The meta-data vocabulary for draft 2020-12.
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
        /// Gets the singleton instance of the Draft 2020-12 meta-data vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new MetaData();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/meta-data";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/meta-data"u8;

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
            // TODO: use the generated 2020-12 meta-data validator.
            return true;
        }
    }

    /// <summary>
    /// The unevaluated vocabulary for draft 2020-12.
    /// </summary>
    public sealed class Unevaluated : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                UnevaluatedItemsKeyword.Instance,
                UnevaluatedPropertiesKeyword.Instance,
            ];

        private Unevaluated()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2020-12 unevaluated vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Unevaluated();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/unevaluated";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/unevaluated"u8;

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
            // TODO: use the generated 2020-12 unevaluated validator.
            return true;
        }
    }

    /// <summary>
    /// The validation vocabulary for draft 2020-12.
    /// </summary>
    public sealed class Validation : IVocabulary
    {
        private static readonly IKeyword[] KeywordsBacking =
            [
                TypeKeyword.Instance,
                ConstKeyword.Instance,
                EnumKeyword.Instance,
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
            ];

        private Validation()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the Draft 2020-12 validation vocabulary.
        /// </summary>
        public static IVocabulary Instance { get; } = new Validation();

        /// <inheritdoc/>
        public string Uri => "https://json-schema.org/draft/2020-12/vocab/validation";

        /// <inheritdoc/>
        public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/validation"u8;

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
            // TODO: use the generated 2020-12 unevaluated validator.
            return true;
        }
    }
}