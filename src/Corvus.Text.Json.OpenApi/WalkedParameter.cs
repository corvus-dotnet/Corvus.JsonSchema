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
/// This carries the computed metadata the walker extracts (location,
/// style, explode, required) alongside a <see cref="JsonElement"/> reference
/// to the parameter node in the parsed document. No strings are allocated;
/// the name is accessible on demand via the element.
/// </para>
/// </remarks>
public readonly struct WalkedParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WalkedParameter"/> struct.
    /// </summary>
    /// <param name="element">The parameter element from the parsed document.</param>
    /// <param name="location">Where the parameter appears.</param>
    /// <param name="isRequired">Whether the parameter is required.</param>
    /// <param name="style">The serialization style.</param>
    /// <param name="explode">Whether to explode array/object values.</param>
    /// <param name="hasSchema">Whether the parameter declares a schema.</param>
    /// <param name="serializationKind">
    /// The serialization classification for this parameter, determined by the
    /// walker from the schema's <c>type</c> and <c>format</c> keywords.
    /// </param>
    /// <param name="isPathLevel">
    /// Whether this parameter was declared at the path-item level rather than
    /// the operation level. Path-level parameters live at
    /// <c>#/paths/&lt;path&gt;/parameters/&lt;i&gt;</c> rather than
    /// <c>#/paths/&lt;path&gt;/&lt;method&gt;/parameters/&lt;i&gt;</c>.
    /// </param>
    /// <param name="sourceIndex">
    /// The zero-based index of this parameter in its source array (path-item
    /// <c>parameters</c> or operation <c>parameters</c>). Used to construct
    /// correct JSON Pointer references to the parameter schema.
    /// </param>
    public WalkedParameter(
        JsonElement element,
        ParameterLocation location,
        bool isRequired,
        ParameterStyle style,
        bool explode,
        bool hasSchema,
        ParameterSerializationKind serializationKind,
        bool isPathLevel = false,
        int sourceIndex = 0)
    {
        this.Element = element;
        this.Location = location;
        this.IsRequired = isRequired;
        this.Style = style;
        this.Explode = explode;
        this.HasSchema = hasSchema;
        this.SerializationKind = serializationKind;
        this.IsPathLevel = isPathLevel;
        this.SourceIndex = sourceIndex;
    }

    /// <summary>
    /// Gets the parameter element from the parsed document.
    /// Use this to access the parameter name and other properties on demand.
    /// </summary>
    public JsonElement Element { get; }

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

    /// <summary>
    /// Gets the serialization classification for this parameter, determined
    /// by the walker from the schema's <c>type</c> and <c>format</c> keywords.
    /// </summary>
    public ParameterSerializationKind SerializationKind { get; }

    /// <summary>
    /// Gets a value indicating whether this parameter was declared at the
    /// path-item level rather than the operation level.
    /// </summary>
    public bool IsPathLevel { get; }

    /// <summary>
    /// Gets the zero-based index of this parameter in its source array
    /// (path-item or operation <c>parameters</c>).
    /// </summary>
    public int SourceIndex { get; }
}