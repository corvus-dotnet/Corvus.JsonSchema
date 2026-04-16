// <copyright file="JMESPathCodeGenHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Public helper methods used by JMESPath source-generated evaluation code.
/// </summary>
/// <remarks>
/// <para>
/// These methods are called from code emitted by <c>JMESPathCodeGenerator</c>
/// and <c>JMESPathSourceGenerator</c>. They provide the same semantics as the
/// runtime <see cref="Compiler"/> — both share the same implementations to
/// guarantee identical behaviour.
/// </para>
/// <para>
/// This class is not intended for direct use by application code.
/// </para>
/// </remarks>
public static class JMESPathCodeGenHelpers
{
    /// <summary>
    /// A delegate that evaluates a JMESPath (sub-)expression against a data context.
    /// Used for expression-reference arguments (<c>&amp;expr</c>) in generated code.
    /// </summary>
    /// <param name="data">The current data context.</param>
    /// <param name="workspace">The workspace for intermediate document allocation.</param>
    /// <returns>The resulting JSON element.</returns>
    public delegate JsonElement ExpressionEvaluator(in JsonElement data, JsonWorkspace workspace);

    /// <summary>Gets a pre-parsed <c>null</c> element.</summary>
    public static readonly JsonElement NullElement = JsonElement.ParseValue("null"u8);

    /// <summary>Gets a pre-parsed <c>true</c> element.</summary>
    public static readonly JsonElement TrueElement = JsonElement.ParseValue("true"u8);

    /// <summary>Gets a pre-parsed <c>false</c> element.</summary>
    public static readonly JsonElement FalseElement = JsonElement.ParseValue("false"u8);

    /// <summary>Gets a pre-parsed empty array element.</summary>
    public static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);

    /// <summary>Gets a pre-parsed zero element.</summary>
    public static readonly JsonElement ZeroElement = JsonElement.ParseValue("0"u8);

    /// <summary>Gets a pre-parsed empty string element.</summary>
    public static readonly JsonElement EmptyStringElement = JsonElement.ParseValue("\"\""u8);

    /// <summary>Gets a pre-parsed <c>"string"</c> type element.</summary>
    public static readonly JsonElement TypeString = JsonElement.ParseValue("\"string\""u8);

    /// <summary>Gets a pre-parsed <c>"number"</c> type element.</summary>
    public static readonly JsonElement TypeNumber = JsonElement.ParseValue("\"number\""u8);

    /// <summary>Gets a pre-parsed <c>"boolean"</c> type element.</summary>
    public static readonly JsonElement TypeBoolean = JsonElement.ParseValue("\"boolean\""u8);

    /// <summary>Gets a pre-parsed <c>"array"</c> type element.</summary>
    public static readonly JsonElement TypeArray = JsonElement.ParseValue("\"array\""u8);

    /// <summary>Gets a pre-parsed <c>"object"</c> type element.</summary>
    public static readonly JsonElement TypeObject = JsonElement.ParseValue("\"object\""u8);

    /// <summary>Gets a pre-parsed <c>"null"</c> type element.</summary>
    public static readonly JsonElement TypeNull = JsonElement.ParseValue("\"null\""u8);

    /// <summary>
    /// Determines whether a JSON element is "truthy" per JMESPath semantics.
    /// </summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> if the value is truthy; otherwise <see langword="false"/>.</returns>
    public static bool IsTruthy(in JsonElement value)
    {
        if (value.IsNullOrUndefined())
        {
            return false;
        }

        switch (value.ValueKind)
        {
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return false;
            case JsonValueKind.True:
            case JsonValueKind.Number:
                return true;
            case JsonValueKind.String:
            {
                using UnescapedUtf8JsonString utf8Str = value.GetUtf8String();
                return utf8Str.Span.Length > 0;
            }

            case JsonValueKind.Array:
                return value.GetArrayLength() > 0;
            case JsonValueKind.Object:
                return value.GetPropertyCount() > 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Performs a deep-equality comparison of two JSON elements per JMESPath semantics.
    /// </summary>
    /// <param name="left">The left element.</param>
    /// <param name="right">The right element.</param>
    /// <returns><see langword="true"/> if the elements are deeply equal.</returns>
    public static bool DeepEquals(in JsonElement left, in JsonElement right)
    {
        if (left.IsNullOrUndefined() && right.IsNullOrUndefined())
        {
            return true;
        }

        if (left.IsNullOrUndefined() || right.IsNullOrUndefined())
        {
            return false;
        }

        if (left.ValueKind != right.ValueKind)
        {
            return false;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            case JsonValueKind.Number:
                return left.GetDouble() == right.GetDouble();
            case JsonValueKind.String:
            {
                using UnescapedUtf8JsonString leftStr = left.GetUtf8String();
                using UnescapedUtf8JsonString rightStr = right.GetUtf8String();
                return leftStr.Span.SequenceEqual(rightStr.Span);
            }

            case JsonValueKind.Array:
                return ArrayEquals(left, right);
            case JsonValueKind.Object:
                return ObjectEquals(left, right);
            default:
                return false;
        }
    }

    /// <summary>
    /// Returns <see cref="TrueElement"/> or <see cref="FalseElement"/>.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The corresponding JSON element.</returns>
    public static JsonElement BoolElement(bool value) => value ? TrueElement : FalseElement;

    /// <summary>
    /// Converts a <see cref="double"/> to a JSON number element.
    /// </summary>
    /// <param name="value">The numeric value.</param>
    /// <param name="workspace">The workspace (unused, reserved for future use).</param>
    /// <returns>A JSON number element.</returns>
    public static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonElement.ParseValue(buffer.Slice(0, bytesWritten));
        }

        return ZeroElement;
    }

    /// <summary>
    /// Converts a string to a JSON string element.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A JSON string element.</returns>
    public static JsonElement StringToElement(string value)
    {
        StringBuilder sb = new(value.Length + 8);
        sb.Append('"');
        foreach (char ch in value)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20)
                    {
                        sb.Append($"\\u{(int)ch:X4}");
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
        return JsonElement.ParseValue(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    /// <summary>
    /// Normalizes a slice index per JMESPath spec.
    /// </summary>
    /// <param name="index">The raw index value.</param>
    /// <param name="length">The array length.</param>
    /// <param name="isStart">Whether this is the start index.</param>
    /// <param name="positiveStep">Whether the step is positive.</param>
    /// <returns>The normalized index.</returns>
    public static int NormalizeSliceIndex(int index, int length, bool isStart, bool positiveStep)
    {
        if (index < 0)
        {
            index += length;
            if (index < 0)
            {
                index = positiveStep ? 0 : -1;
            }
        }
        else
        {
            if (positiveStep)
            {
                if (index > length)
                {
                    index = length;
                }
            }
            else
            {
                if (isStart && index > length - 1)
                {
                    index = length - 1;
                }
                else if (!isStart && index > length)
                {
                    index = length;
                }
            }
        }

        return index;
    }

    // ─── NUMERIC FUNCTIONS ──────────────────────────────────────────

    /// <summary>Computes <c>abs(value)</c>.</summary>
    public static JsonElement Abs(in JsonElement value, JsonWorkspace workspace)
    {
        RequireNumber("abs", value);
        return DoubleToElement(Math.Abs(value.GetDouble()), workspace);
    }

    /// <summary>Computes <c>ceil(value)</c>.</summary>
    public static JsonElement Ceil(in JsonElement value, JsonWorkspace workspace)
    {
        RequireNumber("ceil", value);
        return DoubleToElement(Math.Ceiling(value.GetDouble()), workspace);
    }

    /// <summary>Computes <c>floor(value)</c>.</summary>
    public static JsonElement Floor(in JsonElement value, JsonWorkspace workspace)
    {
        RequireNumber("floor", value);
        return DoubleToElement(Math.Floor(value.GetDouble()), workspace);
    }

    /// <summary>Computes <c>avg(array)</c>.</summary>
    public static JsonElement Avg(in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("avg", array);
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return NullElement;
        }

        double sum = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireNumber("avg", item);
            sum += item.GetDouble();
        }

        return DoubleToElement(sum / len, workspace);
    }

    /// <summary>Computes <c>sum(array)</c>.</summary>
    public static JsonElement Sum(in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("sum", array);
        double sum = 0;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireNumber("sum", item);
            sum += item.GetDouble();
        }

        return DoubleToElement(sum, workspace);
    }

    /// <summary>Computes <c>max(array)</c>.</summary>
    public static JsonElement Max(in JsonElement array)
    {
        RequireArray("max", array);
        if (array.GetArrayLength() == 0)
        {
            return NullElement;
        }

        return FindExtremum(array, isMax: true);
    }

    /// <summary>Computes <c>min(array)</c>.</summary>
    public static JsonElement Min(in JsonElement array)
    {
        RequireArray("min", array);
        if (array.GetArrayLength() == 0)
        {
            return NullElement;
        }

        return FindExtremum(array, isMax: false);
    }

    // ─── STRING FUNCTIONS ───────────────────────────────────────────

    /// <summary>Computes <c>length(value)</c>.</summary>
    public static JsonElement Length(in JsonElement value, JsonWorkspace workspace)
    {
        int len;
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
            {
                using UnescapedUtf8JsonString utf8Str = value.GetUtf8String();
                len = JsonElementHelpers.GetUtf8StringLength(utf8Str.Span);
                break;
            }

            case JsonValueKind.Array:
                len = value.GetArrayLength();
                break;
            case JsonValueKind.Object:
                len = value.GetPropertyCount();
                break;
            default:
                throw new JMESPathException("invalid-type: length() expects a string, array, or object argument.");
        }

        return DoubleToElement(len, workspace);
    }

    /// <summary>Computes <c>contains(subject, search)</c>.</summary>
    public static JsonElement Contains(in JsonElement subject, in JsonElement search)
    {
        if (subject.ValueKind == JsonValueKind.String)
        {
            RequireString("contains", search);
            using UnescapedUtf8JsonString subjectStr = subject.GetUtf8String();
            using UnescapedUtf8JsonString searchStr = search.GetUtf8String();
            return BoolElement(subjectStr.Span.IndexOf(searchStr.Span) >= 0);
        }

        if (subject.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in subject.EnumerateArray())
            {
                if (DeepEquals(item, search))
                {
                    return TrueElement;
                }
            }

            return FalseElement;
        }

        throw new JMESPathException("invalid-type: contains() expects a string or array as the first argument.");
    }

    /// <summary>Computes <c>starts_with(str, prefix)</c>.</summary>
    public static JsonElement StartsWith(in JsonElement str, in JsonElement prefix)
    {
        RequireString("starts_with", str);
        RequireString("starts_with", prefix);
        using UnescapedUtf8JsonString strUtf8 = str.GetUtf8String();
        using UnescapedUtf8JsonString prefixUtf8 = prefix.GetUtf8String();
        return BoolElement(strUtf8.Span.StartsWith(prefixUtf8.Span));
    }

    /// <summary>Computes <c>ends_with(str, suffix)</c>.</summary>
    public static JsonElement EndsWith(in JsonElement str, in JsonElement suffix)
    {
        RequireString("ends_with", str);
        RequireString("ends_with", suffix);
        using UnescapedUtf8JsonString strUtf8 = str.GetUtf8String();
        using UnescapedUtf8JsonString suffixUtf8 = suffix.GetUtf8String();
        return BoolElement(strUtf8.Span.EndsWith(suffixUtf8.Span));
    }

    /// <summary>Computes <c>join(separator, array)</c>.</summary>
    public static JsonElement Join(in JsonElement separator, in JsonElement array, JsonWorkspace workspace)
    {
        RequireString("join", separator);
        RequireArray("join", array);

        int len = array.GetArrayLength();
        if (len == 0)
        {
            return EmptyStringElement;
        }

        // Use RawUtf8JsonString for separator — already JSON-escaped
        using RawUtf8JsonString sepRaw = JsonMarshal.GetRawUtf8Value(separator);
        ReadOnlySpan<byte> sepBytes = sepRaw.Span;

        // Strip quotes from raw value to get inner escaped content
        ReadOnlySpan<byte> sepInner = sepBytes.Slice(1, sepBytes.Length - 2);

        // Estimate buffer size and rent from pool
        const int estimatedSize = 256;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(estimatedSize);
        try
        {
            int pos = 0;
            buffer[pos++] = (byte)'"';

            bool first = true;
            foreach (JsonElement item in array.EnumerateArray())
            {
                RequireString("join", item);

                if (!first)
                {
                    EnsureJoinBuffer(ref buffer, pos, sepInner.Length);
                    sepInner.CopyTo(buffer.AsSpan(pos));
                    pos += sepInner.Length;
                }

                using RawUtf8JsonString itemRaw = JsonMarshal.GetRawUtf8Value(item);
                ReadOnlySpan<byte> itemInner = itemRaw.Span.Slice(1, itemRaw.Span.Length - 2);
                EnsureJoinBuffer(ref buffer, pos, itemInner.Length);
                itemInner.CopyTo(buffer.AsSpan(pos));
                pos += itemInner.Length;
                first = false;
            }

            EnsureJoinBuffer(ref buffer, pos, 1);
            buffer[pos++] = (byte)'"';

            FixedJsonValueDocument<JsonElement> doc =
                FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
                    new ReadOnlySpan<byte>(buffer, 0, pos));
            workspace.RegisterDocument(doc);
            return doc.RootElement;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Computes <c>reverse(value)</c> for strings and arrays.</summary>
    public static JsonElement Reverse(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString utf8Str = value.GetUtf8String();
            ReadOnlySpan<byte> source = utf8Str.Span;

            if (source.Length == 0)
            {
                return EmptyStringElement;
            }

            byte[] reversedBuffer = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                int writePos = 0;
                int i = source.Length;
                while (i > 0)
                {
                    int cpEnd = i;
                    i--;
                    while (i > 0 && (source[i] & 0xC0) == 0x80)
                    {
                        i--;
                    }

                    int cpLen = cpEnd - i;
                    source.Slice(i, cpLen).CopyTo(reversedBuffer.AsSpan(writePos));
                    writePos += cpLen;
                }

                ReadOnlySpan<byte> reversed = reversedBuffer.AsSpan(0, writePos);
                int maxEscapedLen = (writePos * 6) + 2;
                byte[] escapedBuffer = ArrayPool<byte>.Shared.Rent(maxEscapedLen);
                try
                {
                    int pos = 0;
                    escapedBuffer[pos++] = (byte)'"';
                    pos += WriteJsonEscapedFromUtf8(reversed, escapedBuffer.AsSpan(pos));
                    escapedBuffer[pos++] = (byte)'"';

                    FixedJsonValueDocument<JsonElement> doc =
                        FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
                            new ReadOnlySpan<byte>(escapedBuffer, 0, pos));
                    workspace.RegisterDocument(doc);
                    return doc.RootElement;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(escapedBuffer);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(reversedBuffer);
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            int len = value.GetArrayLength();

            // Copy elements forward via enumerator (O(n)), then reverse-iterate the managed array
            JsonElement[] temp = ArrayPool<JsonElement>.Shared.Rent(len);
            try
            {
                int idx = 0;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    temp[idx++] = item;
                }

                JMESPathSequenceBuilder builder = default;
                try
                {
                    for (int i = len - 1; i >= 0; i--)
                    {
                        builder.Add(temp[i]);
                    }

                    return builder.ToElement(workspace);
                }
                finally
                {
                    builder.ReturnArray();
                }
            }
            finally
            {
                temp.AsSpan(0, len).Clear();
                ArrayPool<JsonElement>.Shared.Return(temp);
            }
        }

        throw new JMESPathException("invalid-type: reverse() expects a string or array argument.");
    }

    // ─── OBJECT / ARRAY FUNCTIONS ───────────────────────────────────

    /// <summary>Computes <c>keys(obj)</c>.</summary>
    public static JsonElement Keys(in JsonElement obj, JsonWorkspace workspace)
    {
        RequireObject("keys", obj);

        int count = obj.GetPropertyCount();
        if (count == 0)
        {
            return EmptyArrayElement;
        }

        JsonElement[] keys = ArrayPool<JsonElement>.Shared.Rent(count);
        try
        {
            int idx = 0;
            foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
            {
                using UnescapedUtf8JsonString name = prop.Utf8NameSpan;
                keys[idx++] = StringElementFromUnescapedUtf8(name.Span, workspace);
            }

            Array.Sort(keys, 0, count, Utf8StringElementComparer.Instance);

            JMESPathSequenceBuilder builder = default;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    builder.Add(keys[i]);
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        }
        finally
        {
            keys.AsSpan(0, count).Clear();
            ArrayPool<JsonElement>.Shared.Return(keys);
        }
    }

    /// <summary>Computes <c>values(obj)</c>.</summary>
    public static JsonElement Values(in JsonElement obj, JsonWorkspace workspace)
    {
        RequireObject("values", obj);

        JMESPathSequenceBuilder builder = default;
        try
        {
            foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
            {
                builder.Add(prop.Value);
            }

            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    /// <summary>Computes <c>sort(array)</c>.</summary>
    public static JsonElement Sort(in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("sort", array);
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return EmptyArrayElement;
        }

        JsonElement[] elements = ArrayPool<JsonElement>.Shared.Rent(len);
        try
        {
            int idx = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                elements[idx++] = item;
            }

            JsonValueKind kind = elements[0].ValueKind;
            if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
            {
                throw new JMESPathException("invalid-type: sort() expects an array of numbers or strings.");
            }

            for (int i = 1; i < len; i++)
            {
                if (elements[i].ValueKind != kind)
                {
                    throw new JMESPathException("invalid-type: sort() requires all elements to be the same type.");
                }
            }

            StableSort(elements.AsSpan(0, len), kind);

            JMESPathSequenceBuilder builder = default;
            try
            {
                for (int i = 0; i < len; i++)
                {
                    builder.Add(elements[i]);
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        }
        finally
        {
            elements.AsSpan(0, len).Clear();
            ArrayPool<JsonElement>.Shared.Return(elements);
        }
    }

    /// <summary>Computes <c>sort_by(array, &amp;expr)</c>.</summary>
    public static JsonElement SortBy(in JsonElement array, ExpressionEvaluator expr, JsonWorkspace workspace)
    {
        RequireArray("sort_by", array);
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return EmptyArrayElement;
        }

        (int Index, JsonElement Element, JsonElement Key)[] pairs =
            ArrayPool<(int, JsonElement, JsonElement)>.Shared.Rent(len);
        try
        {
            int idx = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                pairs[idx] = (idx, item, expr(item, workspace));
                idx++;
            }

            JsonValueKind keyKind = pairs[0].Key.ValueKind;
            if (keyKind != JsonValueKind.Number && keyKind != JsonValueKind.String)
            {
                throw new JMESPathException("invalid-type: sort_by() expression must return numbers or strings.");
            }

            for (int i = 1; i < len; i++)
            {
                if (pairs[i].Key.ValueKind != keyKind)
                {
                    throw new JMESPathException("invalid-type: sort_by() expression must return consistent types.");
                }
            }

            StableSortBy(pairs.AsSpan(0, len), keyKind);

            JMESPathSequenceBuilder builder = default;
            try
            {
                for (int i = 0; i < len; i++)
                {
                    builder.Add(pairs[i].Element);
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        }
        finally
        {
            pairs.AsSpan(0, len).Clear();
            ArrayPool<(int, JsonElement, JsonElement)>.Shared.Return(pairs);
        }
    }

    /// <summary>Computes <c>map(&amp;expr, array)</c>.</summary>
    public static JsonElement Map(ExpressionEvaluator expr, in JsonElement array, JsonWorkspace workspace)
    {
        RequireArray("map", array);
        JMESPathSequenceBuilder builder = default;
        try
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                JsonElement mapped = expr(item, workspace);
                builder.Add(mapped.IsNullOrUndefined() ? NullElement : mapped);
            }

            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    /// <summary>Computes <c>max_by(array, &amp;expr)</c>.</summary>
    public static JsonElement MaxBy(in JsonElement array, ExpressionEvaluator expr, JsonWorkspace workspace)
    {
        RequireArray("max_by", array);
        return FindExtremumBy(array, expr, workspace, isMax: true);
    }

    /// <summary>Computes <c>min_by(array, &amp;expr)</c>.</summary>
    public static JsonElement MinBy(in JsonElement array, ExpressionEvaluator expr, JsonWorkspace workspace)
    {
        RequireArray("min_by", array);
        return FindExtremumBy(array, expr, workspace, isMax: false);
    }

    /// <summary>Computes <c>merge(obj1, obj2, ...)</c>.</summary>
    public static JsonElement Merge(JsonElement[] objects, JsonWorkspace workspace)
    {
        int estimatedCount = 0;
        foreach (JsonElement val in objects)
        {
            RequireObject("merge", val);
            estimatedCount += val.GetPropertyCount();
        }

        JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            (objects, objects.Length),
            static (in (JsonElement[] Vals, int Count) ctx, ref JsonElement.ObjectBuilder builder) =>
            {
                for (int i = 0; i < ctx.Count; i++)
                {
                    foreach (JsonProperty<JsonElement> prop in ctx.Vals[i].EnumerateObject())
                    {
                        using UnescapedUtf8JsonString name = prop.Utf8NameSpan;
                        builder.AddProperty(name.Span, prop.Value, escapeName: false, nameRequiresUnescaping: false);
                    }
                }
            },
            estimatedMemberCount: estimatedCount);

        return (JsonElement)doc.RootElement;
    }

    /// <summary>Computes <c>not_null(arg1, arg2, ...)</c>.</summary>
    public static JsonElement NotNull(JsonElement[] values)
    {
        foreach (JsonElement val in values)
        {
            if (!val.IsNullOrUndefined())
            {
                return val;
            }
        }

        return NullElement;
    }

    /// <summary>Computes <c>to_array(value)</c>.</summary>
    public static JsonElement ToArray(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value;
        }

        JMESPathSequenceBuilder builder = default;
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

    /// <summary>Computes <c>to_number(value)</c>.</summary>
    public static JsonElement ToNumber(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString utf8Str = value.GetUtf8String();
            if (Utf8Parser.TryParse(utf8Str.Span, out double d, out int consumed)
                && consumed == utf8Str.Span.Length)
            {
                return DoubleToElement(d, workspace);
            }
        }

        return NullElement;
    }

    /// <summary>Computes <c>to_string(value)</c>.</summary>
    public static JsonElement ToString(in JsonElement value, JsonWorkspace workspace)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value;
        }

        if (value.IsNullOrUndefined())
        {
            return StringElementFromUnescapedUtf8("null"u8, workspace);
        }

        return ToStringCore(value, workspace);
    }

    private static readonly JsonWriterOptions CompactWriterOptions = new() { Indented = false };

    private static JsonElement ToStringCore(in JsonElement val, JsonWorkspace ws)
    {
        // For numbers and booleans, GetRawUtf8Value is always compact — zero alloc
        // For arrays and objects, re-serialize through a rented Utf8JsonWriter for compact output
        // (GetRawUtf8Value would preserve original whitespace, e.g., "[0, 1]" instead of "[0,1]")
        if (val.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            Utf8JsonWriter writer = ws.RentWriterAndBuffer(CompactWriterOptions, 256, out IByteBufferWriter bufferWriter);
            try
            {
                val.WriteTo(writer);
                writer.Flush();
                return EscapeAndWrapAsString(bufferWriter.WrittenSpan, ws);
            }
            finally
            {
                ws.ReturnWriterAndBuffer(writer, bufferWriter);
            }
        }

        // Numbers, booleans: raw UTF-8 is already compact
        using RawUtf8JsonString rawUtf8 = JsonMarshal.GetRawUtf8Value(val);
        return EscapeAndWrapAsString(rawUtf8.Span, ws);
    }

    private static JsonElement EscapeAndWrapAsString(ReadOnlySpan<byte> serialized, JsonWorkspace ws)
    {
        int maxEscapedLen = (serialized.Length * 6) + 2;
        byte[] escapedBuffer = ArrayPool<byte>.Shared.Rent(maxEscapedLen);
        try
        {
            int pos = 0;
            escapedBuffer[pos++] = (byte)'"';
            pos += WriteJsonEscapedFromUtf8(serialized, escapedBuffer.AsSpan(pos));
            escapedBuffer[pos++] = (byte)'"';

            FixedJsonValueDocument<JsonElement> doc =
                FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
                    new ReadOnlySpan<byte>(escapedBuffer, 0, pos));
            ws.RegisterDocument(doc);
            return doc.RootElement;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(escapedBuffer);
        }
    }

    /// <summary>Computes <c>type(value)</c>.</summary>
    public static JsonElement TypeOf(in JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => TypeString,
            JsonValueKind.Number => TypeNumber,
            JsonValueKind.True or JsonValueKind.False => TypeBoolean,
            JsonValueKind.Array => TypeArray,
            JsonValueKind.Object => TypeObject,
            _ => TypeNull,
        };
    }

    // ─── VALIDATION HELPERS ─────────────────────────────────────────

    /// <summary>Asserts that <paramref name="value"/> is a JSON number.</summary>
    public static void RequireNumber(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a number argument.");
        }
    }

    /// <summary>Asserts that <paramref name="value"/> is a JSON string.</summary>
    public static void RequireString(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a string argument.");
        }
    }

    /// <summary>Asserts that <paramref name="value"/> is a JSON array.</summary>
    public static void RequireArray(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an array argument.");
        }
    }

    /// <summary>Asserts that <paramref name="value"/> is a JSON object.</summary>
    public static void RequireObject(string funcName, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an object argument.");
        }
    }

    // ─── PRIVATE HELPERS ────────────────────────────────────────────
    private static JsonElement FindExtremum(in JsonElement array, bool isMax)
    {
        var enumerator = array.EnumerateArray();
        enumerator.MoveNext();
        JsonElement best = enumerator.Current;
        JsonValueKind kind = best.ValueKind;

        if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: max()/min() expects an array of numbers or strings.");
        }

        while (enumerator.MoveNext())
        {
            JsonElement item = enumerator.Current;
            if (item.ValueKind != kind)
            {
                throw new JMESPathException("invalid-type: max()/min() requires all elements to be the same type.");
            }

            int cmp;
            if (kind == JsonValueKind.Number)
            {
                cmp = item.GetDouble().CompareTo(best.GetDouble());
            }
            else
            {
                using UnescapedUtf8JsonString itemStr = item.GetUtf8String();
                using UnescapedUtf8JsonString bestStr = best.GetUtf8String();
                cmp = itemStr.Span.SequenceCompareTo(bestStr.Span);
            }

            if (isMax ? cmp > 0 : cmp < 0)
            {
                best = item;
            }
        }

        return best;
    }

    private static JsonElement FindExtremumBy(
        in JsonElement array,
        ExpressionEvaluator expr,
        JsonWorkspace workspace,
        bool isMax)
    {
        int len = array.GetArrayLength();
        if (len == 0)
        {
            return NullElement;
        }

        var enumerator = array.EnumerateArray();
        enumerator.MoveNext();
        JsonElement bestElement = enumerator.Current;
        JsonElement bestKey = expr(bestElement, workspace);
        JsonValueKind keyKind = bestKey.ValueKind;

        if (keyKind != JsonValueKind.Number && keyKind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: max_by()/min_by() expression must return numbers or strings.");
        }

        while (enumerator.MoveNext())
        {
            JsonElement item = enumerator.Current;
            JsonElement key = expr(item, workspace);
            if (key.ValueKind != keyKind)
            {
                throw new JMESPathException("invalid-type: max_by()/min_by() expression must return consistent types.");
            }

            int cmp;
            if (keyKind == JsonValueKind.Number)
            {
                cmp = key.GetDouble().CompareTo(bestKey.GetDouble());
            }
            else
            {
                using UnescapedUtf8JsonString keyStr = key.GetUtf8String();
                using UnescapedUtf8JsonString bestKeyStr = bestKey.GetUtf8String();
                cmp = keyStr.Span.SequenceCompareTo(bestKeyStr.Span);
            }

            if (isMax ? cmp > 0 : cmp < 0)
            {
                bestElement = item;
                bestKey = key;
            }
        }

        return bestElement;
    }

    private static bool ArrayEquals(in JsonElement left, in JsonElement right)
    {
        int len = left.GetArrayLength();
        if (len != right.GetArrayLength())
        {
            return false;
        }

        var leftEnum = left.EnumerateArray();
        var rightEnum = right.EnumerateArray();
        while (leftEnum.MoveNext())
        {
            rightEnum.MoveNext();
            if (!DeepEquals(leftEnum.Current, rightEnum.Current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectEquals(in JsonElement left, in JsonElement right)
    {
        if (left.GetPropertyCount() != right.GetPropertyCount())
        {
            return false;
        }

        foreach (JsonProperty<JsonElement> prop in left.EnumerateObject())
        {
            using UnescapedUtf8JsonString name = prop.Utf8NameSpan;
            if (!right.TryGetProperty(name.Span, out JsonElement rightVal)
                || !DeepEquals(prop.Value, rightVal))
            {
                return false;
            }
        }

        return true;
    }

    private static JsonElement StringElementFromUnescapedUtf8(ReadOnlySpan<byte> unescaped, JsonWorkspace workspace)
    {
        if (unescaped.Length == 0)
        {
            return EmptyStringElement;
        }

        int maxEscapedLen = (unescaped.Length * 6) + 2;
        byte[] escapedBuffer = ArrayPool<byte>.Shared.Rent(maxEscapedLen);
        try
        {
            int pos = 0;
            escapedBuffer[pos++] = (byte)'"';
            pos += WriteJsonEscapedFromUtf8(unescaped, escapedBuffer.AsSpan(pos));
            escapedBuffer[pos++] = (byte)'"';

            FixedJsonValueDocument<JsonElement> doc =
                FixedJsonValueDocument<JsonElement>.ForStringFromSpan(
                    new ReadOnlySpan<byte>(escapedBuffer, 0, pos));
            workspace.RegisterDocument(doc);
            return doc.RootElement;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(escapedBuffer);
        }
    }

    private static int WriteJsonEscapedFromUtf8(ReadOnlySpan<byte> source, Span<byte> destination)
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
                destination[pos++] = (byte)'\\';
                switch (b)
                {
                    case (byte)'\b':
                        destination[pos++] = (byte)'b';
                        break;
                    case (byte)'\f':
                        destination[pos++] = (byte)'f';
                        break;
                    case (byte)'\n':
                        destination[pos++] = (byte)'n';
                        break;
                    case (byte)'\r':
                        destination[pos++] = (byte)'r';
                        break;
                    case (byte)'\t':
                        destination[pos++] = (byte)'t';
                        break;
                    default:
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
                // Multi-byte UTF-8 (≥0x80) and printable ASCII pass through unchanged
                destination[pos++] = b;
            }
        }

        return pos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte HexDigit(int value) =>
        (byte)(value < 10 ? '0' + value : 'a' + value - 10);

    private static void EnsureJoinBuffer(ref byte[] buffer, int pos, int needed)
    {
        if (pos + needed <= buffer.Length)
        {
            return;
        }

        int newSize = Math.Max(buffer.Length * 2, pos + needed + 64);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        buffer.AsSpan(0, pos).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }

    private static void StableSort(Span<JsonElement> elements, JsonValueKind kind)
    {
        int len = elements.Length;

        (int Index, JsonElement Element)[] pairs = ArrayPool<(int, JsonElement)>.Shared.Rent(len);
        try
        {
            for (int i = 0; i < len; i++)
            {
                pairs[i] = (i, elements[i]);
            }

            IComparer<(int Index, JsonElement Element)> comparer = kind == JsonValueKind.Number
                ? StableNumberComparer.Instance
                : StableStringComparer.Instance;

            Array.Sort(pairs, 0, len, comparer);

            for (int i = 0; i < len; i++)
            {
                elements[i] = pairs[i].Element;
            }
        }
        finally
        {
            pairs.AsSpan(0, len).Clear();
            ArrayPool<(int, JsonElement)>.Shared.Return(pairs);
        }
    }

    private static void StableSortBy(
        Span<(int Index, JsonElement Element, JsonElement Key)> pairs,
        JsonValueKind keyKind)
    {
        int len = pairs.Length;

        IComparer<(int Index, JsonElement Element, JsonElement Key)> comparer = keyKind == JsonValueKind.Number
            ? StableKeyedNumberComparer.Instance
            : StableKeyedStringComparer.Instance;

        // Copy to an array for Array.Sort (Span doesn't support IComparer overload)
        (int Index, JsonElement Element, JsonElement Key)[] sortArray =
            ArrayPool<(int, JsonElement, JsonElement)>.Shared.Rent(len);
        try
        {
            for (int i = 0; i < len; i++)
            {
                sortArray[i] = pairs[i];
            }

            Array.Sort(sortArray, 0, len, comparer);

            for (int i = 0; i < len; i++)
            {
                pairs[i] = sortArray[i];
            }
        }
        finally
        {
            sortArray.AsSpan(0, len).Clear();
            ArrayPool<(int, JsonElement, JsonElement)>.Shared.Return(sortArray);
        }
    }

    private sealed class StableNumberComparer : IComparer<(int Index, JsonElement Element)>
    {
        public static readonly StableNumberComparer Instance = new();

        public int Compare((int Index, JsonElement Element) x, (int Index, JsonElement Element) y)
        {
            int cmp = x.Element.GetDouble().CompareTo(y.Element.GetDouble());
            return cmp != 0 ? cmp : x.Index.CompareTo(y.Index);
        }
    }

    private sealed class StableStringComparer : IComparer<(int Index, JsonElement Element)>
    {
        public static readonly StableStringComparer Instance = new();

        public int Compare((int Index, JsonElement Element) x, (int Index, JsonElement Element) y)
        {
            using UnescapedUtf8JsonString xStr = x.Element.GetUtf8String();
            using UnescapedUtf8JsonString yStr = y.Element.GetUtf8String();
            int cmp = xStr.Span.SequenceCompareTo(yStr.Span);
            return cmp != 0 ? cmp : x.Index.CompareTo(y.Index);
        }
    }

    private sealed class StableKeyedNumberComparer : IComparer<(int Index, JsonElement Element, JsonElement Key)>
    {
        public static readonly StableKeyedNumberComparer Instance = new();

        public int Compare((int Index, JsonElement Element, JsonElement Key) x, (int Index, JsonElement Element, JsonElement Key) y)
        {
            int cmp = x.Key.GetDouble().CompareTo(y.Key.GetDouble());
            return cmp != 0 ? cmp : x.Index.CompareTo(y.Index);
        }
    }

    private sealed class StableKeyedStringComparer : IComparer<(int Index, JsonElement Element, JsonElement Key)>
    {
        public static readonly StableKeyedStringComparer Instance = new();

        public int Compare((int Index, JsonElement Element, JsonElement Key) x, (int Index, JsonElement Element, JsonElement Key) y)
        {
            using UnescapedUtf8JsonString xStr = x.Key.GetUtf8String();
            using UnescapedUtf8JsonString yStr = y.Key.GetUtf8String();
            int cmp = xStr.Span.SequenceCompareTo(yStr.Span);
            return cmp != 0 ? cmp : x.Index.CompareTo(y.Index);
        }
    }

    private sealed class Utf8StringElementComparer : IComparer<JsonElement>
    {
        public static readonly Utf8StringElementComparer Instance = new();

        public int Compare(JsonElement x, JsonElement y)
        {
            using UnescapedUtf8JsonString xStr = x.GetUtf8String();
            using UnescapedUtf8JsonString yStr = y.GetUtf8String();
            return xStr.Span.SequenceCompareTo(yStr.Span);
        }
    }
}
