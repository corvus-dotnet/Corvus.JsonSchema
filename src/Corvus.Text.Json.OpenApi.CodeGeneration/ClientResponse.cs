// <copyright file="ClientResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents one response entry in an API operation, backed by a reference into the
/// parsed spec document.
/// </summary>
public readonly struct ClientResponse
{
    private static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientResponse"/> struct.
    /// </summary>
    /// <param name="statusProperty">
    /// The property from the responses map. The property name is the status code
    /// pattern (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
    /// </param>
    /// <param name="element">The resolved response element.</param>
    /// <param name="content">The content entries, one per media type.</param>
    public ClientResponse(
        JsonProperty<JsonElement> statusProperty,
        JsonElement element,
        ClientMediaTypeContent[] content)
    {
        this.StatusProperty = statusProperty;
        this.Element = element;
        this.Content = content;
    }

    /// <summary>
    /// Gets the property from the responses map. The property name is the status code
    /// pattern (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
    /// </summary>
    public JsonProperty<JsonElement> StatusProperty { get; }

    /// <summary>
    /// Gets the resolved response element from the spec document.
    /// </summary>
    public JsonElement Element { get; }

    /// <summary>
    /// Gets the content entries, one per media type.
    /// </summary>
    public ClientMediaTypeContent[] Content { get; }

    /// <summary>
    /// Gets the status code pattern string (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
    /// </summary>
    /// <returns>The status code pattern.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetStatusCode() => this.StatusProperty.Name;

    /// <summary>
    /// Gets the description from the spec.
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