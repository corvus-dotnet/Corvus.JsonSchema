// <copyright file="ClientParameter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a parameter in an API operation, backed by a reference into the
/// parsed spec document.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Element"/> holds a reference into the parsed document and is only
/// valid while the document is alive. String properties (name, description) are
/// extracted at the emitter boundary via the <c>Get*</c> methods.
/// </para>
/// </remarks>
public readonly struct ClientParameter
{
    private static ReadOnlySpan<byte> NameUtf8 => "name"u8;

    private static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientParameter"/> struct.
    /// </summary>
    /// <param name="element">The resolved parameter element from the spec.</param>
    /// <param name="location">Where the parameter appears.</param>
    /// <param name="isRequired">Whether the parameter is required.</param>
    /// <param name="schemaPointer">
    /// UTF-8 JSON pointer to the parameter's schema, or <see langword="null"/>
    /// if the parameter has no schema.
    /// </param>
    public ClientParameter(
        JsonElement element,
        ParameterLocation location,
        bool isRequired,
        byte[]? schemaPointer)
    {
        this.Element = element;
        this.Location = location;
        this.IsRequired = isRequired;
        this.SchemaPointer = schemaPointer;
    }

    /// <summary>
    /// Gets the resolved parameter element from the spec document.
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
    /// Gets the UTF-8 JSON pointer to the parameter's schema, or <see langword="null"/>.
    /// </summary>
    public byte[]? SchemaPointer { get; }

    /// <summary>
    /// Gets the parameter name as declared in the spec.
    /// </summary>
    /// <returns>The parameter name, or <c>"unknown"</c> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetName()
    {
        if (this.Element.TryGetProperty(NameUtf8, out JsonElement name)
            && name.ValueKind == JsonValueKind.String)
        {
            return name.GetString()!;
        }

        return "unknown";
    }

    /// <summary>
    /// Gets the parameter description from the spec.
    /// </summary>
    /// <returns>The description, or <see langword="null"/> if not present.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetDescription()
    {
        if (this.Element.TryGetProperty(DescriptionUtf8, out JsonElement desc)
            && desc.ValueKind == JsonValueKind.String)
        {
            return desc.GetString();
        }

        return null;
    }
}