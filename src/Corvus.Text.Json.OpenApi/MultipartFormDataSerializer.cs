// <copyright file="MultipartFormDataSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Serializes a JSON object to <c>multipart/form-data</c> format.
/// </summary>
/// <remarks>
/// <para>
/// Per RFC 7578, each property of the JSON object becomes a separate part
/// with a <c>Content-Disposition: form-data; name="..."</c> header.
/// </para>
/// <para>
/// Default behavior (no Encoding Object overrides):
/// </para>
/// <list type="bullet">
/// <item><description>Primitive properties (<c>string</c>, <c>number</c>, <c>integer</c>,
/// <c>boolean</c>): written as plain text.</description></item>
/// <item><description>Complex properties (<c>object</c>, <c>array</c>): JSON-stringified
/// with <c>Content-Type: application/json</c>.</description></item>
/// <item><description>Null properties: written as empty parts.</description></item>
/// </list>
/// <para>
/// When an Encoding Object specifies <c>contentType</c> per property, the overload
/// accepting a <see cref="IReadOnlyDictionary{TKey, TValue}"/> of
/// <see cref="PropertyEncoding"/> applies those overrides.
/// </para>
/// </remarks>
public static class MultipartFormDataSerializer
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    /// <summary>
    /// Generates a unique MIME multipart boundary string.
    /// </summary>
    /// <returns>A boundary string safe for use in multipart messages.</returns>
    public static string GenerateBoundary()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>multipart/form-data</c> format using default encoding.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the multipart body to.</param>
    /// <param name="boundary">The boundary string (must match the one in the Content-Type header).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(in T value, Stream output, string boundary)
        where T : struct, IJsonElement<T>
    {
        Serialize(value, output, boundary, null);
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>multipart/form-data</c> format, applying per-property encoding
    /// overrides from the OpenAPI Encoding Object.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the multipart body to.</param>
    /// <param name="boundary">The boundary string (must match the one in the Content-Type header).</param>
    /// <param name="encodings">
    /// Per-property encoding overrides keyed by property name, or <see langword="null"/>
    /// to use default encoding for all properties.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(
        in T value,
        Stream output,
        string boundary,
        IReadOnlyDictionary<string, PropertyEncoding>? encodings)
        where T : struct, IJsonElement<T>
    {
        Serialize(value, output, boundary, encodings, null);
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>multipart/form-data</c> format, applying per-property encoding
    /// overrides and substituting binary parts for specified properties.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the multipart body to.</param>
    /// <param name="boundary">The boundary string (must match the one in the Content-Type header).</param>
    /// <param name="encodings">
    /// Per-property encoding overrides keyed by property name, or <see langword="null"/>
    /// to use default encoding for all properties.
    /// </param>
    /// <param name="binaryParts">
    /// Binary part data keyed by property name, or <see langword="null"/> if there
    /// are no binary parts. Properties that appear here are written as raw binary
    /// content instead of their JSON representation.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(
        in T value,
        Stream output,
        string boundary,
        IReadOnlyDictionary<string, PropertyEncoding>? encodings,
        IReadOnlyDictionary<string, BinaryPartData>? binaryParts)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            string name = property.Name;

            // Check if this property is a binary part.
            if (binaryParts is not null && binaryParts.TryGetValue(name, out BinaryPartData binaryPart))
            {
                WriteBinaryPart(writer, output, boundary, name, binaryPart);
                continue;
            }

            writer.Write("--");
            writer.Write(boundary);
            writer.Write("\r\n");

            writer.Write("Content-Disposition: form-data; name=\"");
            writer.Write(name);
            writer.Write("\"\r\n");

            JsonElement propValue = property.Value;

            PropertyEncoding enc = default;
            encodings?.TryGetValue(name, out enc);

            string? contentTypeOverride = enc.ContentType;

            switch (propValue.ValueKind)
            {
                case JsonValueKind.String:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write(propValue.GetString());
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    writer.Write(propValue.ToString());
                    break;

                case JsonValueKind.Null:
                    if (contentTypeOverride is not null)
                    {
                        writer.Write("Content-Type: ");
                        writer.Write(contentTypeOverride);
                        writer.Write("\r\n");
                    }

                    writer.Write("\r\n");
                    break;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    writer.Write("Content-Type: ");
                    writer.Write(contentTypeOverride ?? "application/json");
                    writer.Write("\r\n\r\n");
                    writer.Write(propValue.ToString());
                    break;

                default:
                    writer.Write("\r\n");
                    break;
            }

            writer.Write("\r\n");
        }

        // Write any binary parts that don't correspond to JSON properties
        // (e.g. the JSON body used a placeholder or omitted the field).
        if (binaryParts is not null)
        {
            HashSet<string> seen = [];
            foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
            {
                seen.Add(property.Name);
            }

            foreach (KeyValuePair<string, BinaryPartData> kvp in binaryParts)
            {
                if (!seen.Contains(kvp.Key))
                {
                    WriteBinaryPart(writer, output, boundary, kvp.Key, kvp.Value);
                }
            }
        }

        // Final boundary with closing "--".
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("--\r\n");
    }

    private static void WriteBinaryPart(
        StreamWriter writer,
        Stream output,
        string boundary,
        string name,
        BinaryPartData binaryPart)
    {
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("\r\n");

        writer.Write("Content-Disposition: form-data; name=\"");
        writer.Write(name);
        writer.Write("\"");

        if (binaryPart.FileName is not null)
        {
            writer.Write("; filename=\"");
            writer.Write(binaryPart.FileName);
            writer.Write("\"");
        }

        writer.Write("\r\n");
        writer.Write("Content-Type: ");
        writer.Write(binaryPart.ContentType);
        writer.Write("\r\n\r\n");

        // Flush text writer before writing raw bytes.
        writer.Flush();
        output.Write(binaryPart.Content.Span);

        writer.Write("\r\n");
    }
}