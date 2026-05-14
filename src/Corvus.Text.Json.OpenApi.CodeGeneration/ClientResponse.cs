// <copyright file="ClientResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents one response entry in an API operation.
/// </summary>
public readonly struct ClientResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientResponse"/> struct.
    /// </summary>
    /// <param name="statusCode">
    /// The status code pattern (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
    /// </param>
    /// <param name="description">The description, or <see langword="null"/>.</param>
    /// <param name="content">The content entries, one per media type.</param>
    public ClientResponse(
        string statusCode,
        string? description,
        ClientMediaTypeContent[] content)
    {
        this.StatusCode = statusCode;
        this.Description = description;
        this.Content = content;
    }

    /// <summary>
    /// Gets the status code pattern (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
    /// </summary>
    public string StatusCode { get; }

    /// <summary>
    /// Gets the description, or <see langword="null"/> if not present.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the content entries, one per media type.
    /// </summary>
    public ClientMediaTypeContent[] Content { get; }
}