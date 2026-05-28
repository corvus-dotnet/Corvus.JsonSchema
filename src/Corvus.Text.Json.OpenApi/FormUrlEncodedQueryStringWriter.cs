// <copyright file="FormUrlEncodedQueryStringWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Writes a JSON object as a <c>application/x-www-form-urlencoded</c> query string
/// to an <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is used by generated request structs for OpenAPI 3.2 <c>in: querystring</c>
/// parameters. The parameter's content schema (always an object) is serialized as
/// <c>key=value&amp;key2=value2</c> with percent-encoding applied to both keys and values.
/// </para>
/// <para>
/// Primitive values are encoded directly. Complex values (arrays, objects) are
/// JSON-stringified before percent-encoding, per OAS §4.8.14.4 default encoding.
/// </para>
/// </remarks>
public static class FormUrlEncodedQueryStringWriter
{
    /// <summary>
    /// Writes the properties of a JSON object as form-urlencoded query parameters.
    /// </summary>
    /// <typeparam name="T">The JSON element type (must be an object at runtime).</typeparam>
    /// <param name="value">The JSON object whose properties become query parameters.</param>
    /// <param name="writer">The buffer writer to write UTF-8 encoded bytes to.</param>
    /// <returns>The total number of bytes written.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static int Write<T>(in T value, IBufferWriter<byte> writer)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        int totalWritten = 0;
        bool first = true;

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            string name = property.Name;
            string encodedName = Uri.EscapeDataString(name);

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                // Exploded array (default for form): name=item1&name=item2
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    totalWritten += WritePair(writer, encodedName, GetEncodedValue(item), ref first);
                }
            }
            else
            {
                totalWritten += WritePair(writer, encodedName, GetEncodedValue(property.Value), ref first);
            }
        }

        return totalWritten;
    }

    private static int WritePair(IBufferWriter<byte> writer, string encodedName, string encodedValue, ref bool first)
    {
        int written = 0;

        if (!first)
        {
            writer.Write("&"u8);
            written++;
        }

        first = false;

        int nameLen = Encoding.UTF8.GetByteCount(encodedName);
        Span<byte> nameSpan = writer.GetSpan(nameLen);
        Encoding.UTF8.GetBytes(encodedName, nameSpan);
        writer.Advance(nameLen);
        written += nameLen;

        writer.Write("="u8);
        written++;

        int valueLen = Encoding.UTF8.GetByteCount(encodedValue);
        Span<byte> valueSpan = writer.GetSpan(valueLen);
        Encoding.UTF8.GetBytes(encodedValue, valueSpan);
        writer.Advance(valueLen);
        written += valueLen;

        return written;
    }

    private static string GetEncodedValue(JsonElement value)
    {
        string raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Object or JsonValueKind.Array => value.ToString(),
            _ => value.GetRawText(),
        };

        return Uri.EscapeDataString(raw);
    }
}