// <copyright file="WalkedMediaTypeContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A media type content entry extracted from a request body or response
/// by the spec walker.
/// </summary>
public readonly struct WalkedMediaTypeContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedMediaTypeContent"/> struct.
    /// </summary>
    /// <param name="mediaType">The media type string (e.g. <c>application/json</c>).</param>
    /// <param name="hasSchema">Whether this media type entry declares a schema.</param>
    public WalkedMediaTypeContent(string mediaType, bool hasSchema)
    {
        this.MediaType = mediaType;
        this.HasSchema = hasSchema;
    }

    /// <summary>
    /// Gets the media type string.
    /// </summary>
    public string MediaType { get; }

    /// <summary>
    /// Gets a value indicating whether this media type declares a schema.
    /// </summary>
    public bool HasSchema { get; }
}