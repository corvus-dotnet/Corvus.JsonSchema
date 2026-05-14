// <copyright file="WalkedRequestBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A request body extracted from an API operation by the spec walker.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Element"/> references the request body node in the parsed
/// document. No strings are allocated; the description is accessible on demand.
/// </para>
/// </remarks>
public readonly struct WalkedRequestBody
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedRequestBody"/> struct.
    /// </summary>
    /// <param name="element">The request body element from the parsed document.</param>
    /// <param name="isRequired">Whether the request body is required.</param>
    /// <param name="content">The media type content entries.</param>
    public WalkedRequestBody(
        JsonElement element,
        bool isRequired,
        WalkedMediaTypeContent[] content)
    {
        this.Element = element;
        this.IsRequired = isRequired;
        this.Content = content;
    }

    /// <summary>
    /// Gets the request body element from the parsed document.
    /// </summary>
    public JsonElement Element { get; }

    /// <summary>
    /// Gets a value indicating whether the request body is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the media type content entries.
    /// </summary>
    public WalkedMediaTypeContent[] Content { get; }
}