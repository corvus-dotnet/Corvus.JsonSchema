// <copyright file="JsonToYamlConverter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

#if !STJ
using Corvus.Text.Json.Internal;
#endif

#if STJ
namespace Corvus.Yaml.Internal;
#else
namespace Corvus.Text.Json.Yaml.Internal;
#endif

/// <summary>
/// Converts JSON to YAML by writing to a <see cref="Utf8YamlWriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// Three conversion paths are provided, all as static methods that take
/// the writer by <c>ref</c> so no ref-to-ref-struct field is needed:
/// </para>
/// <list type="number">
/// <item><description>
/// <b>Raw UTF-8 bytes</b> (<see cref="Convert(ref Utf8YamlWriter, ReadOnlySpan{byte})"/>):
/// tokenizes with <see cref="Utf8JsonReader"/> and writes each token directly.
/// Shared across both the STJ and CTJ builds with zero conditional compilation;
/// <c>Utf8JsonReader</c> resolves to the correct type per build via namespace lookup.
/// </description></item>
/// <item><description>
/// <b>STJ element walk</b> (STJ build only): walks
/// <see cref="System.Text.Json.JsonElement"/> using
/// <c>System.Runtime.InteropServices.JsonMarshal</c> for zero-copy raw
/// UTF-8 access to property names, string values, and numbers.
/// </description></item>
/// <item><description>
/// <b>CTJ element walk</b> (CTJ build only): walks any
/// <see cref="IJsonElement{T}"/> implementation through the
/// <see cref="IJsonDocument"/> APIs using index-based
/// <see cref="ObjectEnumerator"/> and <see cref="ArrayEnumerator"/>
/// enumerators. No <c>System.Text.Json</c> references.
/// </description></item>
/// </list>
/// </remarks>
internal static class JsonToYamlConverter
{
    // ================================================================
    // Path 1: Shared UTF-8 reader path
    //
    // Utf8JsonReader resolves to:
    //   STJ build  → System.Text.Json.Utf8JsonReader
    //   CTJ build  → Corvus.Text.Json.Utf8JsonReader
    //
    // Both expose ValueIsEscaped, CopyString, ValueSpan, TokenType.
    // ================================================================

    /// <summary>
    /// Converts UTF-8 JSON bytes to YAML.
    /// </summary>
    /// <param name="writer">The YAML writer to write output to.</param>
    /// <param name="utf8Json">The UTF-8 encoded JSON bytes.</param>
    public static void Convert(ref Utf8YamlWriter writer, ReadOnlySpan<byte> utf8Json)
    {
        Utf8JsonReader reader = new(utf8Json);

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    if (reader.Read() && reader.TokenType == JsonTokenType.EndObject)
                    {
                        writer.WriteEmptyMapping();
                    }
                    else
                    {
                        writer.WriteStartMapping();
                        WriteReaderToken(ref writer, ref reader);
                    }

                    break;

                case JsonTokenType.EndObject:
                    writer.WriteEndMapping();
                    break;

                case JsonTokenType.StartArray:
                    if (reader.Read() && reader.TokenType == JsonTokenType.EndArray)
                    {
                        writer.WriteEmptySequence();
                    }
                    else
                    {
                        writer.WriteStartSequence();
                        WriteReaderToken(ref writer, ref reader);
                    }

                    break;

                case JsonTokenType.EndArray:
                    writer.WriteEndSequence();
                    break;

                case JsonTokenType.PropertyName:
                    WriteReaderStringOrName(ref writer, ref reader, isKey: true);
                    break;

                case JsonTokenType.String:
                    WriteReaderStringOrName(ref writer, ref reader, isKey: false);
                    break;

                case JsonTokenType.Number:
                    writer.WriteNumberValue(reader.ValueSpan);
                    break;

                case JsonTokenType.True:
                    writer.WriteBooleanValue(true);
                    break;

                case JsonTokenType.False:
                    writer.WriteBooleanValue(false);
                    break;

                case JsonTokenType.Null:
                    writer.WriteNullValue();
                    break;
            }
        }
    }

    /// <summary>
    /// Dispatches a single token that has already been read (after a peek-ahead
    /// to detect empty containers).
    /// </summary>
    private static void WriteReaderToken(ref Utf8YamlWriter writer, ref Utf8JsonReader reader)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                if (reader.Read() && reader.TokenType == JsonTokenType.EndObject)
                {
                    writer.WriteEmptyMapping();
                }
                else
                {
                    writer.WriteStartMapping();
                    WriteReaderToken(ref writer, ref reader);
                }

                break;

            case JsonTokenType.StartArray:
                if (reader.Read() && reader.TokenType == JsonTokenType.EndArray)
                {
                    writer.WriteEmptySequence();
                }
                else
                {
                    writer.WriteStartSequence();
                    WriteReaderToken(ref writer, ref reader);
                }

                break;

            case JsonTokenType.PropertyName:
                WriteReaderStringOrName(ref writer, ref reader, isKey: true);
                break;

            case JsonTokenType.String:
                WriteReaderStringOrName(ref writer, ref reader, isKey: false);
                break;

            case JsonTokenType.Number:
                writer.WriteNumberValue(reader.ValueSpan);
                break;

            case JsonTokenType.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonTokenType.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonTokenType.Null:
                writer.WriteNullValue();
                break;
        }
    }

    /// <summary>
    /// Writes a string or property name from a <see cref="Utf8JsonReader"/>,
    /// unescaping JSON escape sequences when present.
    /// </summary>
    private static void WriteReaderStringOrName(ref Utf8YamlWriter writer, scoped ref Utf8JsonReader reader, bool isKey)
    {
        if (!reader.ValueIsEscaped)
        {
            if (isKey)
            {
                writer.WritePropertyName(reader.ValueSpan);
            }
            else
            {
                writer.WriteStringValue(reader.ValueSpan);
            }

            return;
        }

        // Unescape via CopyString (unescaped length <= escaped length).
        byte[]? rentedArray = null;
        int maxLen = reader.ValueSpan.Length;

        Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(maxLen));

        try
        {
            int written = reader.CopyString(buffer);

            if (isKey)
            {
                writer.WritePropertyName(buffer.Slice(0, written));
            }
            else
            {
                writer.WriteStringValue(buffer.Slice(0, written));
            }
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

#if STJ && !BUILDING_SOURCE_GENERATOR
    // ================================================================
    // Path 2: STJ element walk
    //
    // Uses System.Runtime.InteropServices.JsonMarshal for zero-copy
    // raw UTF-8 access to property names, string values, and numbers.
    // ================================================================

    /// <summary>
    /// Converts a <see cref="System.Text.Json.JsonElement"/> to YAML.
    /// </summary>
    /// <param name="writer">The YAML writer to write output to.</param>
    /// <param name="element">The JSON element to convert.</param>
    public static void Convert(ref Utf8YamlWriter writer, System.Text.Json.JsonElement element)
    {
        WriteElement(ref writer, element);
    }

    private static void WriteElement(ref Utf8YamlWriter writer, System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                WriteObject(ref writer, element);
                break;

            case System.Text.Json.JsonValueKind.Array:
                WriteArray(ref writer, element);
                break;

            case System.Text.Json.JsonValueKind.String:
                WriteStjString(ref writer, element);
                break;

            case System.Text.Json.JsonValueKind.Number:
                WriteStjNumber(ref writer, element);
                break;

            case System.Text.Json.JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case System.Text.Json.JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case System.Text.Json.JsonValueKind.Null:
            case System.Text.Json.JsonValueKind.Undefined:
            default:
                writer.WriteNullValue();
                break;
        }
    }

    private static void WriteObject(ref Utf8YamlWriter writer, System.Text.Json.JsonElement element)
    {
        System.Text.Json.JsonElement.ObjectEnumerator enumerator = element.EnumerateObject();

        if (!enumerator.MoveNext())
        {
            writer.WriteEmptyMapping();
            return;
        }

        writer.WriteStartMapping();

        do
        {
            WriteStjPropertyName(ref writer, enumerator.Current);
            WriteElement(ref writer, enumerator.Current.Value);
        }
        while (enumerator.MoveNext());

        writer.WriteEndMapping();
    }

    private static void WriteArray(ref Utf8YamlWriter writer, System.Text.Json.JsonElement element)
    {
        if (element.GetArrayLength() == 0)
        {
            writer.WriteEmptySequence();
            return;
        }

        writer.WriteStartSequence();

        foreach (System.Text.Json.JsonElement item in element.EnumerateArray())
        {
            WriteElement(ref writer, item);
        }

        writer.WriteEndSequence();
    }

    private static void WriteStjPropertyName(ref Utf8YamlWriter writer, System.Text.Json.JsonProperty property)
    {
        ReadOnlySpan<byte> raw =
            System.Runtime.InteropServices.JsonMarshal.GetRawUtf8PropertyName(property);

        if (raw.IndexOf((byte)'\\') < 0)
        {
            writer.WritePropertyName(raw);
            return;
        }

        UnescapeRawJsonAndWrite(ref writer, raw, isKey: true);
    }

    private static void WriteStjString(ref Utf8YamlWriter writer, System.Text.Json.JsonElement element)
    {
        ReadOnlySpan<byte> rawWithQuotes =
            System.Runtime.InteropServices.JsonMarshal.GetRawUtf8Value(element);

        // Strip the enclosing double quotes.
        ReadOnlySpan<byte> raw = rawWithQuotes.Slice(1, rawWithQuotes.Length - 2);

        if (raw.IndexOf((byte)'\\') < 0)
        {
            writer.WriteStringValue(raw);
            return;
        }

        UnescapeRawJsonAndWrite(ref writer, raw, isKey: false);
    }

    private static void WriteStjNumber(ref Utf8YamlWriter writer, System.Text.Json.JsonElement element)
    {
        ReadOnlySpan<byte> raw =
            System.Runtime.InteropServices.JsonMarshal.GetRawUtf8Value(element);
        writer.WriteNumberValue(raw);
    }

    /// <summary>
    /// Unescapes raw JSON-escaped UTF-8 bytes (without enclosing quotes)
    /// by wrapping them in quotes and parsing with <see cref="System.Text.Json.Utf8JsonReader"/>.
    /// The unescape logic is inlined (rather than delegating to
    /// <see cref="WriteReaderStringOrName"/>) to avoid passing a reader
    /// backed by stackalloc data across a method boundary, which the
    /// compiler rejects as a ref-safety violation.
    /// </summary>
    private static void UnescapeRawJsonAndWrite(ref Utf8YamlWriter writer, ReadOnlySpan<byte> rawEscaped, bool isKey)
    {
        byte[]? jsonRented = null;
        int jsonLen = rawEscaped.Length + 2;

        Span<byte> json = jsonLen <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (jsonRented = ArrayPool<byte>.Shared.Rent(jsonLen));

        try
        {
            json[0] = (byte)'"';
            rawEscaped.CopyTo(json.Slice(1));
            json[rawEscaped.Length + 1] = (byte)'"';

            System.Text.Json.Utf8JsonReader reader = new(json.Slice(0, jsonLen));
            reader.Read();

            // Inlined from WriteReaderStringOrName: unescape via CopyString.
            if (!reader.ValueIsEscaped)
            {
                if (isKey)
                {
                    writer.WritePropertyName(reader.ValueSpan);
                }
                else
                {
                    writer.WriteStringValue(reader.ValueSpan);
                }
            }
            else
            {
                byte[]? unescapeRented = null;
                int maxLen = reader.ValueSpan.Length;

                Span<byte> buffer = maxLen <= JsonConstants.StackallocByteThreshold
                    ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                    : (unescapeRented = ArrayPool<byte>.Shared.Rent(maxLen));

                try
                {
                    int written = reader.CopyString(buffer);

                    if (isKey)
                    {
                        writer.WritePropertyName(buffer.Slice(0, written));
                    }
                    else
                    {
                        writer.WriteStringValue(buffer.Slice(0, written));
                    }
                }
                finally
                {
                    if (unescapeRented is not null)
                    {
                        ArrayPool<byte>.Shared.Return(unescapeRented);
                    }
                }
            }
        }
        finally
        {
            if (jsonRented is not null)
            {
                ArrayPool<byte>.Shared.Return(jsonRented);
            }
        }
    }

#elif !STJ
    // ================================================================
    // Path 3: CTJ element walk (generic over IJsonElement<T>)
    //
    // Walks the element tree through IJsonDocument APIs.
    // Uses Internal.ObjectEnumerator and Internal.ArrayEnumerator
    // for zero-allocation index-based iteration.
    // No System.Text.Json references in this section.
    // ================================================================

    /// <summary>
    /// Converts a JSON element to YAML by walking the parent document tree.
    /// </summary>
    /// <typeparam name="TElement">The type of the JSON element.</typeparam>
    /// <param name="writer">The YAML writer to write output to.</param>
    /// <param name="element">The JSON element to convert.</param>
    public static void Convert<TElement>(ref Utf8YamlWriter writer, in TElement element)
        where TElement : struct, IJsonElement<TElement>
    {
        ConvertElement(ref writer, element.ParentDocument, element.ParentDocumentIndex);
    }

    private static void ConvertElement(ref Utf8YamlWriter writer, IJsonDocument doc, int index)
    {
        JsonTokenType tokenType = doc.GetJsonTokenType(index);

        switch (tokenType)
        {
            case JsonTokenType.StartObject:
                WriteObject(ref writer, doc, index);
                break;

            case JsonTokenType.StartArray:
                WriteArray(ref writer, doc, index);
                break;

            case JsonTokenType.String:
                WriteCtjString(ref writer, doc, index);
                break;

            case JsonTokenType.Number:
                WriteCtjNumber(ref writer, doc, index);
                break;

            case JsonTokenType.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonTokenType.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonTokenType.Null:
            default:
                writer.WriteNullValue();
                break;
        }
    }

    private static void WriteObject(ref Utf8YamlWriter writer, IJsonDocument doc, int index)
    {
        if (doc.GetPropertyCount(index) == 0)
        {
            writer.WriteEmptyMapping();
            return;
        }

        writer.WriteStartMapping();

        ObjectEnumerator objEnum = new(doc, index);
        while (objEnum.MoveNext())
        {
            int valueIndex = objEnum.CurrentIndex;
            using UnescapedUtf8JsonString name = doc.GetPropertyNameUnescaped(valueIndex);
            writer.WritePropertyName(name.Span);
            ConvertElement(ref writer, doc, valueIndex);
        }

        writer.WriteEndMapping();
    }

    private static void WriteArray(ref Utf8YamlWriter writer, IJsonDocument doc, int index)
    {
        if (doc.GetArrayLength(index) == 0)
        {
            writer.WriteEmptySequence();
            return;
        }

        writer.WriteStartSequence();

        ArrayEnumerator arrEnum = new(doc, index);
        while (arrEnum.MoveNext())
        {
            ConvertElement(ref writer, doc, arrEnum.CurrentIndex);
        }

        writer.WriteEndSequence();
    }

    private static void WriteCtjString(ref Utf8YamlWriter writer, IJsonDocument doc, int index)
    {
        using UnescapedUtf8JsonString str = doc.GetUtf8JsonString(index, JsonTokenType.String);
        writer.WriteStringValue(str.Span);
    }

    private static void WriteCtjNumber(ref Utf8YamlWriter writer, IJsonDocument doc, int index)
    {
        ReadOnlyMemory<byte> raw = doc.GetRawSimpleValue(index);
        writer.WriteNumberValue(raw.Span);
    }

#endif
}