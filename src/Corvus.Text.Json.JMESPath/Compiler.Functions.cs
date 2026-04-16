// <copyright file="Compiler.Functions.cs" company="Endjin Limited">
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
/// JMESPath built-in function implementations.
/// </summary>
internal static partial class Compiler
{
    private static readonly JsonElement TrueElement = JsonElement.ParseValue("true"u8);
    private static readonly JsonElement FalseElement = JsonElement.ParseValue("false"u8);
    private static readonly JsonElement EmptyArrayElement = JsonElement.ParseValue("[]"u8);
    private static readonly JsonElement ZeroElement = JsonElement.ParseValue("0"u8);
    private static readonly JsonElement EmptyStringElement = JsonElement.ParseValue("\"\""u8);

    private static readonly JsonElement TypeString = JsonElement.ParseValue("\"string\""u8);
    private static readonly JsonElement TypeNumber = JsonElement.ParseValue("\"number\""u8);
    private static readonly JsonElement TypeBoolean = JsonElement.ParseValue("\"boolean\""u8);
    private static readonly JsonElement TypeArray = JsonElement.ParseValue("\"array\""u8);
    private static readonly JsonElement TypeObject = JsonElement.ParseValue("\"object\""u8);
    private static readonly JsonElement TypeNull = JsonElement.ParseValue("\"null\""u8);

    // ─── NUMERIC FUNCTIONS ──────────────────────────────────────────
    private static JMESPathEval CompileAbs(JMESPathNode[] args)
    {
        ValidateArity("abs", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireNumber("abs", val);
            return DoubleToElement(Math.Abs(val.GetDouble()), ws);
        };
    }

    private static JMESPathEval CompileAvg(JMESPathNode[] args)
    {
        ValidateArity("avg", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("avg", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return NullElement;
            }

            double sum = 0;
            foreach (JsonElement item in val.EnumerateArray())
            {
                RequireNumber("avg", item);
                sum += item.GetDouble();
            }

            return DoubleToElement(sum / len, ws);
        };
    }

    private static JMESPathEval CompileCeil(JMESPathNode[] args)
    {
        ValidateArity("ceil", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireNumber("ceil", val);
            return DoubleToElement(Math.Ceiling(val.GetDouble()), ws);
        };
    }

    private static JMESPathEval CompileFloor(JMESPathNode[] args)
    {
        ValidateArity("floor", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireNumber("floor", val);
            return DoubleToElement(Math.Floor(val.GetDouble()), ws);
        };
    }

    private static JMESPathEval CompileSum(JMESPathNode[] args)
    {
        ValidateArity("sum", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("sum", val);
            double sum = 0;
            foreach (JsonElement item in val.EnumerateArray())
            {
                RequireNumber("sum", item);
                sum += item.GetDouble();
            }

            return DoubleToElement(sum, ws);
        };
    }

    private static JMESPathEval CompileMax(JMESPathNode[] args)
    {
        ValidateArity("max", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("max", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return NullElement;
            }

            return FindExtremum(val, isMax: true);
        };
    }

    private static JMESPathEval CompileMin(JMESPathNode[] args)
    {
        ValidateArity("min", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("min", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return NullElement;
            }

            return FindExtremum(val, isMax: false);
        };
    }

    private static JMESPathEval CompileToNumber(JMESPathNode[] args)
    {
        ValidateArity("to_number", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.Number)
            {
                return val;
            }

            if (val.ValueKind == JsonValueKind.String)
            {
                using UnescapedUtf8JsonString utf8 = val.GetUtf8String();
                ReadOnlySpan<byte> span = utf8.Span;
                if (span.Length > 0 && Utf8Parser.TryParse(span, out double d, out int consumed) && consumed == span.Length)
                {
                    return DoubleToElement(d, ws);
                }
            }

            return NullElement;
        };
    }

    // ─── STRING FUNCTIONS ───────────────────────────────────────────
    private static JMESPathEval CompileContains(JMESPathNode[] args)
    {
        ValidateArity("contains", args, 2);
        JMESPathEval evalSubject = CompileNode(args[0]);
        JMESPathEval evalSearch = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement subject = evalSubject(data, ws);
            JsonElement search = evalSearch(data, ws);

            if (subject.ValueKind == JsonValueKind.String)
            {
                RequireString("contains", search);
                using UnescapedUtf8JsonString subjectStr = subject.GetUtf8String();
                using UnescapedUtf8JsonString searchStr = search.GetUtf8String();
                bool found = subjectStr.Span.IndexOf(searchStr.Span) >= 0;
                return found ? TrueElement : FalseElement;
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
        };
    }

    private static JMESPathEval CompileEndsWith(JMESPathNode[] args)
    {
        ValidateArity("ends_with", args, 2);
        JMESPathEval evalStr = CompileNode(args[0]);
        JMESPathEval evalSuffix = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement str = evalStr(data, ws);
            JsonElement suffix = evalSuffix(data, ws);
            RequireString("ends_with", str);
            RequireString("ends_with", suffix);
            using UnescapedUtf8JsonString strUtf8 = str.GetUtf8String();
            using UnescapedUtf8JsonString suffixUtf8 = suffix.GetUtf8String();
            bool found = strUtf8.Span.EndsWith(suffixUtf8.Span);
            return found ? TrueElement : FalseElement;
        };
    }

    private static JMESPathEval CompileStartsWith(JMESPathNode[] args)
    {
        ValidateArity("starts_with", args, 2);
        JMESPathEval evalStr = CompileNode(args[0]);
        JMESPathEval evalPrefix = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement str = evalStr(data, ws);
            JsonElement prefix = evalPrefix(data, ws);
            RequireString("starts_with", str);
            RequireString("starts_with", prefix);
            using UnescapedUtf8JsonString strUtf8 = str.GetUtf8String();
            using UnescapedUtf8JsonString prefixUtf8 = prefix.GetUtf8String();
            bool found = strUtf8.Span.StartsWith(prefixUtf8.Span);
            return found ? TrueElement : FalseElement;
        };
    }

    private static JMESPathEval CompileJoin(JMESPathNode[] args)
    {
        ValidateArity("join", args, 2);
        JMESPathEval evalSep = CompileNode(args[0]);
        JMESPathEval evalArr = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement sep = evalSep(data, ws);
            JsonElement arr = evalArr(data, ws);
            RequireString("join", sep);
            RequireArray("join", arr);

            int arrLen = arr.GetArrayLength();
            if (arrLen == 0)
            {
                return EmptyStringElement;
            }

            // Get separator as raw JSON (includes quotes, already escaped)
            using RawUtf8JsonString sepRaw = JsonMarshal.GetRawUtf8Value(sep);
            ReadOnlySpan<byte> sepInner = sepRaw.Span.Slice(1, sepRaw.Span.Length - 2);

            // Build result in a rented buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(256);
            int pos = 0;
            buffer[pos++] = (byte)'"';

            try
            {
                bool first = true;
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    RequireString("join", item);

                    if (!first)
                    {
                        EnsureJoinBuffer(ref buffer, pos, sepInner.Length);
                        sepInner.CopyTo(buffer.AsSpan(pos));
                        pos += sepInner.Length;
                    }

                    // Get item's raw JSON bytes (includes quotes, already escaped)
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
                ws.RegisterDocument(doc);
                return doc.RootElement;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        };
    }

    private static void EnsureJoinBuffer(ref byte[] buffer, int pos, int needed)
    {
        if (pos + needed >= buffer.Length)
        {
            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(buffer.Length * 2, pos + needed + 1));
            buffer.AsSpan(0, pos).CopyTo(newBuffer);
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = newBuffer;
        }
    }

    private static JMESPathEval CompileLength(JMESPathNode[] args)
    {
        ValidateArity("length", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            int len;
            switch (val.ValueKind)
            {
                case JsonValueKind.String:
                    {
                        using UnescapedUtf8JsonString utf8 = val.GetUtf8String();
                        len = JsonElementHelpers.GetUtf8StringLength(utf8.Span);
                        break;
                    }

                case JsonValueKind.Array:
                    len = val.GetArrayLength();
                    break;
                case JsonValueKind.Object:
                    len = val.GetPropertyCount();
                    break;
                default:
                    throw new JMESPathException("invalid-type: length() expects a string, array, or object argument.");
            }

            return DoubleToElement(len, ws);
        };
    }

    private static JMESPathEval CompileReverse(JMESPathNode[] args)
    {
        ValidateArity("reverse", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.String)
            {
                using UnescapedUtf8JsonString utf8Str = val.GetUtf8String();
                ReadOnlySpan<byte> source = utf8Str.Span;

                if (source.Length == 0)
                {
                    return EmptyStringElement;
                }

                // Reverse code points in UTF-8: each code point is 1-4 bytes.
                // The reversed bytes are the same length as the source.
                byte[] reversedBuffer = ArrayPool<byte>.Shared.Rent(source.Length);
                try
                {
                    int writePos = 0;

                    // Walk backwards through the source, identifying code point boundaries
                    int i = source.Length;
                    while (i > 0)
                    {
                        // Find the start of this code point by scanning back over continuation bytes (10xxxxxx)
                        int cpEnd = i;
                        i--;
                        while (i > 0 && (source[i] & 0xC0) == 0x80)
                        {
                            i--;
                        }

                        // Copy the code point bytes [i..cpEnd) into the reversed buffer
                        int cpLen = cpEnd - i;
                        source.Slice(i, cpLen).CopyTo(reversedBuffer.AsSpan(writePos));
                        writePos += cpLen;
                    }

                    // JSON-escape the reversed bytes and create a string element
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
                        ws.RegisterDocument(doc);
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

            if (val.ValueKind == JsonValueKind.Array)
            {
                int len = val.GetArrayLength();

                // Copy elements forward via enumerator (O(n)), then reverse-iterate the managed array
                JsonElement[] temp = ArrayPool<JsonElement>.Shared.Rent(len);
                try
                {
                    int idx = 0;
                    foreach (JsonElement item in val.EnumerateArray())
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

                        return builder.ToElement(ws);
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
        };
    }

    // ─── TYPE FUNCTIONS ─────────────────────────────────────────────
    private static JMESPathEval CompileType(JMESPathNode[] args)
    {
        ValidateArity("type", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            return val.ValueKind switch
            {
                JsonValueKind.String => TypeString,
                JsonValueKind.Number => TypeNumber,
                JsonValueKind.True or JsonValueKind.False => TypeBoolean,
                JsonValueKind.Array => TypeArray,
                JsonValueKind.Object => TypeObject,
                _ => TypeNull,
            };
        };
    }

    private static JMESPathEval CompileToString(JMESPathNode[] args)
    {
        ValidateArity("to_string", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.String)
            {
                return val;
            }

            if (val.IsNullOrUndefined())
            {
                return StringElementFromUnescapedUtf8("null"u8, ws);
            }

            return ToStringCore(val, ws);
        };
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

    private static JMESPathEval CompileToArray(JMESPathNode[] args)
    {
        ValidateArity("to_array", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            if (val.ValueKind == JsonValueKind.Array)
            {
                return val;
            }

            JMESPathSequenceBuilder builder = default;
            try
            {
                builder.Add(val);
                return builder.ToElement(ws);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    // ─── OBJECT / ARRAY FUNCTIONS ───────────────────────────────────
    private static JMESPathEval CompileKeys(JMESPathNode[] args)
    {
        ValidateArity("keys", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireObject("keys", val);

            int count = val.GetPropertyCount();
            if (count == 0)
            {
                return JMESPathCodeGenHelpers.EmptyArrayElement;
            }

            // Create key elements and sort by their UTF-8 content (ordinal sort)
            JsonElement[] keys = ArrayPool<JsonElement>.Shared.Rent(count);
            try
            {
                int idx = 0;
                foreach (JsonProperty<JsonElement> prop in val.EnumerateObject())
                {
                    using UnescapedUtf8JsonString name = prop.Utf8NameSpan;
                    keys[idx++] = StringElementFromUnescapedUtf8(name.Span, ws);
                }

                // Sort using UTF-8 ordinal comparison of the unescaped string content
                Array.Sort(keys, 0, count, Utf8StringElementComparer.Instance);

                JMESPathSequenceBuilder builder = default;
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        builder.Add(keys[i]);
                    }

                    return builder.ToElement(ws);
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
        };
    }

    private static JMESPathEval CompileValues(JMESPathNode[] args)
    {
        ValidateArity("values", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireObject("values", val);

            JMESPathSequenceBuilder builder = default;
            try
            {
                foreach (JsonProperty<JsonElement> prop in val.EnumerateObject())
                {
                    builder.Add(prop.Value);
                }

                return builder.ToElement(ws);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    private static JMESPathEval CompileMerge(JMESPathNode[] args)
    {
        ValidateMinArity("merge", args, 1);
        JMESPathEval[] evalArgs = new JMESPathEval[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            evalArgs[i] = CompileNode(args[i]);
        }

        return (in JsonElement data, JsonWorkspace ws) =>
        {
            // Evaluate all args first (also validates they're objects)
            JsonElement[] vals = ArrayPool<JsonElement>.Shared.Rent(evalArgs.Length);
            try
            {
                int estimatedCount = 0;
                for (int i = 0; i < evalArgs.Length; i++)
                {
                    vals[i] = evalArgs[i](data, ws);
                    RequireObject("merge", vals[i]);
                    estimatedCount += vals[i].GetPropertyCount();
                }

                JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
                    ws,
                    (vals, evalArgs.Length),
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
            finally
            {
                vals.AsSpan(0, evalArgs.Length).Clear();
                ArrayPool<JsonElement>.Shared.Return(vals);
            }
        };
    }

    private static JMESPathEval CompileSort(JMESPathNode[] args)
    {
        ValidateArity("sort", args, 1);
        JMESPathEval evalArg = CompileNode(args[0]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement val = evalArg(data, ws);
            RequireArray("sort", val);
            int len = val.GetArrayLength();
            if (len == 0)
            {
                return EmptyArrayElement;
            }

            // Rent array from pool
            JsonElement[] elements = ArrayPool<JsonElement>.Shared.Rent(len);
            try
            {
                int idx = 0;
                foreach (JsonElement item in val.EnumerateArray())
                {
                    elements[idx++] = item;
                }

                JsonValueKind kind = elements[0].ValueKind;
                if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
                {
                    throw new JMESPathException("invalid-type: sort() expects an array of numbers or strings.");
                }

                // Verify homogeneous types
                for (int i = 1; i < len; i++)
                {
                    if (elements[i].ValueKind != kind)
                    {
                        throw new JMESPathException("invalid-type: sort() requires all elements to be the same type.");
                    }
                }

                // Stable sort: use index tiebreaker
                StableSort(elements.AsSpan(0, len), kind);

                JMESPathSequenceBuilder builder = default;
                try
                {
                    for (int i = 0; i < len; i++)
                    {
                        builder.Add(elements[i]);
                    }

                    return builder.ToElement(ws);
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
        };
    }

    private static JMESPathEval CompileNotNull(JMESPathNode[] args)
    {
        ValidateMinArity("not_null", args, 1);
        JMESPathEval[] evalArgs = new JMESPathEval[args.Length];
        for (int i = 0; i < args.Length; i++)
        {
            evalArgs[i] = CompileNode(args[i]);
        }

        return (in JsonElement data, JsonWorkspace ws) =>
        {
            foreach (JMESPathEval evalArg in evalArgs)
            {
                JsonElement val = evalArg(data, ws);
                if (!val.IsNullOrUndefined())
                {
                    return val;
                }
            }

            return NullElement;
        };
    }

    // ─── EXPRESSION ARGUMENT FUNCTIONS ──────────────────────────────
    private static JMESPathEval CompileMap(JMESPathNode[] args)
    {
        ValidateArity("map", args, 2);
        JMESPathEval exprFn = CompileExpressionArg("map", args[0]);
        JMESPathEval evalArr = CompileNode(args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("map", arr);

            JMESPathSequenceBuilder builder = default;
            try
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    JsonElement mapped = exprFn(item, ws);
                    builder.Add(mapped.IsNullOrUndefined() ? NullElement : mapped);
                }

                return builder.ToElement(ws);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    private static JMESPathEval CompileSortBy(JMESPathNode[] args)
    {
        ValidateArity("sort_by", args, 2);
        JMESPathEval evalArr = CompileNode(args[0]);
        JMESPathEval exprFn = CompileExpressionArg("sort_by", args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("sort_by", arr);
            int len = arr.GetArrayLength();
            if (len == 0)
            {
                return EmptyArrayElement;
            }

            // Rent array for (index, element, key) triples
            var pairs = ArrayPool<(int Index, JsonElement Element, JsonElement Key)>.Shared.Rent(len);
            try
            {
                int idx = 0;
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    JsonElement key = exprFn(item, ws);
                    pairs[idx] = (idx, item, key);
                    idx++;
                }

                // Verify homogeneous key types
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

                StableSortBy(pairs, len, keyKind);

                JMESPathSequenceBuilder builder = default;
                try
                {
                    for (int i = 0; i < len; i++)
                    {
                        builder.Add(pairs[i].Element);
                    }

                    return builder.ToElement(ws);
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
        };
    }

    private static JMESPathEval CompileMaxBy(JMESPathNode[] args)
    {
        ValidateArity("max_by", args, 2);
        JMESPathEval evalArr = CompileNode(args[0]);
        JMESPathEval exprFn = CompileExpressionArg("max_by", args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("max_by", arr);
            return FindExtremumBy(arr, exprFn, ws, isMax: true);
        };
    }

    private static JMESPathEval CompileMinBy(JMESPathNode[] args)
    {
        ValidateArity("min_by", args, 2);
        JMESPathEval evalArr = CompileNode(args[0]);
        JMESPathEval exprFn = CompileExpressionArg("min_by", args[1]);
        return (in JsonElement data, JsonWorkspace ws) =>
        {
            JsonElement arr = evalArr(data, ws);
            RequireArray("min_by", arr);
            return FindExtremumBy(arr, exprFn, ws, isMax: false);
        };
    }

    // ─── VALIDATION HELPERS ─────────────────────────────────────────
    private static void ValidateArity(string name, JMESPathNode[] args, int expected)
    {
        if (args.Length != expected)
        {
            throw new JMESPathException($"invalid-arity: {name}() takes exactly {expected} argument(s), got {args.Length}.");
        }
    }

    private static void ValidateMinArity(string name, JMESPathNode[] args, int min)
    {
        if (args.Length < min)
        {
            throw new JMESPathException($"invalid-arity: {name}() takes at least {min} argument(s), got {args.Length}.");
        }
    }

    private static JMESPathEval CompileExpressionArg(string funcName, JMESPathNode arg)
    {
        if (arg is ExpressionRefNode exprRef)
        {
            return CompileNode(exprRef.Expression);
        }

        throw new JMESPathException($"invalid-type: {funcName}() expects an expression argument (&expr).");
    }

    private static void RequireNumber(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.Number)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a number argument.");
        }
    }

    private static void RequireString(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.String)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects a string argument.");
        }
    }

    private static void RequireArray(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.Array)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an array argument.");
        }
    }

    private static void RequireObject(string funcName, in JsonElement val)
    {
        if (val.ValueKind != JsonValueKind.Object)
        {
            throw new JMESPathException($"invalid-type: {funcName}() expects an object argument.");
        }
    }

    // ─── ELEMENT CREATION HELPERS ───────────────────────────────────
    private static JsonElement DoubleToElement(double value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[32];
        if (Utf8Formatter.TryFormat(value, buffer, out int bytesWritten))
        {
            return JsonElement.ParseValue(buffer.Slice(0, bytesWritten));
        }

        return ZeroElement;
    }

    private static JsonElement BoolElement(bool value) => value ? TrueElement : FalseElement;

    /// <summary>
    /// Creates a string JsonElement from unescaped UTF-8 bytes (e.g., from a property name).
    /// </summary>
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

    /// <summary>
    /// Comparer for JsonElement strings using their unescaped UTF-8 byte content.
    /// </summary>
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

    /// <summary>
    /// Writes JSON-escaped UTF-8 bytes from unescaped UTF-8 source.
    /// </summary>
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

    // ─── COMPARISON HELPERS ─────────────────────────────────────────
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
        JMESPathEval exprFn,
        JsonWorkspace ws,
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
        JsonElement bestKey = exprFn(bestElement, ws);
        JsonValueKind keyKind = bestKey.ValueKind;

        if (keyKind != JsonValueKind.Number && keyKind != JsonValueKind.String)
        {
            throw new JMESPathException("invalid-type: max_by()/min_by() expression must return numbers or strings.");
        }

        while (enumerator.MoveNext())
        {
            JsonElement item = enumerator.Current;
            JsonElement key = exprFn(item, ws);
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

    /// <summary>
    /// Performs a stable sort of <see cref="JsonElement"/> values, using an
    /// index-based tiebreaker to preserve original order for equal keys.
    /// </summary>
    private static void StableSort(Span<JsonElement> elements, JsonValueKind kind)
    {
        int len = elements.Length;

        // Build (index, element) pairs for stable tiebreaker
        (int Index, JsonElement Element)[] pairs = ArrayPool<(int, JsonElement)>.Shared.Rent(len);
        try
        {
            for (int i = 0; i < len; i++)
            {
                pairs[i] = (i, elements[i]);
            }

            if (kind == JsonValueKind.Number)
            {
                Array.Sort(pairs, 0, len, StableNumberComparer.Instance);
            }
            else
            {
                Array.Sort(pairs, 0, len, StableStringComparer.Instance);
            }

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

    /// <summary>
    /// Performs a stable sort of keyed pairs, using an index-based tiebreaker.
    /// </summary>
    private static void StableSortBy(
        (int Index, JsonElement Element, JsonElement Key)[] pairs,
        int len,
        JsonValueKind keyKind)
    {
        if (keyKind == JsonValueKind.Number)
        {
            Array.Sort(pairs, 0, len, StableKeyedNumberComparer.Instance);
        }
        else
        {
            Array.Sort(pairs, 0, len, StableKeyedStringComparer.Instance);
        }
    }

    private sealed class StableNumberComparer : IComparer<(int Index, JsonElement Element)>
    {
        public static readonly StableNumberComparer Instance = new();

        public int Compare((int Index, JsonElement Element) a, (int Index, JsonElement Element) b)
        {
            int cmp = a.Element.GetDouble().CompareTo(b.Element.GetDouble());
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        }
    }

    private sealed class StableStringComparer : IComparer<(int Index, JsonElement Element)>
    {
        public static readonly StableStringComparer Instance = new();

        public int Compare((int Index, JsonElement Element) a, (int Index, JsonElement Element) b)
        {
            using UnescapedUtf8JsonString aStr = a.Element.GetUtf8String();
            using UnescapedUtf8JsonString bStr = b.Element.GetUtf8String();
            int cmp = aStr.Span.SequenceCompareTo(bStr.Span);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        }
    }

    private sealed class StableKeyedNumberComparer : IComparer<(int Index, JsonElement Element, JsonElement Key)>
    {
        public static readonly StableKeyedNumberComparer Instance = new();

        public int Compare((int Index, JsonElement Element, JsonElement Key) a, (int Index, JsonElement Element, JsonElement Key) b)
        {
            int cmp = a.Key.GetDouble().CompareTo(b.Key.GetDouble());
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        }
    }

    private sealed class StableKeyedStringComparer : IComparer<(int Index, JsonElement Element, JsonElement Key)>
    {
        public static readonly StableKeyedStringComparer Instance = new();

        public int Compare((int Index, JsonElement Element, JsonElement Key) a, (int Index, JsonElement Element, JsonElement Key) b)
        {
            using UnescapedUtf8JsonString aStr = a.Key.GetUtf8String();
            using UnescapedUtf8JsonString bStr = b.Key.GetUtf8String();
            int cmp = aStr.Span.SequenceCompareTo(bStr.Span);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        }
    }
}
