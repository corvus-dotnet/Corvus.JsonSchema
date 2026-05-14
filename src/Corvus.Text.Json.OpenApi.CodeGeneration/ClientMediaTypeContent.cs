// <copyright file="ClientMediaTypeContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a content entry (media type → schema) in a request body or response.
/// </summary>
public readonly struct ClientMediaTypeContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientMediaTypeContent"/> struct.
    /// </summary>
    /// <param name="mediaType">The media type (e.g. <c>application/json</c>).</param>
    /// <param name="schemaPointer">
    /// JSON pointer to the schema, or <see langword="null"/> if no schema is defined.
    /// </param>
    public ClientMediaTypeContent(
        string mediaType,
        string? schemaPointer)
    {
        this.MediaType = mediaType;
        this.SchemaPointer = schemaPointer;
    }

    /// <summary>
    /// Gets the media type (e.g. <c>application/json</c>).
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Gets the JSON pointer to the schema within the spec document,
    /// or <see langword="null"/> if no schema is defined for this media type.
    /// </summary>
    public string? SchemaPointer { get; }
}