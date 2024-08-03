// <copyright file="SchemaVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration.CorvusVocabulary;

/// <summary>
/// The corvus schema vocabulary.
/// </summary>
public sealed class SchemaVocabulary : IVocabulary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaVocabulary"/> class.
    /// </summary>
    internal SchemaVocabulary()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the Corvus default vocabulary.
    /// </summary>
    public static SchemaVocabulary DefaultInstance { get; } = new SchemaVocabulary();

    /// <inheritdoc/>
    public string Uri => "https://endjin.com/corvus/json-schema/2020-12/vocab/corvus-extensions";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> UriUtf8 => "https://json-schema.org/draft/2020-12/vocab/corvus-extensions"u8;

    /// <inheritdoc/>
    public IEnumerable<IKeyword> Keywords =>
        [
            DollarCorvusTypeNameKeyword.Instance,
        ];

    /// <inheritdoc/>
    public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
    {
        return null;
    }

    /// <inheritdoc/>
    public bool ValidateSchemaInstance(JsonElement schemaInstance)
    {
        // There's nothing for us to do just yet!
        return true;
    }
}