// <copyright file="WalkedResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A response extracted from an API operation by the spec walker.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Property"/> carries both the status code
/// (via <see cref="JsonProperty{TValue}.Name"/>) and the response element.
/// No strings are allocated; values are accessible on demand.
/// </para>
/// </remarks>
public readonly struct WalkedResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedResponse"/> struct.
    /// </summary>
    /// <param name="property">The response property (name = status code, value = response element).</param>
    /// <param name="content">The media type content entries.</param>
    public WalkedResponse(
        JsonProperty<JsonElement> property,
        WalkedMediaTypeContent[] content)
    {
        this.Property = property;
        this.Content = content;
    }

    /// <summary>
    /// Gets the response property from the responses map.
    /// The property name is the status code (e.g. <c>200</c>, <c>default</c>),
    /// and the value is the response element.
    /// </summary>
    public JsonProperty<JsonElement> Property { get; }

    /// <summary>
    /// Gets the media type content entries.
    /// </summary>
    public WalkedMediaTypeContent[] Content { get; }
}