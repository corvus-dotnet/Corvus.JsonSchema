// <copyright file="ClientResponseHeader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a response header in an API operation.
/// </summary>
/// <remarks>
/// <para>
/// Stores a <see cref="JsonProperty{TValue}"/> reference into the parsed document.
/// The header name is the property name. No strings are allocated at construction time.
/// </para>
/// </remarks>
public readonly struct ClientResponseHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientResponseHeader"/> struct.
    /// </summary>
    /// <param name="headerProperty">The header property (name = header name, value = header element).</param>
    /// <param name="schemaPointer">
    /// JSON pointer to the schema, or <see langword="null"/> if no schema is defined.
    /// </param>
    public ClientResponseHeader(
        JsonProperty<JsonElement> headerProperty,
        string? schemaPointer)
    {
        this.HeaderProperty = headerProperty;
        this.SchemaPointer = schemaPointer;
    }

    /// <summary>
    /// Gets the header property from the response's headers map.
    /// The property name is the header name (e.g. <c>X-Rate-Limit</c>).
    /// </summary>
    public JsonProperty<JsonElement> HeaderProperty { get; }

    /// <summary>
    /// Gets the JSON pointer to the schema within the spec document,
    /// or <see langword="null"/> if no schema is defined for this header.
    /// </summary>
    public string? SchemaPointer { get; }

    /// <summary>
    /// Gets the header name (e.g. <c>X-Rate-Limit</c>).
    /// </summary>
    /// <returns>The header name.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetHeaderName() => this.HeaderProperty.Name;

    /// <summary>
    /// Tests whether this header's name matches the given UTF-8 name
    /// without allocating a string.
    /// </summary>
    /// <param name="utf8HeaderName">The UTF-8 header name to compare.</param>
    /// <returns><see langword="true"/> if the header name matches.</returns>
    public bool HeaderNameEquals(ReadOnlySpan<byte> utf8HeaderName)
        => this.HeaderProperty.NameEquals(utf8HeaderName);
}