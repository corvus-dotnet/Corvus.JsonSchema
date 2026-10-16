// <copyright file="SpecificationInfoReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Operations;

/// <summary>
/// Reads the document-level facts of an OpenAPI document that every version carries in the same place.
/// </summary>
public static class SpecificationInfoReader
{
    /// <summary>
    /// Reads <c>info.title</c> and <c>info.version</c>.
    /// </summary>
    /// <param name="specRoot">The root element of the document.</param>
    /// <param name="title">The title, or <see langword="null"/>.</param>
    /// <param name="apiVersion">The API version, or <see langword="null"/>.</param>
    public static void Read(JsonElement specRoot, out string? title, out string? apiVersion)
    {
        title = null;
        apiVersion = null;
        if (specRoot.ValueKind == JsonValueKind.Object
            && specRoot.TryGetProperty("info"u8, out JsonElement info)
            && info.ValueKind == JsonValueKind.Object)
        {
            title = ReadString(info, "title"u8);
            apiVersion = ReadString(info, "version"u8);
        }
    }

    internal static string? ReadString(JsonElement element, ReadOnlySpan<byte> name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}