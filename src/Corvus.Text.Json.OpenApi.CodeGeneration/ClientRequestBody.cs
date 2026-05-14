// <copyright file="ClientRequestBody.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents the request body of an API operation.
/// </summary>
/// <remarks>
/// <para>
/// Stores a <see cref="JsonElement"/> reference to the request body node in the
/// parsed document. No strings are allocated at construction time.
/// </para>
/// </remarks>
public readonly struct ClientRequestBody
{
    private static readonly ReadOnlyMemory<byte> DescriptionUtf8 = "description"u8.ToArray();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientRequestBody"/> struct.
    /// </summary>
    /// <param name="element">The request body element from the parsed document.</param>
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
    /// Gets the request body element from the parsed document.
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
    /// Gets the description, or <see langword="null"/> if not present.
    /// </summary>
    /// <returns>The description string, or <see langword="null"/>.</returns>
    /// <remarks>This allocates a string. Call only at the code-emission boundary.</remarks>
    public string? GetDescription()
    {
        if (this.Element.TryGetProperty(DescriptionUtf8.Span, out JsonElement desc)
            && desc.ValueKind == JsonValueKind.String)
        {
            return desc.GetString();
        }

        return null;
    }
}