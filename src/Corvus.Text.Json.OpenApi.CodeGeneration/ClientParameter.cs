// <copyright file="ClientParameter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a parameter in an API operation.
/// </summary>
/// <remarks>
/// <para>
/// Stores a <see cref="JsonElement"/> reference to the parameter node in the
/// parsed document. The name is accessible on demand via <see cref="GetName"/>.
/// </para>
/// </remarks>
public readonly struct ClientParameter
{
    private static readonly ReadOnlyMemory<byte> NameUtf8 = "name"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientParameter"/> struct.
    /// </summary>
    /// <param name="element">The parameter element from the parsed document.</param>
    /// <param name="location">Where the parameter appears.</param>
    /// <param name="isRequired">Whether the parameter is required.</param>
    /// <param name="schemaPointer">
    /// JSON pointer to the parameter's schema, or <see langword="null"/>
    /// if the parameter has no schema.
    /// </param>
    /// <param name="style">The OpenAPI serialization style for this parameter.</param>
    /// <param name="explode">Whether to explode array/object values.</param>
    /// <param name="serializationKind">
    /// The serialization classification, determined by the spec walker from
    /// the schema's <c>type</c> and <c>format</c> keywords.
    /// </param>
    public ClientParameter(
        JsonElement element,
        ParameterLocation location,
        bool isRequired,
        string? schemaPointer,
        ParameterStyle style,
        bool explode,
        ParameterSerializationKind serializationKind)
    {
        this.Element = element;
        this.Location = location;
        this.IsRequired = isRequired;
        this.SchemaPointer = schemaPointer;
        this.Style = style;
        this.Explode = explode;
        this.SerializationKind = serializationKind;
    }

    /// <summary>
    /// Gets the parameter element from the parsed document.
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
    /// Gets the JSON pointer to the parameter's schema, or <see langword="null"/>.
    /// </summary>
    public string? SchemaPointer { get; }

    /// <summary>
    /// Gets the OpenAPI serialization style for this parameter.
    /// </summary>
    public ParameterStyle Style { get; }

    /// <summary>
    /// Gets a value indicating whether array/object values are exploded.
    /// </summary>
    public bool Explode { get; }

    /// <summary>
    /// Gets the serialization classification for this parameter, determined
    /// by the spec walker from the schema's <c>type</c> and <c>format</c> keywords.
    /// </summary>
    public ParameterSerializationKind SerializationKind { get; }

    /// <summary>
    /// Gets the parameter name as declared in the spec.
    /// </summary>
    /// <returns>The parameter name.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetName()
    {
        if (this.Element.TryGetProperty(NameUtf8.Span, out JsonElement name)
            && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString()!;
        }

        return "unknown";
    }
}