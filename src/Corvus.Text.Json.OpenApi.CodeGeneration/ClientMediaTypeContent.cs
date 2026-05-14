// <copyright file="ClientMediaTypeContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a content entry (media type → schema) in a request body or response.
/// </summary>
/// <remarks>
/// <para>
/// Stores a <see cref="JsonProperty{TValue}"/> reference into the parsed document.
/// The media type is the property name. No strings are allocated at construction time.
/// </para>
/// </remarks>
public readonly struct ClientMediaTypeContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientMediaTypeContent"/> struct.
    /// </summary>
    /// <param name="mediaTypeProperty">The media type property (name = media type, value = content element).</param>
    /// <param name="schemaPointer">
    /// JSON pointer to the schema, or <see langword="null"/> if no schema is defined.
    /// </param>
    public ClientMediaTypeContent(
        JsonProperty<JsonElement> mediaTypeProperty,
        string? schemaPointer)
    {
        this.MediaTypeProperty = mediaTypeProperty;
        this.SchemaPointer = schemaPointer;
    }

    /// <summary>
    /// Gets the media type property from the content map.
    /// The property name is the media type (e.g. <c>application/json</c>).
    /// </summary>
    public JsonProperty<JsonElement> MediaTypeProperty { get; }

    /// <summary>
    /// Gets the JSON pointer to the schema within the spec document,
    /// or <see langword="null"/> if no schema is defined for this media type.
    /// </summary>
    public string? SchemaPointer { get; }

    /// <summary>
    /// Gets the media type string (e.g. <c>application/json</c>).
    /// </summary>
    /// <returns>The media type.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetMediaType() => this.MediaTypeProperty.Name;

    /// <summary>
    /// Tests whether this entry's media type matches the given UTF-8 name
    /// without allocating a string.
    /// </summary>
    /// <param name="utf8MediaType">The UTF-8 media type to compare.</param>
    /// <returns><see langword="true"/> if the media type matches.</returns>
    public bool MediaTypeEquals(ReadOnlySpan<byte> utf8MediaType)
        => this.MediaTypeProperty.NameEquals(utf8MediaType);
}