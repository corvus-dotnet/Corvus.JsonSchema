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
    /// in <c>multipart/form-data</c> format.
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
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        using StreamWriter writer = new(output, Utf8NoBom, bufferSize: 256, leaveOpen: true);

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            writer.Write("--");
            writer.Write(boundary);
            writer.Write("\r\n");

            string name = property.Name;
            writer.Write("Content-Disposition: form-data; name=\"");
            writer.Write(name);
            writer.Write("\"\r\n");

            JsonElement propValue = property.Value;

            switch (propValue.ValueKind)
            {
                case JsonValueKind.String:
                    writer.Write("\r\n");
                    writer.Write(propValue.GetString());
                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    writer.Write("\r\n");
                    writer.Write(propValue.ToString());
                    break;

                case JsonValueKind.Null:
                    writer.Write("\r\n");
                    break;

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    // Complex values are JSON-stringified with an explicit content type.
                    writer.Write("Content-Type: application/json\r\n\r\n");
                    writer.Write(propValue.ToString());
                    break;

                default:
                    writer.Write("\r\n");
                    break;
            }

            writer.Write("\r\n");
        }

        // Final boundary with closing "--".
        writer.Write("--");
        writer.Write(boundary);
        writer.Write("--\r\n");
    }
}