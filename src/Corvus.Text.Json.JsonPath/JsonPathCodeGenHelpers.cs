// <copyright file="JsonPathCodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Public helper methods used by JSONPath source-generated evaluation code.
/// </summary>
/// <remarks>
/// <para>
/// These methods are called from code emitted by <c>JsonPathCodeGenerator</c>
/// and <c>JsonPathSourceGenerator</c>. They provide the same semantics as the
/// runtime <see cref="Compiler"/> — both share the same implementations to
/// guarantee identical behaviour.
/// </para>
/// <para>
/// This class is not intended for direct use by application code.
/// </para>
/// </remarks>
public static class JsonPathCodeGenHelpers
{
    /// <summary>Gets a pre-parsed empty JSON array element.</summary>
    public static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);

    /// <summary>Gets a pre-parsed <c>null</c> element.</summary>
    public static readonly JsonElement NullElement = JsonElement.ParseValue("null"u8);

    /// <summary>Gets a pre-parsed <c>true</c> element.</summary>
    public static readonly JsonElement TrueElement = JsonElement.ParseValue("true"u8);

    /// <summary>Gets a pre-parsed <c>false</c> element.</summary>
    public static readonly JsonElement FalseElement = JsonElement.ParseValue("false"u8);

    /// <summary>
    /// Builds a JSON array containing a single element.
    /// </summary>
    /// <param name="value">The element to wrap in an array.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <returns>A JSON array element containing <paramref name="value"/>.</returns>
    public static JsonElement SingletonArray(in JsonElement value, JsonWorkspace workspace)
    {
        JsonPathSequenceBuilder builder = default;
        try
        {
            builder.Add(value);
            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    /// <summary>
    /// Builds a JSON array from a read-only span of elements.
    /// </summary>
    /// <param name="nodes">The source span of elements.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <returns>A JSON array element.</returns>
    public static JsonElement BuildArrayFromSpan(ReadOnlySpan<JsonElement> nodes, JsonWorkspace workspace)
    {
        if (nodes.Length == 0)
        {
            return EmptyArrayElement;
        }

        JsonPathSequenceBuilder builder = default;
        try
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                builder.Add(nodes[i]);
            }

            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    /// <summary>
    /// Normalizes a JSONPath array index, converting negative indices to positive
    /// offsets from the end.
    /// </summary>
    /// <param name="index">The index to normalize (may be negative).</param>
    /// <param name="length">The length of the array.</param>
    /// <returns>The normalized index.</returns>
    public static int NormalizeIndex(long index, int length)
    {
        return index < 0 ? (int)(index + length) : (int)index;
    }

    /// <summary>
    /// Normalizes slice parameters per RFC 9535 Section 2.3.5.
    /// </summary>
    /// <param name="startOpt">The optional start value (<see langword="null"/> if omitted).</param>
    /// <param name="endOpt">The optional end value (<see langword="null"/> if omitted).</param>
    /// <param name="stepOpt">The optional step value (<see langword="null"/> if omitted; defaults to 1).</param>
    /// <param name="length">The length of the array.</param>
    /// <returns>A tuple of (start, end, step) clamped and ready for use in a for-loop.</returns>
    public static (int Start, int End, int Step) NormalizeSlice(
        long? startOpt,
        long? endOpt,
        long? stepOpt,
        int length)
    {
        long s = stepOpt ?? 1;
        if (s == 0)
        {
            return (0, 0, 0);
        }

        long start;
        long end;

        if (s > 0)
        {
            start = NormalizeSliceIndexInternal(startOpt ?? 0, length);
            end = NormalizeSliceIndexInternal(endOpt ?? length, length);
            start = Math.Max(Math.Min(start, length), 0);
            end = Math.Max(Math.Min(end, length), 0);
        }
        else
        {
            long upper = NormalizeSliceIndexInternal(startOpt ?? (length - 1), length);
            long lower = NormalizeSliceIndexInternal(endOpt ?? (-length - 1), length);
            upper = Math.Max(Math.Min(upper, length - 1), -1);
            lower = Math.Max(Math.Min(lower, length - 1), -1);
            start = upper;
            end = lower;
        }

        return ((int)start, (int)end, (int)s);
    }

    /// <summary>
    /// Performs a recursive deep structural equality comparison of two JSON elements.
    /// </summary>
    /// <param name="left">The left element.</param>
    /// <param name="right">The right element.</param>
    /// <returns>
    /// <see langword="true"/> if the elements are structurally equal;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public static bool DeepEquals(in JsonElement left, in JsonElement right)
    {
        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.True:
            case JsonValueKind.False:
                return true;

            case JsonValueKind.Number:
                return left.GetDouble() == right.GetDouble();

            case JsonValueKind.String:
                return left.Equals(right);

            case JsonValueKind.Array:
                if (left.GetArrayLength() != right.GetArrayLength())
                {
                    return false;
                }

                using (var leftEnum = left.EnumerateArray())
                using (var rightEnum = right.EnumerateArray())
                {
                    while (leftEnum.MoveNext() && rightEnum.MoveNext())
                    {
                        if (!DeepEquals(leftEnum.Current, rightEnum.Current))
                        {
                            return false;
                        }
                    }
                }

                return true;

            case JsonValueKind.Object:
                if (left.GetPropertyCount() != right.GetPropertyCount())
                {
                    return false;
                }

                foreach (JsonProperty<JsonElement> prop in left.EnumerateObject())
                {
                    using UnescapedUtf8JsonString name = prop.Utf8NameSpan;
                    if (!right.TryGetProperty(name.Span, out JsonElement rightVal) ||
                        !DeepEquals(prop.Value, rightVal))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Compares two JSON element values per RFC 9535 comparison semantics.
    /// </summary>
    /// <param name="left">
    /// The left value. A value with <see cref="JsonValueKind.Undefined"/> represents "Nothing".
    /// </param>
    /// <param name="right">
    /// The right value. A value with <see cref="JsonValueKind.Undefined"/> represents "Nothing".
    /// </param>
    /// <param name="op">
    /// The comparison operator: 0 = equal, 1 = not-equal, 2 = less-than,
    /// 3 = less-than-or-equal, 4 = greater-than, 5 = greater-than-or-equal.
    /// </param>
    /// <returns><see langword="true"/> if the comparison holds; otherwise <see langword="false"/>.</returns>
    public static bool CompareValues(in JsonElement left, in JsonElement right, int op)
    {
        bool leftNothing = left.ValueKind == JsonValueKind.Undefined;
        bool rightNothing = right.ValueKind == JsonValueKind.Undefined;

        if (leftNothing || rightNothing)
        {
            bool bothNothing = leftNothing && rightNothing;
            return op switch
            {
                0 => bothNothing,
                1 => !bothNothing,
                _ => false,
            };
        }

        JsonValueKind lk = left.ValueKind;
        JsonValueKind rk = right.ValueKind;

        if (op == 0 || op == 1)
        {
            bool eq = DeepEquals(left, right);
            return op == 0 ? eq : !eq;
        }

        // For ordering comparisons, types must match
        if (lk != rk)
        {
            return false;
        }

        if (lk == JsonValueKind.Number)
        {
            double lv = left.GetDouble();
            double rv = right.GetDouble();
            return op switch
            {
                2 => lv < rv,
                3 => lv <= rv,
                4 => lv > rv,
                5 => lv >= rv,
                _ => false,
            };
        }

        if (lk == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString leftUtf8 = left.GetUtf8String();
            using UnescapedUtf8JsonString rightUtf8 = right.GetUtf8String();
            int cmp = leftUtf8.Span.SequenceCompareTo(rightUtf8.Span);
            return op switch
            {
                2 => cmp < 0,
                3 => cmp <= 0,
                4 => cmp > 0,
                5 => cmp >= 0,
                _ => false,
            };
        }

        // RFC 9535: for non-orderable types, <= and >= reduce to ==
        if (op == 3 || op == 5)
        {
            return DeepEquals(left, right);
        }

        return false;
    }

    /// <summary>
    /// Returns the length of a JSON value per RFC 9535 <c>length()</c> function semantics.
    /// </summary>
    /// <param name="val">The value to measure.</param>
    /// <returns>
    /// The length: string character count, array element count, or object property count.
    /// Returns -1 for types that have no length.
    /// </returns>
    public static int LengthValue(in JsonElement val)
    {
        if (val.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString utf8 = val.GetUtf8String();
            return JsonElementHelpers.GetUtf8StringLength(utf8.Span);
        }

        return val.ValueKind switch
        {
            JsonValueKind.Array => val.GetArrayLength(),
            JsonValueKind.Object => val.GetPropertyCount(),
            _ => -1,
        };
    }

    /// <summary>
    /// Returns the count of elements in a node list (JSON array).
    /// </summary>
    /// <param name="nodeList">The node list (a JSON array).</param>
    /// <returns>The number of elements in the array.</returns>
    public static int CountNodes(in JsonElement nodeList)
    {
        return nodeList.IsNullOrUndefined() ? 0 : nodeList.GetArrayLength();
    }

    /// <summary>
    /// Returns the single value from a node list, or <c>default</c> (Undefined) if
    /// the list does not contain exactly one element.
    /// </summary>
    /// <param name="nodeList">The node list (a JSON array).</param>
    /// <returns>The single element, or <c>default</c>.</returns>
    public static JsonElement ValueOfNodes(in JsonElement nodeList)
    {
        if (!nodeList.IsNullOrUndefined() && nodeList.GetArrayLength() == 1)
        {
            foreach (JsonElement item in nodeList.EnumerateArray())
            {
                return item;
            }
        }

        return default;
    }

    private static readonly JsonElement[] IntCache = BuildIntCache();

    private static JsonElement[] BuildIntCache()
    {
        JsonElement[] cache = new JsonElement[256];
        Span<byte> buf = stackalloc byte[4];
        for (int i = 0; i < cache.Length; i++)
        {
            Utf8Formatter.TryFormat(i, buf, out int written);
            cache[i] = JsonElement.ParseValue(buf.Slice(0, written));
        }

        return cache;
    }

    /// <summary>
    /// Converts an integer to a <see cref="JsonElement"/> number.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A JSON number element.</returns>
    public static JsonElement IntToElement(int value)
    {
        if ((uint)value < (uint)IntCache.Length)
        {
            return IntCache[value];
        }

        Span<byte> buf = stackalloc byte[16];
        Utf8Formatter.TryFormat(value, buf, out int written);
        return JsonElement.ParseValue(buf.Slice(0, written));
    }

    /// <summary>
    /// Translates an I-Regexp (RFC 9485) pattern to a .NET regular expression pattern.
    /// </summary>
    /// <param name="pattern">The I-Regexp pattern.</param>
    /// <returns>The translated .NET regex pattern.</returns>
    public static string TranslateIRegexp(string pattern)
    {
        const string iRegexpDot = @"(?:[^\r\n\uD800-\uDFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";

        StringBuilder sb = new(pattern.Length * 2);
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                sb.Append(c);
                sb.Append(pattern[i + 1]);
                i++;
            }
            else if (c == '[')
            {
                sb.Append(c);
                i++;
                if (i < pattern.Length && pattern[i] == '^')
                {
                    sb.Append(pattern[i]);
                    i++;
                }

                if (i < pattern.Length && pattern[i] == ']')
                {
                    sb.Append(pattern[i]);
                    i++;
                }

                while (i < pattern.Length && pattern[i] != ']')
                {
                    if (pattern[i] == '\\' && i + 1 < pattern.Length)
                    {
                        sb.Append(pattern[i]);
                        sb.Append(pattern[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(pattern[i]);
                        i++;
                    }
                }

                if (i < pattern.Length)
                {
                    sb.Append(pattern[i]);
                }
            }
            else if (c == '.')
            {
                sb.Append(iRegexpDot);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Recursively visits all descendants of <paramref name="node"/>, appending
    /// matching named property values to <paramref name="result"/>. Specialized
    /// for descendant-name patterns (e.g. <c>$..author</c>). Only recurses
    /// into container children (objects and arrays), skipping primitives.
    /// </summary>
    /// <param name="node">The current node to visit.</param>
    /// <param name="propertyName">The UTF-8 property name to search for.</param>
    /// <param name="result">The result to append matching values to.</param>
    public static void DescendantsForName(
        in JsonElement node,
        ReadOnlySpan<byte> propertyName,
        ref JsonPathResult result)
    {
        foreach (JsonElement value in node.EnumerateDescendantProperties(propertyName))
        {
            result.Append(value);
        }
    }

    /// <summary>
    /// Recursively visits all descendants of <paramref name="node"/>, counting
    /// matching named property values and capturing the first match. Specialized
    /// for descendant-name patterns in filter sub-queries.
    /// </summary>
    /// <param name="node">The current node to visit.</param>
    /// <param name="propertyName">The UTF-8 property name to search for.</param>
    /// <param name="count">The running count of matched nodes.</param>
    /// <param name="first">
    /// Receives the first matched element (set when <paramref name="count"/> transitions from 0).
    /// </param>
    public static void DescendantsForNameCount(
        in JsonElement node,
        ReadOnlySpan<byte> propertyName,
        ref int count,
        ref JsonElement first)
    {
        foreach (JsonElement value in node.EnumerateDescendantProperties(propertyName))
        {
            if (count == 0)
            {
                first = value;
            }

            count++;
        }
    }

    private static long NormalizeSliceIndexInternal(long index, int len)
    {
        return index >= 0 ? index : len + index;
    }

    /// <summary>
    /// Doubles the capacity of a DFS stack, returning the old array to the pool.
    /// </summary>
    /// <param name="stack">The current stack array (replaced on return).</param>
    /// <param name="capacity">The current capacity (doubled on return).</param>
    /// <param name="currentSize">The number of elements currently in use.</param>
    public static void GrowStack(ref JsonElement[] stack, ref int capacity, int currentSize)
    {
        int newCapacity = capacity * 2;
        JsonElement[] newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(stack, newArray, currentSize);
        ArrayPool<JsonElement>.Shared.Return(stack);
        stack = newArray;
        capacity = newCapacity;
    }

    /// <summary>
    /// Reverses a region of elements in-place (for document-order DFS).
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="start">The start index of the region.</param>
    /// <param name="length">The number of elements to reverse.</param>
    public static void ReverseRegion(JsonElement[] array, int start, int length)
    {
        int lo = start;
        int hi = start + length - 1;
        while (lo < hi)
        {
            JsonElement temp = array[lo];
            array[lo] = array[hi];
            array[hi] = temp;
            lo++;
            hi--;
        }
    }
}
