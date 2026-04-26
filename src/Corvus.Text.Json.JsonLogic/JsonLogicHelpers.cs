// <copyright file="JsonLogicHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Corvus.Numerics;
using Corvus.Runtime.InteropServices;
using Corvus.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Helper methods for JsonLogic truthy/falsy semantics and type coercion.
/// </summary>
public static class JsonLogicHelpers
{
    private static readonly JsonElement TrueElement = ParsedJsonDocument<JsonElement>.True;
    private static readonly JsonElement FalseElement = ParsedJsonDocument<JsonElement>.False;
    private static readonly JsonElement NullElementValue = ParsedJsonDocument<JsonElement>.Null;
    private static readonly JsonElement ZeroElement = ParsedJsonDocument<JsonElement>.NumberConstant("0"u8.ToArray());
    private static readonly JsonElement OneElement = ParsedJsonDocument<JsonElement>.NumberConstant("1"u8.ToArray());
    private static readonly JsonElement EmptyStringElement = ParsedJsonDocument<JsonElement>.StringConstant("\"\""u8.ToArray());
    private static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);

    /// <summary>
    /// Determines whether a <see cref="JsonElement"/> is truthy per JsonLogic semantics.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> if the value is truthy; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// JsonLogic truthy/falsy rules:
    /// Falsy: 0, null, "", [] (empty array), false, Undefined/Null.
    /// Truthy: "0", non-zero numbers, non-empty strings, non-empty arrays, true.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTruthy(in JsonElement value)
    {
        if (value.IsNullOrUndefined())
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            JsonValueKind.Number => !IsZero(value),
            JsonValueKind.String => !IsEmptyString(value),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => true,
            _ => false,
        };
    }

    /// <summary>
    /// Creates a JsonElement representing a boolean value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement BooleanElement(bool value)
    {
        return value ? TrueElement : FalseElement;
    }

    /// <summary>
    /// Gets a JsonElement representing null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NullElement()
    {
        return NullElementValue;
    }

    /// <summary>
    /// Gets a JsonElement representing zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement Zero()
    {
        return ZeroElement;
    }

    /// <summary>
    /// Gets a JsonElement representing one.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement One()
    {
        return OneElement;
    }

    /// <summary>
    /// Gets a JsonElement representing an empty string.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement EmptyString()
    {
        return EmptyStringElement;
    }

    /// <summary>
    /// Gets a JsonElement representing an empty array.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement EmptyArray()
    {
        return EmptyArrayElement;
    }

    /// <summary>
    /// Attempts to coerce a value to a number for arithmetic/comparison.
    /// Strings that are valid JSON numbers are converted; booleans become 0/1;
    /// null becomes 0. Returns false if coercion is not possible.
    /// </summary>
    /// <remarks>
    /// This never converts through <see cref="double"/> or any other .NET numeric type.
    /// String coercion validates the string as a JSON number representation using
    /// <see cref="JsonElementHelpers.TryParseNumber"/> and parses directly to a
    /// <see cref="JsonElement"/> from the UTF-8 bytes.
    /// </remarks>
    public static bool TryCoerceToNumber(in JsonElement value, out JsonElement result)
    {
        return TryCoerceToNumber(value, null, out result);
    }

    /// <summary>
    /// Coerces a <see cref="JsonElement"/> to a number, registering any created documents in the workspace.
    /// </summary>
    public static bool TryCoerceToNumber(in JsonElement value, JsonWorkspace? workspace, out JsonElement result)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            result = value;
            return true;
        }

        if (value.IsNullOrUndefined())
        {
            result = ZeroElement;
            return true;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                result = OneElement;
                return true;
            case JsonValueKind.False:
                result = ZeroElement;
                return true;
            case JsonValueKind.String:
                return TryCoerceStringToNumber(value, workspace, out result);
            default:
                result = default;
                return false;
        }
    }

    /// <summary>
    /// Compares two JsonElement numbers using UTF-8 normalized number comparison.
    /// </summary>
    /// <returns>-1 if left &lt; right, 0 if equal, 1 if left &gt; right.</returns>
    public static int CompareNumbers(in JsonElement left, in JsonElement right)
    {
        using RawUtf8JsonString leftRaw = JsonMarshal.GetRawUtf8Value(left);
        using RawUtf8JsonString rightRaw = JsonMarshal.GetRawUtf8Value(right);

        JsonElementHelpers.ParseNumber(leftRaw.Span, out bool lNeg, out ReadOnlySpan<byte> lInt, out ReadOnlySpan<byte> lFrac, out int lExp);
        JsonElementHelpers.ParseNumber(rightRaw.Span, out bool rNeg, out ReadOnlySpan<byte> rInt, out ReadOnlySpan<byte> rFrac, out int rExp);

        return JsonElementHelpers.CompareNormalizedJsonNumbers(
            lNeg, lInt, lFrac, lExp,
            rNeg, rInt, rFrac, rExp);
    }

    /// <summary>
    /// Determines whether two JsonElement numbers are equal using UTF-8 comparison.
    /// </summary>
    public static bool AreNumbersEqual(in JsonElement left, in JsonElement right)
    {
        using RawUtf8JsonString leftRaw = JsonMarshal.GetRawUtf8Value(left);
        using RawUtf8JsonString rightRaw = JsonMarshal.GetRawUtf8Value(right);

        return JsonElementHelpers.AreEqualJsonNumbers(leftRaw.Span, rightRaw.Span);
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> wrapping a number from raw UTF-8 bytes.
    /// Uses the pooled <see cref="FixedJsonValueDocument{T}"/>.
    /// </summary>
    /// <param name="utf8NumberBytes">The raw UTF-8 number bytes (must be heap-allocated or pinned).</param>
    /// <returns>A <see cref="JsonElement"/> backed by a pooled document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromUtf8(ReadOnlyMemory<byte> utf8NumberBytes)
    {
        return FixedJsonValueDocument<JsonElement>.ForNumber(utf8NumberBytes).RootElement;
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> wrapping a number from a UTF-8 span,
    /// copying into a rented buffer (zero heap allocation on the hot path).
    /// </summary>
    /// <param name="utf8NumberBytes">The raw UTF-8 number bytes.</param>
    /// <returns>A <see cref="JsonElement"/> backed by a pooled document with a rented buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromSpan(ReadOnlySpan<byte> utf8NumberBytes)
    {
        return FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(utf8NumberBytes).RootElement;
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> wrapping a number from a UTF-8 span and registers
    /// the backing document in the workspace for proper lifecycle management.
    /// </summary>
    /// <param name="utf8NumberBytes">The raw UTF-8 number bytes.</param>
    /// <param name="workspace">The workspace to register the document in.</param>
    /// <returns>A <see cref="JsonElement"/> backed by a pooled document with a rented buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NumberFromSpan(ReadOnlySpan<byte> utf8NumberBytes, JsonWorkspace workspace)
    {
        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForNumberFromSpan(utf8NumberBytes);
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> wrapping a quoted string from raw UTF-8 bytes.
    /// Uses the pooled <see cref="FixedJsonValueDocument{T}"/>.
    /// </summary>
    /// <param name="quotedUtf8StringBytes">The raw UTF-8 string bytes including surrounding quotes.</param>
    /// <returns>A <see cref="JsonElement"/> backed by a pooled document.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement StringFromQuotedUtf8(ReadOnlyMemory<byte> quotedUtf8StringBytes)
    {
        return FixedJsonValueDocument<JsonElement>.ForString(quotedUtf8StringBytes).RootElement;
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> wrapping a quoted string from a UTF-8 span,
    /// copying the data into a pooled document's rented buffer.
    /// </summary>
    /// <param name="quotedUtf8StringBytes">The raw UTF-8 string bytes including surrounding quotes.</param>
    /// <returns>A <see cref="JsonElement"/> backed by a pooled document with a rented buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement StringFromQuotedUtf8Span(ReadOnlySpan<byte> quotedUtf8StringBytes)
    {
        return FixedJsonValueDocument<JsonElement>.ForStringFromSpan(quotedUtf8StringBytes).RootElement;
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> wrapping a quoted string from a UTF-8 span and registers
    /// the backing document in the workspace for proper lifecycle management.
    /// </summary>
    /// <param name="quotedUtf8StringBytes">The raw UTF-8 string bytes including surrounding quotes.</param>
    /// <param name="workspace">The workspace to register the document in.</param>
    /// <returns>A <see cref="JsonElement"/> backed by a pooled document with a rented buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement StringFromQuotedUtf8Span(ReadOnlySpan<byte> quotedUtf8StringBytes, JsonWorkspace workspace)
    {
        FixedJsonValueDocument<JsonElement> doc = FixedJsonValueDocument<JsonElement>.ForStringFromSpan(quotedUtf8StringBytes);
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Appends the UTF-8 string coercion of a <see cref="JsonElement"/> directly to a
    /// <see cref="Utf8ValueStringBuilder"/>, with zero string allocation.
    /// For strings, copies raw UTF-8 content (excluding quotes). For numbers, copies
    /// raw UTF-8 digits. For booleans/null, appends the literal text.
    /// </summary>
    internal static void AppendCoercedUtf8(ref Utf8ValueStringBuilder builder, in JsonElement value)
    {
        if (value.IsNullOrUndefined())
        {
            builder.Append("null"u8);
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
                ReadOnlySpan<byte> span = raw.Span;

                // Raw includes quotes; slice them off to get unescaped content
                if (span.Length > 2)
                {
                    builder.Append(span.Slice(1, span.Length - 2));
                }

                break;
            }

            case JsonValueKind.Number:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
                builder.Append(raw.Span);
                break;
            }

            case JsonValueKind.True:
                builder.Append("true"u8);
                break;
            case JsonValueKind.False:
                builder.Append("false"u8);
                break;
            case JsonValueKind.Null:
                builder.Append("null"u8);
                break;
        }
    }

    /// <summary>
    /// Coerces a <see cref="JsonElement"/> to its string representation.
    /// </summary>
    public static string CoerceToString(in JsonElement value)
    {
        if (value.IsNullOrUndefined())
        {
            return "null";
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => GetNumberString(value),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Creates a <see cref="JsonElement"/> from a string value using the pooled document.
    /// </summary>
    public static JsonElement StringToElement(string value)
    {
        // Encode the string as quoted UTF-8: "value"
        // Use stackalloc for small strings, ArrayPool for large ones.
        int maxUtf8Len = System.Text.Encoding.UTF8.GetMaxByteCount(value.Length) + 2;
        byte[]? rented = null;

        Span<byte> buffer = maxUtf8Len <= 256
            ? stackalloc byte[256]
            : (rented = ArrayPool<byte>.Shared.Rent(maxUtf8Len));

        try
        {
            buffer[0] = (byte)'"';
#if NET
            int written = System.Text.Encoding.UTF8.GetBytes(value, buffer.Slice(1));
#else
            int written;
            unsafe
            {
                fixed (char* pChars = value)
                fixed (byte* pBytes = buffer.Slice(1))
                {
                    written = System.Text.Encoding.UTF8.GetBytes(pChars, value.Length, pBytes, buffer.Length - 2);
                }
            }
#endif
            buffer[1 + written] = (byte)'"';
            return StringFromQuotedUtf8Span(buffer.Slice(0, written + 2));
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Extracts a substring from a <see cref="JsonElement"/> string value using JsonLogic
    /// substr semantics. Uses a zero-allocation UTF-8 fast path for ASCII strings.
    /// </summary>
    /// <param name="source">The source element (coerced to string if not already).</param>
    /// <param name="start">The start index (may be negative to index from end).</param>
    /// <param name="length">The length to extract (may be negative to trim from end). Use <see cref="int.MaxValue"/> for "to end".</param>
    /// <param name="workspace">The workspace for creating the result element.</param>
    /// <returns>A <see cref="JsonElement"/> containing the extracted substring.</returns>
    public static JsonElement SubstrFromElement(in JsonElement source, int start, int length, JsonWorkspace workspace)
    {
        if (source.ValueKind == JsonValueKind.String)
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(source);
            ReadOnlySpan<byte> span = raw.Span;

            if (span.Length >= 2)
            {
                ReadOnlySpan<byte> content = span.Slice(1, span.Length - 2);

                // ASCII fast path: char positions == byte positions
                bool isSimpleAscii = true;
                for (int i = 0; i < content.Length; i++)
                {
                    if (content[i] >= 0x80 || content[i] == (byte)'\\')
                    {
                        isSimpleAscii = false;
                        break;
                    }
                }

                if (isSimpleAscii)
                {
                    return SubstrFromAsciiUtf8(content, start, length, workspace);
                }
            }
        }

        // Slow path: coerce to string, then substring
        string? str = CoerceToString(source);
        if (str is null || str.Length == 0)
        {
            return EmptyString();
        }

        return SubstrFromManagedString(str, start, length, workspace);
    }

    private static JsonElement SubstrFromAsciiUtf8(ReadOnlySpan<byte> content, int start, int length, JsonWorkspace workspace)
    {
        int len = content.Length;

        if (start < 0)
        {
            start = Math.Max(0, len + start);
        }

        if (start >= len)
        {
            return EmptyString();
        }

        int actualLength = length == int.MaxValue
            ? len - start
            : (length < 0 ? Math.Max(0, len - start + length) : length);

        actualLength = Math.Min(actualLength, len - start);
        if (actualLength <= 0)
        {
            return EmptyString();
        }

        int resultLen = actualLength + 2;
        Span<byte> result = resultLen <= 256
            ? stackalloc byte[256]
            : new byte[resultLen];

        result[0] = (byte)'"';
        content.Slice(start, actualLength).CopyTo(result.Slice(1));
        result[actualLength + 1] = (byte)'"';

        return StringFromQuotedUtf8Span(result.Slice(0, resultLen), workspace);
    }

    private static JsonElement SubstrFromManagedString(string str, int start, int length, JsonWorkspace workspace)
    {
        if (start < 0)
        {
            start = Math.Max(0, str.Length + start);
        }

        if (start >= str.Length)
        {
            return EmptyString();
        }

        int actualLength = length == int.MaxValue
            ? str.Length - start
            : (length < 0 ? Math.Max(0, str.Length - start + length) : length);

        actualLength = Math.Min(actualLength, str.Length - start);
        if (actualLength <= 0)
        {
            return EmptyString();
        }

        return StringToElement(str.Substring(start, actualLength));
    }

    /// <summary>
    /// Concatenates multiple <see cref="JsonElement"/> values into a single JSON string element,
    /// coercing each to its string representation using UTF-8 buffer building (zero managed string allocation).
    /// </summary>
    /// <param name="operands">The operands to concatenate.</param>
    /// <param name="workspace">The workspace for creating the result element.</param>
    /// <returns>A <see cref="JsonElement"/> containing the concatenated string.</returns>
    public static JsonElement ConcatToElement(ReadOnlySpan<JsonElement> operands, JsonWorkspace workspace)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
        int pos = 0;
        buffer[pos++] = (byte)'"';

        for (int i = 0; i < operands.Length; i++)
        {
            AppendCoercedToBuffer(operands[i], ref buffer, ref pos);
        }

        GrowCatBufferIfNeeded(ref buffer, pos, 1);
        buffer[pos++] = (byte)'"';

        JsonElement result = StringFromQuotedUtf8Span(
            new ReadOnlySpan<byte>(buffer, 0, pos), workspace);
        ArrayPool<byte>.Shared.Return(buffer);
        return result;
    }

    /// <summary>
    /// Appends the coerced string representation of a JSON element to a buffer.
    /// </summary>
    public static void AppendCoercedToBuffer(in JsonElement elem, ref byte[] buffer, ref int pos)
    {
        if (elem.IsNullOrUndefined())
        {
            GrowCatBufferIfNeeded(ref buffer, pos, 4);
            "null"u8.CopyTo(buffer.AsSpan(pos));
            pos += 4;
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
                    GrowCatBufferIfNeeded(ref buffer, pos, contentLen);
                    span.Slice(1, contentLen).CopyTo(buffer.AsSpan(pos));
                    pos += contentLen;
                }

                break;
            }

            case JsonValueKind.Number:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(elem);
                ReadOnlySpan<byte> span = raw.Span;
                GrowCatBufferIfNeeded(ref buffer, pos, span.Length);
                span.CopyTo(buffer.AsSpan(pos));
                pos += span.Length;
                break;
            }

            case JsonValueKind.True:
                GrowCatBufferIfNeeded(ref buffer, pos, 4);
                "true"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;
            case JsonValueKind.False:
                GrowCatBufferIfNeeded(ref buffer, pos, 5);
                "false"u8.CopyTo(buffer.AsSpan(pos));
                pos += 5;
                break;
            case JsonValueKind.Null:
                GrowCatBufferIfNeeded(ref buffer, pos, 4);
                "null"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;
        }
    }

    /// <summary>
    /// Grows the cat buffer if needed to accommodate the specified number of bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrowCatBufferIfNeeded(ref byte[] buffer, int pos, int needed)
    {
        if (pos + needed > buffer.Length)
        {
            GrowCatBuffer(ref buffer, pos, needed);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void GrowCatBuffer(ref byte[] buffer, int pos, int needed)
    {
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(buffer.Length * 2, pos + needed));
        buffer.AsSpan(0, pos).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    /// <summary>
    /// Determines whether a numeric <see cref="JsonElement"/> is zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsZero(in JsonElement value)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
        return JsonElementHelpers.AreEqualJsonNumbers(raw.Span, "0"u8);
    }

    /// <summary>
    /// Determines whether a string <see cref="JsonElement"/> is the empty string.
    /// Checks the raw UTF-8 length (including quotes) — empty string is exactly <c>""</c> (2 bytes).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmptyString(in JsonElement value)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);

        // Raw value includes quotes: "" is 2 bytes
        return raw.Span.Length <= 2;
    }

    /// <summary>
    /// Coerces a string JsonElement to a number without going through any .NET string type.
    /// Reads the raw UTF-8 content directly, validates it as a JSON number using
    /// <see cref="JsonElementHelpers.TryParseNumber"/>, and creates a <see cref="JsonElement"/>
    /// via the pooled document.
    /// </summary>
    private static bool TryCoerceStringToNumber(in JsonElement value, JsonWorkspace? workspace, out JsonElement result)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
        ReadOnlySpan<byte> quoted = raw.Span;

        // Raw string value includes quotes: "" is 2 bytes = empty string → 0
        if (quoted.Length <= 2)
        {
            result = ZeroElement;
            return true;
        }

        // Slice off quotes to get unescaped content
        ReadOnlySpan<byte> content = quoted.Slice(1, quoted.Length - 2);

        if (JsonElementHelpers.TryParseNumber(content, out _, out _, out _, out _))
        {
            result = workspace is not null ? NumberFromSpan(content, workspace) : NumberFromSpan(content);
            return true;
        }

        result = default;
        return false;
    }

    private static string GetNumberString(in JsonElement value)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
        ReadOnlySpan<byte> span = raw.Span;

        // JSON numbers are always ASCII, so byte-to-char is 1:1
        Span<char> chars = span.Length <= 128
            ? stackalloc char[128]
            : new char[span.Length];

        for (int i = 0; i < span.Length; i++)
        {
            chars[i] = (char)span[i];
        }

#if NET
        return new string(chars.Slice(0, span.Length));
#else
        return new string(chars.Slice(0, span.Length).ToArray());
#endif
    }

    // ─── Code-generation helpers ─────────────────────────────────
    // These methods are called by source-generated JsonLogic evaluators
    // (produced by JsonLogicCodeGenerator / JsonLogicSourceGenerator).
    // They were previously emitted into every generated class; they now
    // live here so the generated code is smaller and the JIT sees a
    // single copy.

    /// <summary>
    /// Strict equality: same <see cref="JsonValueKind"/> and equal value.
    /// </summary>
    public static bool StrictElementEquals(in JsonElement left, in JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.ValueKind == JsonValueKind.Number)
        {
            return AreNumbersEqual(left, right);
        }

        if (left.ValueKind == JsonValueKind.String)
        {
            using RawUtf8JsonString leftRaw = JsonMarshal.GetRawUtf8Value(left);
            using RawUtf8JsonString rightRaw = JsonMarshal.GetRawUtf8Value(right);
            return leftRaw.Span.SequenceEqual(rightRaw.Span);
        }

        return left.ValueKind is JsonValueKind.True or JsonValueKind.False;
    }

    /// <summary>
    /// Coercing (abstract) equality per JsonLogic semantics.
    /// </summary>
    public static bool CoercingElementEquals(in JsonElement left, in JsonElement right)
    {
        if (left.ValueKind == right.ValueKind)
        {
            return StrictElementEquals(left, right);
        }

        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        if (left.ValueKind == JsonValueKind.Number && right.ValueKind == JsonValueKind.String)
        {
            return TryCoerceToNumber(right, out JsonElement rn) && AreNumbersEqual(left, rn);
        }

        if (left.ValueKind == JsonValueKind.String && right.ValueKind == JsonValueKind.Number)
        {
            return TryCoerceToNumber(left, out JsonElement ln) && AreNumbersEqual(ln, right);
        }

        if (left.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            JsonElement leftNum = left.ValueKind == JsonValueKind.True ? One() : Zero();
            return CoercingElementEquals(leftNum, right);
        }

        if (right.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            JsonElement rightNum = right.ValueKind == JsonValueKind.True ? One() : Zero();
            return CoercingElementEquals(left, rightNum);
        }

        return false;
    }

    /// <summary>
    /// Compare two elements with double fast-path and BigNumber fallback.
    /// <paramref name="op"/>: 1 = &gt;, 2 = &gt;=, 3 = &lt;, 4 = &lt;=.
    /// </summary>
    public static bool CompareCoerced(in JsonElement left, in JsonElement right, int op)
    {
        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        if (TryCoerceToDouble(left, out double ld) && TryCoerceToDouble(right, out double rd))
        {
            return op switch { 1 => ld > rd, 2 => ld >= rd, 3 => ld < rd, 4 => ld <= rd, _ => false };
        }

        if (!TryCoerceToNumber(left, out JsonElement ln) || !TryCoerceToNumber(right, out JsonElement rn))
        {
            return false;
        }

        int cmp = CompareNumbers(ln, rn);
        return op switch
        {
            1 => cmp > 0,
            2 => cmp >= 0,
            3 => cmp < 0,
            4 => cmp <= 0,
            _ => false,
        };
    }

    /// <summary>
    /// Coerces a <see cref="JsonElement"/> to a <see cref="BigNumber"/>.
    /// </summary>
    public static BigNumber CoerceToBigNumber(in JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(element);
            if (BigNumber.TryParse(raw.Span, out BigNumber result))
            {
                return result;
            }
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            return BigNumber.One;
        }

        if (element.ValueKind == JsonValueKind.False || element.ValueKind == JsonValueKind.Null)
        {
            return BigNumber.Zero;
        }

        if (element.ValueKind == JsonValueKind.String && TryCoerceToNumber(element, out JsonElement numElem))
        {
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(numElem);
            if (BigNumber.TryParse(raw.Span, out BigNumber result))
            {
                return result;
            }
        }

        return BigNumber.Zero;
    }

    /// <summary>
    /// Materializes a <see cref="BigNumber"/> into a number <see cref="JsonElement"/>.
    /// </summary>
    public static JsonElement BigNumberToElement(BigNumber value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (value.TryFormat(buffer, out int bytesWritten))
        {
            return NumberFromSpan(buffer.Slice(0, bytesWritten), workspace);
        }

        return Zero();
    }

    /// <summary>
    /// Attempts to coerce a <see cref="JsonElement"/> to a <see langword="double"/>.
    /// Handles Number, True (1), False/Null (0), and numeric strings.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryCoerceToDouble(in JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.TryGetDouble(out value);
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            value = 1;
            return true;
        }

        if (element.ValueKind == JsonValueKind.False || element.ValueKind == JsonValueKind.Null)
        {
            value = 0;
            return true;
        }

        if (element.ValueKind == JsonValueKind.String && TryCoerceToNumber(element, out JsonElement numElem) && numElem.TryGetDouble(out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Materializes a <see langword="double"/> into a number <see cref="JsonElement"/>.
    /// </summary>
    public static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (System.Buffers.Text.Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return NumberFromSpan(buffer.Slice(0, bytesWritten), workspace);
        }

        return Zero();
    }

    /// <summary>
    /// Resolves a variable path from <paramref name="data"/> using <paramref name="pathElement"/>.
    /// Handles dotted paths (e.g. "a.b.c") and array indexing. Zero-allocation for common paths.
    /// </summary>
    public static JsonElement ResolveVar(in JsonElement data, in JsonElement pathElement)
    {
        if (pathElement.IsNullOrUndefined())
        {
            return data;
        }

        if (pathElement.ValueKind == JsonValueKind.Number)
        {
            // Number path — raw text is digits only, no quotes
            using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(pathElement);
            return ResolvePathSegment(data, raw.Span);
        }

        if (pathElement.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString utf8 = pathElement.GetUtf8String();
            ReadOnlySpan<byte> path = utf8.Span;
            if (path.Length == 0)
            {
                return data;
            }

            return ResolveVarPath(data, path);
        }

        return NullElement();
    }

    private static JsonElement ResolveVarPath(in JsonElement data, ReadOnlySpan<byte> path)
    {
        JsonElement current = data;

        while (true)
        {
            if (current.IsNullOrUndefined())
            {
                return NullElement();
            }

            int dotIndex = path.IndexOf((byte)'.');
            ReadOnlySpan<byte> segment = dotIndex >= 0 ? path.Slice(0, dotIndex) : path;

            current = ResolvePathSegment(current, segment);

            if (dotIndex < 0)
            {
                return current;
            }

            path = path.Slice(dotIndex + 1);
        }
    }

    private static JsonElement ResolvePathSegment(in JsonElement current, ReadOnlySpan<byte> segment)
    {
        if (current.ValueKind == JsonValueKind.Object)
        {
            return current.TryGetProperty(segment, out JsonElement value)
                ? value
                : NullElement();
        }

        if (current.ValueKind == JsonValueKind.Array
            && Utf8Parser.TryParse(segment, out int idx, out int consumed)
            && consumed == segment.Length
            && idx >= 0 && idx < current.GetArrayLength())
        {
            return current[idx];
        }

        return NullElement();
    }
}