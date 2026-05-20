// <copyright file="FormUrlEncodedSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Serializes a JSON object to <c>application/x-www-form-urlencoded</c> format,
/// and deserializes form-encoded bodies back to typed JSON elements.
/// </summary>
/// <remarks>
/// <para>
/// Per OAS §4.8.14.4, the default serialization for form-urlencoded content is:
/// </para>
/// <list type="bullet">
/// <item><description>Primitive properties (<c>string</c>, <c>number</c>, <c>integer</c>,
/// <c>boolean</c>): the value is percent-encoded.</description></item>
/// <item><description>Complex properties (<c>object</c>, <c>array</c>): the value is
/// JSON-stringified, then percent-encoded.</description></item>
/// </list>
/// <para>
/// When an Encoding Object specifies <c>style</c>, <c>explode</c>, or
/// <c>allowReserved</c> per property, the overload accepting a
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> of <see cref="PropertyEncoding"/>
/// applies those overrides.
/// </para>
/// <para>
/// Deserialization uses <see cref="Utf8Uri.TryUnescapeDataString"/> (the inverse of
/// <see cref="Utf8Uri.TryEscapeDataString"/> used during serialization) and writes
/// the result as a JSON object via <see cref="FormFieldReader"/>.
/// </para>
/// </remarks>
public static class FormUrlEncodedSerializer
{
    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>application/x-www-form-urlencoded</c> format using default encoding.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the encoded body to.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="value"/> is not a JSON object.
    /// </exception>
    public static void Serialize<T>(in T value, Stream output)
        where T : struct, IJsonElement<T>
    {
        Serialize(value, output, null);
    }

    /// <summary>
    /// Serializes a JSON object's properties directly to a <see cref="Stream"/>
    /// in <c>application/x-www-form-urlencoded</c> format, applying per-property
    /// encoding overrides from the OpenAPI Encoding Object.
    /// </summary>
    /// <typeparam name="T">The JSON element type.</typeparam>
    /// <param name="value">The JSON object to serialize.</param>
    /// <param name="output">The stream to write the encoded body to.</param>
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
        IReadOnlyDictionary<string, PropertyEncoding>? encodings)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        using StreamWriter writer = new(output, new UTF8Encoding(false), bufferSize: 256, leaveOpen: true);
        bool first = true;

        foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
        {
            string name = property.Name;
            JsonElement propValue = property.Value;

            PropertyEncoding enc = default;
            encodings?.TryGetValue(name, out enc);

            WriteProperty(writer, name, propValue, enc, ref first);
        }
    }

    /// <summary>
    /// Deserializes a <c>application/x-www-form-urlencoded</c> body into a
    /// <see cref="ParsedJsonDocument{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the inverse of <see cref="Serialize{T}(in T, Stream)"/>. It unescapes
    /// each field using <see cref="Utf8Uri.TryUnescapeDataString"/>, writes the result
    /// as a JSON object, and parses it into a strongly-typed document. Duplicate keys
    /// (exploded arrays) are collected into JSON arrays.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The JSON element type to parse into.</typeparam>
    /// <param name="formBody">The raw UTF-8 form-urlencoded body bytes.</param>
    /// <returns>A parsed JSON document backed by pooled memory. The caller must dispose it.</returns>
    public static ParsedJsonDocument<T> Deserialize<T>(ReadOnlyMemory<byte> formBody)
        where T : struct, IJsonElement<T>
    {
        using PooledBufferWriter jsonBuffer = new(formBody.Length * 2);
        using Utf8JsonWriter jsonWriter = new(jsonBuffer);

        FormFieldReader.DeserializeToJson(formBody.Span, jsonWriter);
        jsonWriter.Flush();

        return ParsedJsonDocument<T>.Parse(jsonBuffer.WrittenMemory);
    }

    /// <summary>
    /// Deserializes a <c>application/x-www-form-urlencoded</c> body from a stream into a
    /// <see cref="ParsedJsonDocument{T}"/>.
    /// </summary>
    /// <typeparam name="T">The JSON element type to parse into.</typeparam>
    /// <param name="stream">The request body stream.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A parsed JSON document backed by pooled memory. The caller must dispose it.</returns>
    public static async ValueTask<ParsedJsonDocument<T>> DeserializeAsync<T>(
        Stream stream,
        CancellationToken cancellationToken = default)
        where T : struct, IJsonElement<T>
    {
        (byte[] buffer, int length) = await FormFieldReader.RentBodyAsync(stream, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            return Deserialize<T>(buffer.AsMemory(0, length));
        }
        finally
        {
            FormFieldReader.Return(buffer);
        }
    }

    private static void WriteProperty(
        StreamWriter writer,
        string name,
        JsonElement propValue,
        PropertyEncoding enc,
        ref bool first)
    {
        bool explode = enc.EffectiveExplode;
        string style = enc.Style ?? "form";

        switch (propValue.ValueKind)
        {
            case JsonValueKind.Array when explode:
                WriteExplodedArray(writer, name, propValue, enc.AllowReserved, ref first);
                return;

            case JsonValueKind.Array:
                WritePair(writer, name, FormatArray(propValue, style), enc.AllowReserved, ref first);
                return;

            case JsonValueKind.Object when style == "deepObject":
                WriteDeepObject(writer, name, propValue, enc.AllowReserved, ref first);
                return;

            case JsonValueKind.Object when explode:
                WriteExplodedObject(writer, propValue, enc.AllowReserved, ref first);
                return;

            case JsonValueKind.Object:
                WritePair(writer, name, FormatObject(propValue), enc.AllowReserved, ref first);
                return;

            case JsonValueKind.Null:
                WritePair(writer, name, string.Empty, enc.AllowReserved, ref first);
                return;

            default:
                WritePair(writer, name, GetPrimitiveString(propValue), enc.AllowReserved, ref first);
                return;
        }
    }

    private static void WritePair(
        StreamWriter writer, string name, string value, bool allowReserved, ref bool first)
    {
        if (!first)
        {
            writer.Write('&');
        }

        first = false;
        writer.Write(Encode(name, false));
        writer.Write('=');
        writer.Write(Encode(value, allowReserved));
    }

    private static void WriteExplodedArray(
        StreamWriter writer, string name, JsonElement array, bool allowReserved, ref bool first)
    {
        foreach (JsonElement item in array.EnumerateArray())
        {
            WritePair(writer, name, GetPrimitiveString(item), allowReserved, ref first);
        }
    }

    private static void WriteExplodedObject(
        StreamWriter writer, JsonElement obj, bool allowReserved, ref bool first)
    {
        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            WritePair(writer, prop.Name, GetPrimitiveString(prop.Value), allowReserved, ref first);
        }
    }

    private static void WriteDeepObject(
        StreamWriter writer, string name, JsonElement obj, bool allowReserved, ref bool first)
    {
        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            if (!first)
            {
                writer.Write('&');
            }

            first = false;
            writer.Write(Encode(name, false));
            writer.Write('[');
            writer.Write(Encode(prop.Name, false));
            writer.Write("]=");
            writer.Write(Encode(GetPrimitiveString(prop.Value), allowReserved));
        }
    }

    private static string FormatArray(JsonElement array, string style)
    {
        string separator = style switch
        {
            "spaceDelimited" => " ",
            "pipeDelimited" => "|",
            _ => ",",
        };

        StringBuilder sb = new();
        bool first = true;

        foreach (JsonElement item in array.EnumerateArray())
        {
            if (!first)
            {
                sb.Append(separator);
            }

            first = false;
            sb.Append(GetPrimitiveString(item));
        }

        return sb.ToString();
    }

    private static string FormatObject(JsonElement obj)
    {
        StringBuilder sb = new();
        bool first = true;

        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            sb.Append(prop.Name);
            sb.Append(',');
            sb.Append(GetPrimitiveString(prop.Value));
        }

        return sb.ToString();
    }

    private static string GetPrimitiveString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Null => string.Empty,
            _ => value.ToString(),
        };
    }

    private static string Encode(string value, bool allowReserved)
    {
        if (allowReserved)
        {
            // Only encode characters that are not unreserved or reserved per RFC 3986.
            // Uri.EscapeDataString encodes everything except unreserved, so we
            // un-encode reserved chars after escaping.
            return UnescapeReserved(Uri.EscapeDataString(value));
        }

        return Uri.EscapeDataString(value);
    }

    private static string UnescapeReserved(string encoded)
    {
        // Reserved characters per RFC 3986 §2.2:
        // : / ? # [ ] @ ! $ & ' ( ) * + , ; =
        // These are percent-encoded by EscapeDataString; we undo that.
        return encoded
            .Replace("%3A", ":")
            .Replace("%2F", "/")
            .Replace("%3F", "?")
            .Replace("%23", "#")
            .Replace("%5B", "[")
            .Replace("%5D", "]")
            .Replace("%40", "@")
            .Replace("%21", "!")
            .Replace("%24", "$")
            .Replace("%26", "&")
            .Replace("%27", "'")
            .Replace("%28", "(")
            .Replace("%29", ")")
            .Replace("%2A", "*")
            .Replace("%2B", "+")
            .Replace("%2C", ",")
            .Replace("%3B", ";")
            .Replace("%3D", "=");
    }
}