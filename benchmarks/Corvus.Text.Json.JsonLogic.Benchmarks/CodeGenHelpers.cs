// <copyright file="CodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Benchmarks;

/// <summary>
/// Shared helper methods used by code-generated JsonLogic evaluator classes.
/// These mirror the internal helpers from the functional evaluator but use only
/// public APIs, making them suitable for generated code in external projects.
/// </summary>
internal static class CodeGenHelpers
{
    /// <summary>
    /// Resolves a simple (non-dotted) variable path against the data element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement VarSimple(in JsonElement data, ReadOnlySpan<byte> name)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out JsonElement result))
        {
            return result;
        }

        return JsonLogicHelpers.NullElement();
    }

    /// <summary>
    /// Resolves a two-segment dotted path (e.g., "pie.filling") against the data element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement VarDotted(in JsonElement data, ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(first, out JsonElement mid))
        {
            if (mid.ValueKind == JsonValueKind.Object && mid.TryGetProperty(second, out JsonElement result))
            {
                return result;
            }
        }

        return JsonLogicHelpers.NullElement();
    }

    /// <summary>
    /// Coerces a JsonElement to a native double following JsonLogic semantics.
    /// Returns <see cref="double.NaN"/> for non-coercible values.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CoerceToDouble(in JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            if (value.TryGetDouble(out double d))
            {
                return d;
            }

            return 0.0;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return 1.0;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return 0.0;
        }

        if (value.IsNullOrUndefined())
        {
            return 0.0;
        }

        if (value.ValueKind == JsonValueKind.String
            && JsonLogicHelpers.TryCoerceToNumber(value, out JsonElement numElem)
            && numElem.TryGetDouble(out double nd))
        {
            return nd;
        }

        return double.NaN;
    }

    /// <summary>
    /// Materializes a native double into a JsonElement backed by a workspace-registered document.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonElement MaterializeDouble(double value, JsonWorkspace workspace)
    {
        Span<byte> buf = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buf, out int written))
        {
            return JsonLogicHelpers.NumberFromSpan(buf.Slice(0, written), workspace);
        }

        return JsonLogicHelpers.Zero();
    }

    /// <summary>
    /// Grows the cat buffer if needed to accommodate <paramref name="needed"/> additional bytes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void GrowBuffer(ref byte[] buffer, int pos, int needed)
    {
        if (pos + needed > buffer.Length)
        {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
            buffer.AsSpan(0, pos).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = newBuffer;
        }
    }

    /// <summary>
    /// Appends the UTF-8 coercion of a JsonElement to a byte buffer for cat operations.
    /// Strings append their unquoted content; numbers append raw digits; booleans/null append literal text.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AppendCoercedToBuffer(in JsonElement value, ref byte[] buffer, ref int pos)
    {
        if (value.IsNullOrUndefined())
        {
            GrowBuffer(ref buffer, pos, 4);
            "null"u8.CopyTo(buffer.AsSpan(pos));
            pos += 4;
            return;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.String:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
                ReadOnlySpan<byte> span = raw.Span;
                if (span.Length > 2)
                {
                    int contentLen = span.Length - 2;
                    GrowBuffer(ref buffer, pos, contentLen);
                    span.Slice(1, contentLen).CopyTo(buffer.AsSpan(pos));
                    pos += contentLen;
                }

                break;
            }

            case JsonValueKind.Number:
            {
                using RawUtf8JsonString raw = JsonMarshal.GetRawUtf8Value(value);
                GrowBuffer(ref buffer, pos, raw.Span.Length);
                raw.Span.CopyTo(buffer.AsSpan(pos));
                pos += raw.Span.Length;
                break;
            }

            case JsonValueKind.True:
                GrowBuffer(ref buffer, pos, 4);
                "true"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;

            case JsonValueKind.False:
                GrowBuffer(ref buffer, pos, 5);
                "false"u8.CopyTo(buffer.AsSpan(pos));
                pos += 5;
                break;

            case JsonValueKind.Null:
                GrowBuffer(ref buffer, pos, 4);
                "null"u8.CopyTo(buffer.AsSpan(pos));
                pos += 4;
                break;
        }
    }

    /// <summary>
    /// Performs JsonLogic loose equality comparison between two elements.
    /// </summary>
    public static bool LooseElementEquals(in JsonElement left, in JsonElement right)
    {
        // Both null/undefined
        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        // Same kind
        if (left.ValueKind == right.ValueKind)
        {
            switch (left.ValueKind)
            {
                case JsonValueKind.Number:
                    return JsonLogicHelpers.AreNumbersEqual(left, right);
                case JsonValueKind.String:
                {
                    // Compare raw UTF-8 bytes first to avoid GetString() allocation.
                    using RawUtf8JsonString rawLeft = JsonMarshal.GetRawUtf8Value(left);
                    using RawUtf8JsonString rawRight = JsonMarshal.GetRawUtf8Value(right);
                    if (rawLeft.Span.SequenceEqual(rawRight.Span))
                    {
                        return true;
                    }

                    // Raw bytes may differ due to escape sequences; fall back to string comparison.
                    return string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal);
                }
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return true; // Same kind already checked
                default:
                    return false;
            }
        }

        // Cross-type coercion to number
        if (JsonLogicHelpers.TryCoerceToNumber(left, out JsonElement leftNum)
            && JsonLogicHelpers.TryCoerceToNumber(right, out JsonElement rightNum))
        {
            return JsonLogicHelpers.AreNumbersEqual(leftNum, rightNum);
        }

        return false;
    }
}
