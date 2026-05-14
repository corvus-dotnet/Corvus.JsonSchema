// <copyright file="ClientResponse.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents one response entry in an API operation.
/// </summary>
/// <remarks>
/// <para>
/// Stores a <see cref="JsonProperty{TValue}"/> reference into the parsed document.
/// The status code is the property name; the description is accessible via the element.
/// No strings are allocated at construction time.
/// </para>
/// </remarks>
public readonly struct ClientResponse
{
    private static readonly ReadOnlyMemory<byte> DescriptionUtf8 = "description"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientResponse"/> struct.
    /// </summary>
    /// <param name="responseProperty">The response property (name = status code, value = response element).</param>
    /// <param name="content">The content entries, one per media type.</param>
    public ClientResponse(
        JsonProperty<JsonElement> responseProperty,
        ClientMediaTypeContent[] content)
    {
        this.ResponseProperty = responseProperty;
        this.Content = content;
    }

    /// <summary>
    /// Gets the response property from the responses map.
    /// </summary>
    public JsonProperty<JsonElement> ResponseProperty { get; }

    /// <summary>
    /// Gets the content entries, one per media type.
    /// </summary>
    public ClientMediaTypeContent[] Content { get; }

    /// <summary>
    /// Gets the status code pattern (e.g. <c>200</c>, <c>default</c>, <c>2XX</c>).
    /// </summary>
    /// <returns>The status code string.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string GetStatusCode() => this.ResponseProperty.Name;

    /// <summary>
    /// Gets the description, or <see langword="null"/> if not present.
    /// </summary>
    /// <returns>The description string, or <see langword="null"/>.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetDescription()
    {
        if (this.ResponseProperty.Value.TryGetProperty(DescriptionUtf8.Span, out JsonElement desc)
            && desc.ValueKind == JsonValueKind.String)
        {
            return desc.GetString();
        }

        return null;
    }
}