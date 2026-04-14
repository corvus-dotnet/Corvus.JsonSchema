// <copyright file="JsonataHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Helper methods for creating JSON elements from native values using the
/// Corvus.Text.Json pooled document infrastructure. All methods avoid
/// <c>MemoryStream</c>, <c>Utf8JsonWriter</c>, and <c>JsonDocument.Parse</c>
/// by working directly with UTF-8 byte buffers and <see cref="FixedJsonValueDocument{T}"/>.
/// </summary>
internal static class JsonataHelpers
{
    // Pre-cached immutable constants (thread-safe, never disposed).
    private static readonly JsonElement TrueElement = ParsedJsonDocument<JsonElement>.True;
    private static readonly JsonElement FalseElement = ParsedJsonDocument<JsonElement>.False;
    private static readonly JsonElement NullElementValue = ParsedJsonDocument<JsonElement>.Null;
    private static readonly JsonElement ZeroElement = ParsedJsonDocument<JsonElement>.NumberConstant("0"u8.ToArray());
    private static readonly JsonElement OneElement = ParsedJsonDocument<JsonElement>.NumberConstant("1"u8.ToArray());
    private static readonly JsonElement MinusOneElement = ParsedJsonDocument<JsonElement>.NumberConstant("-1"u8.ToArray());
    private static readonly JsonElement EmptyStringElement = ParsedJsonDocument<JsonElement>.StringConstant("\"\""u8.ToArray());
    private static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);

    /// <summary>
    /// Gets the cached <c>true</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement True() => TrueElement;

    /// <summary>
    /// Gets the cached <c>false</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement False() => FalseElement;

    /// <summary>
    /// Gets the cached <c>null</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement Null() => NullElementValue;

    /// <summary>
    /// Gets the cached <c>0</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement Zero() => ZeroElement;

    /// <summary>
    /// Gets the cached <c>1</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement One() => OneElement;

    /// <summary>
    /// Gets the cached <c>-1</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement MinusOne() => MinusOneElement;

    /// <summary>
    /// Gets the cached <c>""</c> (empty string) element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement EmptyString() => EmptyStringElement;

    /// <summary>
    /// Gets the cached <c>[]</c> (empty array) element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement EmptyArray() => EmptyArrayElement;

    /// <summary>
    /// Returns the cached <c>true</c> or <c>false</c> element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement BooleanElement(bool value) => value ? TrueElement : FalseElement;

    /// <summary>
    /// Creates a number element from a <see cref="double"/> value by formatting
    /// into a stack-allocated UTF-8 buffer and using <see cref="FixedJsonValueDocument{T}"/>.
    /// </summary>
    /// <param name="value">The number value. Must not be NaN or Infinity.</param>
    /// <param name="workspace">The workspace to register the document in.</param>
    /// <returns>A pooled <see cref="JsonElement"/> wrapping the number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromDouble(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];

#if NET
        if (!Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            // Fallback for very long representations
            return NumberFromDoubleSlow(value, workspace);
        }
#else
        // On netstandard2.0 / .NET Framework, Utf8Formatter uses G17 which produces
        // non-shortest representations (e.g. 39.4 → "39.399999999999999"). Use "R"
        // format via ToString which produces the shortest roundtrip form, then transcode.
        int bytesWritten = FormatDoubleRoundtrip(value, buffer);
#endif

        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(buffer.Slice(0, bytesWritten));
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a number element from a <see cref="long"/> value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromLong(long value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[20]; // -9223372036854775808

        if (!Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            ThrowFormatFailed();
            bytesWritten = 0; // unreachable
        }

        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(buffer.Slice(0, bytesWritten));
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a number element from pre-formatted UTF-8 number bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromUtf8Span(ReadOnlySpan<byte> utf8NumberBytes, JsonWorkspace workspace)
    {
        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(utf8NumberBytes);
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a string element from a .NET <see cref="string"/> by encoding to
    /// quoted, JSON-escaped UTF-8 in a rented buffer.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <param name="workspace">The workspace to register the document in.</param>
    /// <returns>A pooled <see cref="JsonElement"/> wrapping the string.</returns>
    public static JsonElement StringFromString(string value, JsonWorkspace workspace)
    {
        if (value.Length == 0)
        {
            return EmptyStringElement;
        }

        // Worst case: each char → 6 bytes (\uXXXX) + 2 for quotes
        int maxLen = (value.Length * 6) + 2;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLen);
        int pos = 0;

        buffer[pos++] = (byte)'"';
        pos += WriteJsonEscapedUtf8(value.AsSpan(), buffer.AsSpan(pos));
        buffer[pos++] = (byte)'"';

        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
            new ReadOnlySpan<byte>(buffer, 0, pos));
        workspace.RegisterDocument(doc);
        ArrayPool<byte>.Shared.Return(buffer);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a string element from a <see cref="ReadOnlySpan{T}"/> of chars.
    /// </summary>
    public static JsonElement StringFromChars(ReadOnlySpan<char> value, JsonWorkspace workspace)
    {
        if (value.Length == 0)
        {
            return EmptyStringElement;
        }

        int maxLen = (value.Length * 6) + 2;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLen);
        int pos = 0;

        buffer[pos++] = (byte)'"';
        pos += WriteJsonEscapedUtf8(value, buffer.AsSpan(pos));
        buffer[pos++] = (byte)'"';

        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
            new ReadOnlySpan<byte>(buffer, 0, pos));
        workspace.RegisterDocument(doc);
        ArrayPool<byte>.Shared.Return(buffer);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a string element from already-quoted UTF-8 bytes (including surrounding quotes).
    /// The span content is copied into a pooled document buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement StringFromQuotedUtf8Span(ReadOnlySpan<byte> quotedUtf8, JsonWorkspace workspace)
    {
        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan(quotedUtf8);
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a string element from unescaped, unquoted raw UTF-8 content bytes.
    /// Wraps in quotes and creates a pooled document. The content must already be
    /// valid JSON string content (properly escaped).
    /// </summary>
    public static JsonElement StringFromRawUtf8Content(ReadOnlySpan<byte> rawUtf8Content, JsonWorkspace workspace)
    {
        if (rawUtf8Content.Length == 0)
        {
            return EmptyStringElement;
        }

        int totalLen = rawUtf8Content.Length + 2;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(totalLen);

        buffer[0] = (byte)'"';
        rawUtf8Content.CopyTo(buffer.AsSpan(1));
        buffer[rawUtf8Content.Length + 1] = (byte)'"';

        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
            new ReadOnlySpan<byte>(buffer, 0, totalLen));
        workspace.RegisterDocument(doc);
        ArrayPool<byte>.Shared.Return(buffer);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a string element from unescaped UTF-8 bytes. The bytes are JSON-escaped,
    /// quoted, and stored in a pooled document.
    /// </summary>
    /// <param name="unescapedUtf8">The unescaped UTF-8 bytes (e.g. from <see cref="UnescapedUtf8JsonString"/>).</param>
    /// <param name="workspace">The workspace for the pooled document.</param>
    /// <returns>A string element containing the escaped value.</returns>
    public static JsonElement StringFromUnescapedUtf8(ReadOnlySpan<byte> unescapedUtf8, JsonWorkspace workspace)
    {
        if (unescapedUtf8.Length == 0)
        {
            return EmptyStringElement;
        }

        // Worst case: each byte < 0x20 → 6 bytes (\u00XX) + 2 for quotes
        int maxLen = (unescapedUtf8.Length * 6) + 2;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maxLen);
        int pos = 0;

        buffer[pos++] = (byte)'"';
        pos += WriteJsonEscapedFromUtf8(unescapedUtf8, buffer.AsSpan(pos));
        buffer[pos++] = (byte)'"';

        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
            new ReadOnlySpan<byte>(buffer, 0, pos));
        workspace.RegisterDocument(doc);
        ArrayPool<byte>.Shared.Return(buffer);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a JSON array element from the values in a <see cref="Sequence"/>.
    /// </summary>
    public static JsonElement ArrayFromSequence(Sequence seq, JsonWorkspace workspace)
    {
        if (seq.Count == 0)
        {
            return EmptyArrayElement;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, seq.Count);
        JsonElement.Mutable root = doc.RootElement;

        if (seq.IsRawDoubleArray)
        {
            // Write doubles directly via Source(double) → SimpleTypesBacking,
            // bypassing FixedJsonValueDocument entirely.
            for (int i = 0; i < seq.Count; i++)
            {
                root.AddItem((JsonElement.Source)seq.GetRawDoubleAt(i));
            }
        }
        else
        {
            for (int i = 0; i < seq.Count; i++)
            {
                root.AddItem(seq[i]);
            }
        }

        return (JsonElement)root;
    }

    /// <summary>
    /// Creates a JSON array element from a list of elements.
    /// </summary>
    public static JsonElement ArrayFromList(List<JsonElement> elements, JsonWorkspace workspace)
    {
        if (elements.Count == 0)
        {
            return EmptyArrayElement;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, elements.Count);
        JsonElement.Mutable root = doc.RootElement;

        for (int i = 0; i < elements.Count; i++)
        {
            root.AddItem(elements[i]);
        }

        return (JsonElement)root;
    }

    /// <summary>
    /// Creates a JSON array element from a <see cref="SequenceBuilder"/>.
    /// </summary>
    public static JsonElement ArrayFromBuilder(ref SequenceBuilder builder, JsonWorkspace workspace)
    {
        if (builder.Count == 0)
        {
            return EmptyArrayElement;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, builder.Count);
        JsonElement.Mutable root = doc.RootElement;

        for (int i = 0; i < builder.Count; i++)
        {
            root.AddItem(builder[i]);
        }

        builder.ReturnArray();
        return (JsonElement)root;
    }

    /// <summary>
    /// Creates a JSON array element from a read-only list of elements.
    /// </summary>
    public static JsonElement ArrayFromReadOnlyList(IReadOnlyList<JsonElement> items, JsonWorkspace workspace)
    {
        if (items.Count == 0)
        {
            return EmptyArrayElement;
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateArrayBuilder(workspace, items.Count);
        JsonElement.Mutable root = doc.RootElement;

        for (int i = 0; i < items.Count; i++)
        {
            root.AddItem(items[i]);
        }

        return (JsonElement)root;
    }

    /// <summary>
    /// Creates a regex match object: <c>{"match":"...","index":N,"groups":[...]}</c>.
    /// </summary>
    public static JsonElement CreateMatchObject(string match, int index, IReadOnlyList<string> groups, JsonWorkspace workspace)
    {
        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateObjectBuilder(workspace, 3);
        JsonElement.Mutable root = doc.RootElement;

        root.SetProperty("match"u8, StringFromString(match, workspace));
        root.SetProperty("index"u8, NumberFromLong(index, workspace));

        // Build groups array
        JsonDocumentBuilder<JsonElement.Mutable> groupsDoc = JsonElement.CreateArrayBuilder(workspace, groups.Count);
        JsonElement.Mutable groupsRoot = groupsDoc.RootElement;
        foreach (string g in groups)
        {
            groupsRoot.AddItem(StringFromString(g, workspace));
        }

        root.SetProperty("groups"u8, (JsonElement)groupsRoot);

        return (JsonElement)root;
    }

    /// <summary>
    /// Appends the coerced UTF-8 representation of a JSON element to a rented byte buffer.
    /// For strings, copies the raw escaped UTF-8 content (excluding quotes).
    /// For numbers, copies the raw UTF-8 digits. For booleans/null, appends the literal text.
    /// </summary>
    public static void AppendCoercedToBuffer(in JsonElement elem, ref byte[] buffer, ref int pos)
    {
        if (elem.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        switch (elem.ValueKind)
        {
            case JsonValueKind.String:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(elem);
                ReadOnlySpan<byte> span = raw.Span;
                if (span.Length > 2)
                {
                    int contentLen = span.Length - 2;
                    GrowBufferIfNeeded(ref buffer, pos, contentLen);
                    span.Slice(1, contentLen).CopyTo(buffer.AsSpan(pos));
                    pos += contentLen;
                }

                break;
            }

            case JsonValueKind.Number:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(elem);
                ReadOnlySpan<byte> span = raw.Span;
                GrowBufferIfNeeded(ref buffer, pos, span.Length);
                span.CopyTo(buffer.AsSpan(pos));
                pos += span.Length;
                break;
            }

            case JsonValueKind.True:
                GrowBufferIfNeeded(ref buffer, pos, 4);
                "true"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;
            case JsonValueKind.False:
                GrowBufferIfNeeded(ref buffer, pos, 5);
                "false"u8.CopyTo(buffer.AsSpan(pos));
                pos += 5;
                break;
            case JsonValueKind.Null:
                GrowBufferIfNeeded(ref buffer, pos, 4);
                "null"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;
        }
    }

    /// <summary>
    /// Grows a rented buffer if needed to accommodate additional bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrowBufferIfNeeded(ref byte[] buffer, int pos, int needed)
    {
        if (pos + needed > buffer.Length)
        {
            GrowBuffer(ref buffer, pos, needed);
        }
    }

    /// <summary>
    /// Writes JSON-escaped UTF-8 bytes from a span of chars into a destination byte span.
    /// Returns the number of bytes written. Characters that require JSON escaping
    /// (<c>"</c>, <c>\</c>, control characters U+0000–U+001F) are escaped.
    /// All other characters are encoded as UTF-8.
    /// </summary>
    public static int WriteJsonEscapedUtf8(ReadOnlySpan<char> source, Span<byte> destination)
    {
        int pos = 0;

        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];

            if (c == '"')
            {
                destination[pos++] = (byte)'\\';
                destination[pos++] = (byte)'"';
            }
            else if (c == '\\')
            {
                destination[pos++] = (byte)'\\';
                destination[pos++] = (byte)'\\';
            }
            else if (c < 0x20)
            {
                // Control character escaping
                switch (c)
                {
                    case '\b':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'b';
                        break;
                    case '\f':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'f';
                        break;
                    case '\n':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'n';
                        break;
                    case '\r':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'r';
                        break;
                    case '\t':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'t';
                        break;
                    default:
                        // \u00XX
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'u';
                        destination[pos++] = (byte)'0';
                        destination[pos++] = (byte)'0';
                        destination[pos++] = HexDigit(c >> 4);
                        destination[pos++] = HexDigit(c & 0xF);
                        break;
                }
            }
            else if (c < 0x80)
            {
                // ASCII passthrough (relaxed escaping — no HTML entity encoding)
                destination[pos++] = (byte)c;
            }
            else if (char.IsHighSurrogate(c) && i + 1 < source.Length && char.IsLowSurrogate(source[i + 1]))
            {
                // Surrogate pair → 4-byte UTF-8
                int codePoint = char.ConvertToUtf32(c, source[i + 1]);
                i++;
                destination[pos++] = (byte)(0xF0 | (codePoint >> 18));
                destination[pos++] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
                destination[pos++] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
                destination[pos++] = (byte)(0x80 | (codePoint & 0x3F));
            }
            else if (c < 0x800)
            {
                // 2-byte UTF-8
                destination[pos++] = (byte)(0xC0 | (c >> 6));
                destination[pos++] = (byte)(0x80 | (c & 0x3F));
            }
            else
            {
                // 3-byte UTF-8
                destination[pos++] = (byte)(0xE0 | (c >> 12));
                destination[pos++] = (byte)(0x80 | ((c >> 6) & 0x3F));
                destination[pos++] = (byte)(0x80 | (c & 0x3F));
            }
        }

        return pos;
    }

    /// <summary>
    /// Writes JSON-escaped UTF-8 bytes from an unescaped UTF-8 source.
    /// Multi-byte UTF-8 sequences (bytes ≥ 0x80) pass through unchanged since they
    /// never require JSON escaping. Only ASCII control characters, backslash, and
    /// double-quote need escaping.
    /// </summary>
    public static int WriteJsonEscapedFromUtf8(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        int pos = 0;

        for (int i = 0; i < source.Length; i++)
        {
            byte b = source[i];

            if (b == (byte)'"')
            {
                destination[pos++] = (byte)'\\';
                destination[pos++] = (byte)'"';
            }
            else if (b == (byte)'\\')
            {
                destination[pos++] = (byte)'\\';
                destination[pos++] = (byte)'\\';
            }
            else if (b < 0x20)
            {
                switch (b)
                {
                    case (byte)'\b':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'b';
                        break;
                    case (byte)'\f':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'f';
                        break;
                    case (byte)'\n':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'n';
                        break;
                    case (byte)'\r':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'r';
                        break;
                    case (byte)'\t':
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'t';
                        break;
                    default:
                        destination[pos++] = (byte)'\\';
                        destination[pos++] = (byte)'u';
                        destination[pos++] = (byte)'0';
                        destination[pos++] = (byte)'0';
                        destination[pos++] = HexDigit(b >> 4);
                        destination[pos++] = HexDigit(b & 0xF);
                        break;
                }
            }
            else
            {
                // Bytes >= 0x20 pass through (ASCII and multi-byte UTF-8 continuations)
                destination[pos++] = b;
            }
        }

        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HexDigit(int value)
    {
        return (byte)(value < 10 ? '0' + value : 'a' + value - 10);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowBuffer(ref byte[] buffer, int pos, int needed)
    {
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(buffer.Length * 2, pos + needed));
        buffer.AsSpan(0, pos).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static JsonElement NumberFromDoubleSlow(double value, JsonWorkspace workspace)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
#if NET
            if (!Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
            {
                ThrowFormatFailed();
                bytesWritten = 0; // unreachable
            }
#else
            int bytesWritten = FormatDoubleRoundtrip(value, buffer);
#endif

            FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(
                new ReadOnlySpan<byte>(buffer, 0, bytesWritten));
            workspace.RegisterDocument(doc);
            return doc.RootElement;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

#if !NET
    /// <summary>
    /// Formats a double to its shortest roundtrip UTF-8 representation on
    /// .NET Framework / netstandard2.0 where <see cref="Utf8Formatter"/>
    /// uses G17 (which is not shortest-roundtrip).
    /// </summary>
    private static int FormatDoubleRoundtrip(double value, Span<byte> destination)
    {
        string text = value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        // All digits, '.', '-', 'E', '+' are ASCII — direct byte cast
        if (text.Length > destination.Length)
        {
            ThrowFormatFailed();
        }

        for (int i = 0; i < text.Length; i++)
        {
            destination[i] = (byte)text[i];
        }

        return text.Length;
    }
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFormatFailed()
    {
        throw new InvalidOperationException(SR.FailedToFormatNumberAsUtf8);
    }
}