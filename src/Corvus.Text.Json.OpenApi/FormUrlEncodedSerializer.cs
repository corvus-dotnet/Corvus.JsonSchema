// <copyright file="FormUrlEncodedSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
    // Worst-case percent-encoding expands every byte to 3 bytes (%XX).
    private const int PercentEncodingExpansionFactor = 3;

    // Initial scratch buffer size for encoding work.
    private const int InitialScratchSize = 1024;

    private static readonly byte[] AmpersandByte = [(byte)'&'];
    private static readonly byte[] EqualsByte = [(byte)'='];
    private static readonly byte[] OpenBracketByte = [(byte)'['];
    private static readonly byte[] CloseBracketEqualsByte = [(byte)']', (byte)'='];
    private static readonly byte[] CommaByte = [(byte)','];
    private static readonly byte[] SpaceByte = [(byte)' '];
    private static readonly byte[] PipeByte = [(byte)'|'];

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
        Dictionary<string, PropertyEncoding>? encodings)
        where T : struct, IJsonElement<T>
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowFormBodyMustBeObject();
        }

        // Two separate buffers: valueBuf for formatting JSON values, encodeBuf for percent-encoding.
        // Keeping them separate avoids ref-struct lifetime issues (CS8350).
        byte[] valueBuf = ArrayPool<byte>.Shared.Rent(InitialScratchSize);
        byte[] encodeBuf = ArrayPool<byte>.Shared.Rent(InitialScratchSize);
        char[] charBuf = ArrayPool<char>.Shared.Rent(128);

        try
        {
            bool first = true;

            // AlternateLookup avoids allocating a string for each property name lookup.
            Dictionary<string, PropertyEncoding>.AlternateLookup<ReadOnlySpan<char>> altLookup = default;
            if (encodings is not null)
            {
                altLookup = encodings.GetAlternateLookup<ReadOnlySpan<char>>();
            }

            foreach (JsonProperty<JsonElement> property in JsonElement.From(value).EnumerateObject())
            {
                JsonElement propValue = property.Value;

                PropertyEncoding enc = default;
                if (encodings is not null)
                {
                    using UnescapedUtf8JsonString utf8Name = property.Utf8NameSpan;
                    ReadOnlySpan<byte> nameBytes = utf8Name.Span;

                    int maxChars = System.Text.Encoding.UTF8.GetMaxCharCount(nameBytes.Length);
                    if (charBuf.Length < maxChars)
                    {
                        ArrayPool<char>.Shared.Return(charBuf);
                        charBuf = ArrayPool<char>.Shared.Rent(maxChars);
                    }

                    int charCount = System.Text.Encoding.UTF8.GetChars(nameBytes, charBuf);
                    altLookup.TryGetValue(charBuf.AsSpan(0, charCount), out enc);
                }

                WriteProperty(output, property, propValue, enc, ref first, ref valueBuf, ref encodeBuf);
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(charBuf);
            ArrayPool<byte>.Shared.Return(valueBuf);
            ArrayPool<byte>.Shared.Return(encodeBuf);
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
        Stream output,
        JsonProperty<JsonElement> property,
        JsonElement propValue,
        PropertyEncoding enc,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        bool explode = enc.EffectiveExplode;
        string style = enc.Style ?? "form";

        switch (propValue.ValueKind)
        {
            case JsonValueKind.Array when explode:
                WriteExplodedArray(output, property, propValue, enc.AllowReserved, ref first, ref valueBuf, ref encodeBuf);
                return;

            case JsonValueKind.Array:
                WriteArrayPair(output, property, propValue, style, enc.AllowReserved, ref first, ref valueBuf, ref encodeBuf);
                return;

            case JsonValueKind.Object when style == "deepObject":
                WriteDeepObject(output, property, propValue, enc.AllowReserved, ref first, ref valueBuf, ref encodeBuf);
                return;

            case JsonValueKind.Object when explode:
                WriteExplodedObject(output, propValue, enc.AllowReserved, ref first, ref valueBuf, ref encodeBuf);
                return;

            case JsonValueKind.Object:
                WriteObjectPair(output, property, propValue, enc.AllowReserved, ref first, ref valueBuf, ref encodeBuf);
                return;

            case JsonValueKind.Null:
                WritePairFromNameAndEmptyValue(output, property, enc.AllowReserved, ref first, ref encodeBuf);
                return;

            default:
                WritePrimitivePair(output, property, propValue, enc.AllowReserved, ref first, ref valueBuf, ref encodeBuf);
                return;
        }
    }

    private static void WritePrimitivePair(
        Stream output,
        JsonProperty<JsonElement> property,
        JsonElement value,
        bool allowReserved,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        int valueLen = FormatPrimitiveUtf8(value, ref valueBuf);
        WritePairFromNameAndFormattedValue(output, property, valueBuf.AsSpan(0, valueLen), allowReserved, ref first, ref encodeBuf);
    }

    private static void WritePairFromNameAndEmptyValue(
        Stream output,
        JsonProperty<JsonElement> property,
        bool allowReserved,
        ref bool first,
        ref byte[] encodeBuf)
    {
        if (!first)
        {
            output.Write(AmpersandByte);
        }

        first = false;

        using UnescapedUtf8JsonString utf8Name = property.Utf8NameSpan;
        WriteEncoded(output, utf8Name.Span, allowReserved: false, ref encodeBuf);
        output.Write(EqualsByte);
    }

    private static void WritePairFromNameAndFormattedValue(
        Stream output,
        JsonProperty<JsonElement> property,
        ReadOnlySpan<byte> valueUtf8,
        bool allowReserved,
        ref bool first,
        ref byte[] encodeBuf)
    {
        if (!first)
        {
            output.Write(AmpersandByte);
        }

        first = false;

        using UnescapedUtf8JsonString utf8Name = property.Utf8NameSpan;
        WriteEncoded(output, utf8Name.Span, allowReserved: false, ref encodeBuf);
        output.Write(EqualsByte);
        WriteEncoded(output, valueUtf8, allowReserved, ref encodeBuf);
    }

    private static void WritePairFromUtf8NameAndFormattedValue(
        Stream output,
        ReadOnlySpan<byte> nameUtf8,
        ReadOnlySpan<byte> valueUtf8,
        bool allowReserved,
        ref bool first,
        ref byte[] encodeBuf)
    {
        if (!first)
        {
            output.Write(AmpersandByte);
        }

        first = false;

        WriteEncoded(output, nameUtf8, allowReserved: false, ref encodeBuf);
        output.Write(EqualsByte);
        WriteEncoded(output, valueUtf8, allowReserved, ref encodeBuf);
    }

    private static void WriteExplodedArray(
        Stream output,
        JsonProperty<JsonElement> property,
        JsonElement array,
        bool allowReserved,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        using UnescapedUtf8JsonString utf8Name = property.Utf8NameSpan;
        ReadOnlySpan<byte> nameBytes = utf8Name.Span;

        foreach (JsonElement item in array.EnumerateArray())
        {
            int itemLen = FormatPrimitiveUtf8(item, ref valueBuf);
            WritePairFromUtf8NameAndFormattedValue(output, nameBytes, valueBuf.AsSpan(0, itemLen), allowReserved, ref first, ref encodeBuf);
        }
    }

    private static void WriteExplodedObject(
        Stream output,
        JsonElement obj,
        bool allowReserved,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            int valueLen = FormatPrimitiveUtf8(prop.Value, ref valueBuf);
            using UnescapedUtf8JsonString propName = prop.Utf8NameSpan;
            WritePairFromUtf8NameAndFormattedValue(output, propName.Span, valueBuf.AsSpan(0, valueLen), allowReserved, ref first, ref encodeBuf);
        }
    }

    private static void WriteDeepObject(
        Stream output,
        JsonProperty<JsonElement> property,
        JsonElement obj,
        bool allowReserved,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        using UnescapedUtf8JsonString outerName = property.Utf8NameSpan;
        ReadOnlySpan<byte> outerNameBytes = outerName.Span;

        foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
        {
            if (!first)
            {
                output.Write(AmpersandByte);
            }

            first = false;

            // Write: encodedOuterName[encodedInnerName]=encodedValue
            WriteEncoded(output, outerNameBytes, allowReserved: false, ref encodeBuf);
            output.Write(OpenBracketByte);

            using UnescapedUtf8JsonString innerName = prop.Utf8NameSpan;
            WriteEncoded(output, innerName.Span, allowReserved: false, ref encodeBuf);

            output.Write(CloseBracketEqualsByte);

            int valueLen = FormatPrimitiveUtf8(prop.Value, ref valueBuf);
            WriteEncoded(output, valueBuf.AsSpan(0, valueLen), allowReserved, ref encodeBuf);
        }
    }

    private static void WriteArrayPair(
        Stream output,
        JsonProperty<JsonElement> property,
        JsonElement array,
        string style,
        bool allowReserved,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        // Build the comma/pipe/space-separated value in a temporary buffer.
        ReadOnlySpan<byte> separator = style switch
        {
            "spaceDelimited" => SpaceByte,
            "pipeDelimited" => PipeByte,
            _ => CommaByte,
        };

        byte[] compositeBuf = ArrayPool<byte>.Shared.Rent(InitialScratchSize);

        try
        {
            int written = 0;
            bool firstItem = true;

            foreach (JsonElement item in array.EnumerateArray())
            {
                if (!firstItem)
                {
                    EnsureCapacity(ref compositeBuf, written + separator.Length);
                    separator.CopyTo(compositeBuf.AsSpan(written));
                    written += separator.Length;
                }

                firstItem = false;

                int itemLen = FormatPrimitiveUtf8(item, ref valueBuf);
                ReadOnlySpan<byte> itemBytes = valueBuf.AsSpan(0, itemLen);
                EnsureCapacity(ref compositeBuf, written + itemLen);
                itemBytes.CopyTo(compositeBuf.AsSpan(written));
                written += itemLen;
            }

            WritePairFromNameAndFormattedValue(output, property, compositeBuf.AsSpan(0, written), allowReserved, ref first, ref encodeBuf);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compositeBuf);
        }
    }

    private static void WriteObjectPair(
        Stream output,
        JsonProperty<JsonElement> property,
        JsonElement obj,
        bool allowReserved,
        ref bool first,
        ref byte[] valueBuf,
        ref byte[] encodeBuf)
    {
        // Format: name1,value1,name2,value2,...
        byte[] compositeBuf = ArrayPool<byte>.Shared.Rent(InitialScratchSize);

        try
        {
            int written = 0;
            bool firstProp = true;

            foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
            {
                if (!firstProp)
                {
                    EnsureCapacity(ref compositeBuf, written + 1);
                    compositeBuf[written++] = (byte)',';
                }

                firstProp = false;

                // Write property name
                using UnescapedUtf8JsonString propName = prop.Utf8NameSpan;
                ReadOnlySpan<byte> nameBytes = propName.Span;
                EnsureCapacity(ref compositeBuf, written + nameBytes.Length + 1);
                nameBytes.CopyTo(compositeBuf.AsSpan(written));
                written += nameBytes.Length;

                // Comma separator between name and value
                compositeBuf[written++] = (byte)',';

                // Write value
                int valueLen = FormatPrimitiveUtf8(prop.Value, ref valueBuf);
                ReadOnlySpan<byte> valueBytes = valueBuf.AsSpan(0, valueLen);
                EnsureCapacity(ref compositeBuf, written + valueLen);
                valueBytes.CopyTo(compositeBuf.AsSpan(written));
                written += valueLen;
            }

            WritePairFromNameAndFormattedValue(output, property, compositeBuf.AsSpan(0, written), allowReserved, ref first, ref encodeBuf);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(compositeBuf);
        }
    }

    /// <summary>
    /// Formats a JSON value as UTF-8 bytes into <paramref name="valueBuf"/>,
    /// growing it if necessary. Returns the number of bytes written.
    /// </summary>
    private static int FormatPrimitiveUtf8(JsonElement value, ref byte[] valueBuf)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                EnsureCapacity(ref valueBuf, 4);
                "True"u8.CopyTo(valueBuf);
                return 4;

            case JsonValueKind.False:
                EnsureCapacity(ref valueBuf, 5);
                "False"u8.CopyTo(valueBuf);
                return 5;

            case JsonValueKind.Null:
                return 0;

            default:
                // Numbers, strings, arrays, objects all use TryFormat.
                int written;
                while (!value.TryFormat(valueBuf, out written, default, null))
                {
                    GrowBuffer(ref valueBuf, valueBuf.Length * 2);
                }

                return written;
        }
    }

    /// <summary>
    /// Writes the percent-encoded form of <paramref name="utf8Bytes"/> directly to the stream.
    /// Uses <see cref="Utf8Uri.TryEscapeUri"/> when <paramref name="allowReserved"/> is true,
    /// or <see cref="Utf8Uri.TryEscapeDataString"/> when false.
    /// </summary>
    private static void WriteEncoded(
        Stream output,
        ReadOnlySpan<byte> utf8Bytes,
        bool allowReserved,
        ref byte[] encodeBuf)
    {
        if (utf8Bytes.IsEmpty)
        {
            return;
        }

        // Ensure buffer is large enough for worst-case encoding (3x expansion).
        int requiredSize = utf8Bytes.Length * PercentEncodingExpansionFactor;
        EnsureCapacity(ref encodeBuf, requiredSize);

        bool success;
        int bytesWritten;

        if (allowReserved)
        {
            success = Utf8Uri.TryEscapeUri(utf8Bytes, encodeBuf, out bytesWritten);
        }
        else
        {
            success = Utf8Uri.TryEscapeDataString(utf8Bytes, encodeBuf, out bytesWritten);
        }

        if (success)
        {
            output.Write(encodeBuf.AsSpan(0, bytesWritten));
        }
        else
        {
            // Should not happen given our buffer sizing, but handle gracefully.
            GrowBuffer(ref encodeBuf, requiredSize * 2);

            if (allowReserved)
            {
                Utf8Uri.TryEscapeUri(utf8Bytes, encodeBuf, out bytesWritten);
            }
            else
            {
                Utf8Uri.TryEscapeDataString(utf8Bytes, encodeBuf, out bytesWritten);
            }

            output.Write(encodeBuf.AsSpan(0, bytesWritten));
        }
    }

    private static void GrowBuffer(ref byte[] buffer, int minimumSize)
    {
        byte[] oldBuffer = buffer;
        buffer = ArrayPool<byte>.Shared.Rent(minimumSize);
        ArrayPool<byte>.Shared.Return(oldBuffer);
    }

    private static void EnsureCapacity(ref byte[] buffer, int requiredTotal)
    {
        if (buffer.Length >= requiredTotal)
        {
            return;
        }

        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(requiredTotal * 2);
        buffer.AsSpan().CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }
}