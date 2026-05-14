// <copyright file="ParameterEntry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A single parameter entry in an <see cref="ApiRequest"/>, carrying the typed
/// value together with the OpenAPI style and explode metadata needed for
/// serialization by the transport.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Value"/> is a <see cref="JsonElement"/> that preserves the
/// original JSON backing. The transport applies the appropriate serialization
/// rules based on <see cref="Style"/>, <see cref="Explode"/>, and the value's
/// <see cref="JsonValueKind"/>.
/// </para>
/// </remarks>
public readonly struct ParameterEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterEntry"/> struct.
    /// </summary>
    /// <param name="name">The parameter name as it appears in the HTTP request.</param>
    /// <param name="value">The typed parameter value.</param>
    /// <param name="style">The OpenAPI serialization style.</param>
    /// <param name="explode">Whether to explode array/object values.</param>
    public ParameterEntry(string name, JsonElement value, ParameterStyle style, bool explode)
    {
        this.Name = name;
        this.Value = value;
        this.Style = style;
        this.Explode = explode;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the typed parameter value.
    /// </summary>
    public JsonElement Value { get; }

    /// <summary>
    /// Gets the OpenAPI serialization style.
    /// </summary>
    public ParameterStyle Style { get; }

    /// <summary>
    /// Gets a value indicating whether array/object values are exploded.
    /// </summary>
    public bool Explode { get; }
}