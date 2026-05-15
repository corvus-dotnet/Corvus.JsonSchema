// <copyright file="WalkedResponseHeader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A response header extracted from an API response by the spec walker.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Property"/> carries both the header name
/// (via <see cref="JsonProperty{TValue}.Name"/>) and the header element.
/// No strings are allocated; values are accessible on demand.
/// </para>
/// </remarks>
public readonly struct WalkedResponseHeader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedResponseHeader"/> struct.
    /// </summary>
    /// <param name="property">The header property (name = header name, value = header object).</param>
    /// <param name="hasSchema">Whether the header declares a schema.</param>
    public WalkedResponseHeader(
        JsonProperty<JsonElement> property,
        bool hasSchema)
    {
        this.Property = property;
        this.HasSchema = hasSchema;
    }

    /// <summary>
    /// Gets the header property from the response's headers map.
    /// The property name is the header name (e.g. <c>X-Rate-Limit</c>),
    /// and the value is the header object element.
    /// </summary>
    public JsonProperty<JsonElement> Property { get; }

    /// <summary>
    /// Gets a value indicating whether the header declares a schema.
    /// </summary>
    public bool HasSchema { get; }
}