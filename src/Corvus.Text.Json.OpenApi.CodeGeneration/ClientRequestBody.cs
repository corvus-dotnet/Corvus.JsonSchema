// <copyright file="ClientRequestBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents the request body of an API operation, backed by a reference into the
/// parsed spec document.
/// </summary>
public readonly struct ClientRequestBody
{
    private static ReadOnlySpan<byte> DescriptionUtf8 => "description"u8;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientRequestBody"/> struct.
    /// </summary>
    /// <param name="element">The resolved requestBody element from the spec.</param>
    /// <param name="isRequired">Whether the request body is required.</param>
    /// <param name="content">The content entries, one per media type.</param>
    public ClientRequestBody(
        JsonElement element,
        bool isRequired,
        ClientMediaTypeContent[] content)
    {
        this.Element = element;
        this.IsRequired = isRequired;
        this.Content = content;
    }

    /// <summary>
    /// Gets the resolved requestBody element from the spec document.
    /// </summary>
    public JsonElement Element { get; }

    /// <summary>
    /// Gets a value indicating whether the request body is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets the content entries, one per media type.
    /// </summary>
    public ClientMediaTypeContent[] Content { get; }

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