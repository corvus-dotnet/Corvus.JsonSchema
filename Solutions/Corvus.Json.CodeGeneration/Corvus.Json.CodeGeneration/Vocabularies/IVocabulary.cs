// <copyright file="IVocabulary.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Represents a JSON Schema vocabulary.
/// </summary>
public interface IVocabulary
{
    /// <summary>
    /// Gets the Uri which uniquely identifies this vocabulary.
    /// </summary>
    string Uri { get; }

    /// <summary>
    /// Gets the Uri which uniquely identifies this vocabulary, as a UTF8 string.
    /// </summary>
    ReadOnlySpan<byte> UriUtf8 { get; }

    /// <summary>
    /// Gets all the keywords in the vocabulary.
    /// </summary>
    IEnumerable<IKeyword> Keywords { get; }

    /// <summary>
    /// Validates a schema according to the vocabulary.
    /// </summary>
    /// <param name="schema">The schema to validate.</param>
    /// <returns><see langword="true"/> if the schema was valid.</returns>
    bool ValidateSchemaInstance(JsonElement schema);

    /// <summary>
    /// Builds a reference schema instance from the given path.
    /// </summary>
    /// <param name="jsonSchemaPath">The JSON schema path.</param>
    /// <returns>A JSON schema instance of the form <c>{ "$ref": "/your/reference/here" }</c>.</returns>
    JsonDocument? BuildReferenceSchemaInstance(JsonReference jsonSchemaPath);
}