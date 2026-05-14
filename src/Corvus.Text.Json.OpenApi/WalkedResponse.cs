// <copyright file="WalkedResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A response extracted from an API operation by the spec walker.
/// </summary>
public readonly struct WalkedResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedResponse"/> struct.
    /// </summary>
    /// <param name="statusCode">The status code key (e.g. <c>"200"</c>, <c>"default"</c>).</param>
    /// <param name="description">The response description, if present.</param>
    /// <param name="content">The media type content entries.</param>
    public WalkedResponse(
        string statusCode,
        string? description,
        WalkedMediaTypeContent[] content)
    {
        this.StatusCode = statusCode;
        this.Description = description;
        this.Content = content;
    }

    /// <summary>
    /// Gets the status code key.
    /// </summary>
    public string StatusCode { get; }

    /// <summary>
    /// Gets the description, or <see langword="null"/> if not present.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the media type content entries.
    /// </summary>
    public WalkedMediaTypeContent[] Content { get; }
}