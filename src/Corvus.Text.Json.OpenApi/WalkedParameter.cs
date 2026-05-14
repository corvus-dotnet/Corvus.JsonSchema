// <copyright file="WalkedParameter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// A parameter extracted from an API operation by the spec walker, using
/// the strongly-typed schema model.
/// </summary>
/// <remarks>
/// <para>
/// This carries the typed metadata the walker extracts (name, location,
/// style, explode, required) so that downstream consumers such as
/// <c>ClientModelBuilder</c> do not need to re-parse raw JSON.
/// </para>
/// </remarks>
public readonly struct WalkedParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedParameter"/> struct.
    /// </summary>
    /// <param name="name">The parameter name as declared in the spec.</param>
    /// <param name="location">Where the parameter appears.</param>
    /// <param name="isRequired">Whether the parameter is required.</param>
    /// <param name="style">The serialization style.</param>
    /// <param name="explode">Whether to explode array/object values.</param>
    /// <param name="hasSchema">Whether the parameter declares a schema.</param>
    public WalkedParameter(
        string name,
        ParameterLocation location,
        bool isRequired,
        ParameterStyle style,
        bool explode,
        bool hasSchema)
    {
        this.Name = name;
        this.Location = location;
        this.IsRequired = isRequired;
        this.Style = style;
        this.Explode = explode;
        this.HasSchema = hasSchema;
    }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets where the parameter appears (path, query, header, cookie).
    /// </summary>
    public ParameterLocation Location { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the OpenAPI serialization style.
    /// </summary>
    public ParameterStyle Style { get; }

    /// <summary>
    /// Gets a value indicating whether array/object values are exploded.
    /// </summary>
    public bool Explode { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter declares a schema.
    /// </summary>
    public bool HasSchema { get; }
}