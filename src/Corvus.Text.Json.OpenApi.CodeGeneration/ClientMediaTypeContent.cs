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
/// The <see cref="MediaTypeProperty"/> holds a reference into the parsed spec
/// document and is only valid while the document is alive. The media type string
/// is extracted at the emitter boundary via <see cref="GetMediaType"/>.
/// </para>
/// </remarks>
public readonly struct ClientMediaTypeContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientMediaTypeContent"/> struct.
    /// </summary>
    /// <param name="mediaTypeProperty">The property from the content map (name = media type).</param>
    /// <param name="schemaPointer">
    /// UTF-8 JSON pointer to the schema, or <see langword="null"/> if no schema is defined.
    /// </param>
    public ClientMediaTypeContent(
        JsonProperty<JsonElement> mediaTypeProperty,
        byte[]? schemaPointer)
    {
        this.MediaTypeProperty = mediaTypeProperty;
        this.SchemaPointer = schemaPointer;
    }

    /// <summary>
    /// Gets the property from the content map. The property name is the media type
    /// (e.g. <c>application/json</c>).
    /// </summary>
    public JsonProperty<JsonElement> MediaTypeProperty { get; }

    /// <summary>
    /// Gets the UTF-8 JSON pointer to the schema within the spec document,
    /// or <see langword="null"/> if no schema is defined for this media type.
    /// </summary>
    public byte[]? SchemaPointer { get; }

    /// <summary>
    /// Gets the media type string (e.g. <c>application/json</c>).
    /// </summary>
    /// <returns>The media type.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetMediaType() => this.MediaTypeProperty.Name;
}