// <copyright file="WalkedRequestBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A request body extracted from an API operation by the spec walker.
/// </summary>
public readonly struct WalkedRequestBody
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedRequestBody"/> struct.
    /// </summary>
    /// <param name="isRequired">Whether the request body is required.</param>
    /// <param name="content">The media type content entries.</param>
    /// <param name="description">The description, if present.</param>
    public WalkedRequestBody(
        bool isRequired,
        WalkedMediaTypeContent[] content,
        string? description = null)
    {
        this.IsRequired = isRequired;
        this.Content = content;
        this.Description = description;
    }

    /// <summary>
    /// Gets a value indicating whether the request body is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the media type content entries.
    /// </summary>
    public WalkedMediaTypeContent[] Content { get; }

    /// <summary>
    /// Gets the description, or <see langword="null"/> if not present.
    /// </summary>
    public string? Description { get; }
}