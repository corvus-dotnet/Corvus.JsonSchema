// <copyright file="JsonLogicHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using Corvus.Runtime.InteropServices;
using Corvus.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Helper methods for JsonLogic truthy/falsy semantics and type coercion.
/// </summary>
internal static class JsonLogicHelpers
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
                return TryCoerceStringToNumber(value, out result);
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
    /// Appends the UTF-8 string coercion of a <see cref="JsonElement"/> directly to a
    /// <see cref="Utf8ValueStringBuilder"/>, with zero string allocation.
    /// For strings, copies raw UTF-8 content (excluding quotes). For numbers, copies
    /// raw UTF-8 digits. For booleans/null, appends the literal text.
    /// </summary>
    public static void AppendCoercedUtf8(ref Utf8ValueStringBuilder builder, in JsonElement value)
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
        // Build quoted UTF-8: "value"
        byte[] utf8Value = System.Text.Encoding.UTF8.GetBytes(value);
        byte[] quoted = new byte[utf8Value.Length + 2];
        quoted[0] = (byte)'"';
        Buffer.BlockCopy(utf8Value, 0, quoted, 1, utf8Value.Length);
        quoted[quoted.Length - 1] = (byte)'"';
        return StringFromQuotedUtf8(quoted);
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
    /// Coerces a string JsonElement to a number without going through any .NET numeric type.
    /// Validates the string content as a JSON number using <see cref="JsonElementHelpers.TryParseNumber"/>
    /// and creates a <see cref="JsonElement"/> via the pooled document.
    /// </summary>
    private static bool TryCoerceStringToNumber(in JsonElement value, out JsonElement result)
    {
        string? s = value.GetString();
        if (s is null || s.Length == 0)
        {
            result = ZeroElement;
            return true;
        }

        // JSON numbers are always ASCII (digits, '.', 'e', 'E', '+', '-'),
        // so we can do a simple 1:1 char-to-byte copy. Any non-ASCII character
        // means the string is not a valid JSON number.
        byte[]? rentedArray = null;
        Span<byte> utf8Buffer = s.Length <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(s.Length));

        try
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c > 127)
                {
                    result = default;
                    return false;
                }

                utf8Buffer[i] = (byte)c;
            }

            ReadOnlySpan<byte> utf8 = utf8Buffer.Slice(0, s.Length);

            if (JsonElementHelpers.TryParseNumber(utf8, out _, out _, out _, out _))
            {
                // Copy to a stable array for the pooled document
                byte[] stable = utf8.ToArray();
                result = NumberFromUtf8(stable);
                return true;
            }

            result = default;
            return false;
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    private static string GetNumberString(in JsonElement value)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
        ReadOnlySpan<byte> span = raw.Span;
        char[] chars = new char[span.Length];
        for (int i = 0; i < span.Length; i++)
        {
            chars[i] = (char)span[i];
        }

        return new string(chars);
    }
}