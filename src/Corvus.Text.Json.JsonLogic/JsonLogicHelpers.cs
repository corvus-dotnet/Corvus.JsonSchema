// <copyright file="JsonLogicHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JsonLogic;

/// <summary>
/// Helper methods for JsonLogic truthy/falsy semantics and type coercion.
/// </summary>
internal static class JsonLogicHelpers
{
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
            JsonValueKind.String => value.GetString()!.Length > 0,
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
        return value
            ? JsonElement.ParseValue("true"u8)
            : JsonElement.ParseValue("false"u8);
    }

    /// <summary>
    /// Gets a JsonElement representing null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement NullElement()
    {
        return JsonElement.ParseValue("null"u8);
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
            result = JsonElement.ParseValue("0"u8);
            return true;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                result = JsonElement.ParseValue("1"u8);
                return true;
            case JsonValueKind.False:
                result = JsonElement.ParseValue("0"u8);
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
    /// Determines whether a numeric <see cref="JsonElement"/> is zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsZero(in JsonElement value)
    {
        using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
        return JsonElementHelpers.AreEqualJsonNumbers(raw.Span, "0"u8);
    }

    /// <summary>
    /// Coerces a string JsonElement to a number without going through any .NET numeric type.
    /// Validates the string content as a JSON number using <see cref="JsonElementHelpers.TryParseNumber"/>
    /// and parses it directly as a <see cref="JsonElement"/>.
    /// </summary>
    private static bool TryCoerceStringToNumber(in JsonElement value, out JsonElement result)
    {
        string? s = value.GetString();
        if (s is null || s.Length == 0)
        {
            result = JsonElement.ParseValue("0"u8);
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
                result = JsonElement.ParseValue(utf8);
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
    /// Creates a <see cref="JsonElement"/> from a string value.
    /// </summary>
    public static JsonElement StringToElement(string value)
    {
        using MemoryStream ms = new();
        using Utf8JsonWriter writer = new(ms);
        writer.WriteStringValue(value);
        writer.Flush();
        return JsonElement.ParseValue(ms.ToArray());
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