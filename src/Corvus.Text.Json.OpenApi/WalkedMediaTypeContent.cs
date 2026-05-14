// <copyright file="WalkedMediaTypeContent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A media type content entry extracted from a request body or response
/// by the spec walker.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Property"/> carries both the media type name
/// (via <see cref="JsonProperty{TValue}.Name"/>) and the content element.
/// No strings are allocated; the media type is accessible on demand.
/// </para>
/// </remarks>
public readonly struct WalkedMediaTypeContent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedMediaTypeContent"/> struct.
    /// </summary>
    /// <param name="property">The media type property from the content map.</param>
    /// <param name="hasSchema">Whether this media type entry declares a schema.</param>
    public WalkedMediaTypeContent(JsonProperty<JsonElement> property, bool hasSchema)
    {
        this.Property = property;
        this.HasSchema = hasSchema;
    }

    /// <summary>
    /// Gets the media type property from the content map.
    /// The property name is the media type (e.g. <c>application/json</c>),
    /// and the value is the media type object.
    /// </summary>
    public JsonProperty<JsonElement> Property { get; }

    /// <summary>
    /// Gets a value indicating whether this media type declares a schema.
    /// </summary>
    public bool HasSchema { get; }
}