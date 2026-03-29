// <copyright file="NullVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// This is a special do-nothing vocabulary containing no keywords,
/// used by built-in types.
/// </summary>
public sealed class NullVocabulary : IVocabulary
{
    /// <summary>
    /// Gets the singleton instance of the null vocabulary.
    /// </summary>
    public static NullVocabulary Instance { get; } = new NullVocabulary();

    /// <inheritdoc/>
    public string Uri => "corvus://null-vocabulary";

    /// <inheritdoc/>
    public ReadOnlySpan<byte> UriUtf8 => "corvus://null-vocabulary"u8;

    /// <inheritdoc/>
    public IEnumerable<IKeyword> Keywords => [];

    /// <inheritdoc/>
    public bool ValidateSchemaInstance(JsonElement schema) => true;

    /// <inheritdoc/>
    public JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath)
    {
        return null;
    }
}