// <copyright file="Compiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Compiles JMESPath expression strings into delegate trees for efficient evaluation.
/// </summary>
internal static partial class Compiler
{
    private static readonly JsonElement NullElement = JsonElement.ParseValue("null"u8);

    /// <summary>
    /// Compiles a JMESPath expression string into an evaluation delegate.
    /// </summary>
    /// <param name="expression">The JMESPath expression.</param>
    /// <returns>A compiled evaluation delegate.</returns>
    public static JMESPathEval Compile(string expression)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(expression);
        return Compile(utf8);
    }

    /// <summary>
    /// Compiles a UTF-8 JMESPath expression into an evaluation delegate.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 encoded JMESPath expression.</param>
    /// <returns>A compiled evaluation delegate.</returns>
    public static JMESPathEval Compile(byte[] utf8Expression)
    {
        JMESPathNode ast = Parser.Parse(utf8Expression);
        return CompileNode(ast);
    }

    private static JMESPathEval CompileNode(JMESPathNode node)
    {
        return node switch
        {
            IdentifierNode id => CompileIdentifier(id),
            CurrentNode => static (in JsonElement data, JsonWorkspace _) => data,
            RawStringNode raw => CompileRawString(raw),
            LiteralNode lit => CompileLiteral(lit),
            SubExpressionNode sub => CompileSubExpression(sub),
            IndexNode idx => CompileIndex(idx),
            PipeNode pipe => CompilePipe(pipe),
            OrNode or => CompileOr(or),
            AndNode and => CompileAnd(and),
            NotNode not => CompileNot(not),
            ComparisonNode cmp => CompileComparison(cmp),
            MultiSelectListNode msl => CompileMultiSelectList(msl),
            MultiSelectHashNode msh => CompileMultiSelectHash(msh),
            ListProjectionNode lp => CompileListProjection(lp),
            ValueProjectionNode vp => CompileValueProjection(vp),
            FlattenProjectionNode fp => CompileFlattenProjection(fp),
            FilterProjectionNode filt => CompileFilterProjection(filt),
            SliceNode sl => CompileSlice(sl),
            FunctionCallNode fn => CompileFunctionCall(fn),
            _ => throw new JMESPathException($"Unsupported AST node type: {node.GetType().Name}."),
        };
    }

    private static JMESPathEval CompileIdentifier(IdentifierNode node)
    {
        byte[] name = node.Name;
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(name, out JsonElement result))
            {
                return result;
            }

            return default;
        };
    }

    private static JMESPathEval CompileRawString(RawStringNode node)
    {
        // JSON-escape the raw string value and wrap in double quotes.
        // Raw strings may contain literal backslashes that need JSON escaping.
        string rawValue = Encoding.UTF8.GetString(node.Value);
        StringBuilder sb = new(rawValue.Length + 8);
        sb.Append('"');
        foreach (char ch in rawValue)
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
        JsonElement element = JsonElement.ParseValue(Encoding.UTF8.GetBytes(sb.ToString()));
        return (in JsonElement data, JsonWorkspace workspace) => element;
    }

    private static JMESPathEval CompileLiteral(LiteralNode node)
    {
        JsonElement element = JsonElement.ParseValue(node.JsonValue);
        return (in JsonElement data, JsonWorkspace workspace) => element;
    }

    private static JMESPathEval CompileSubExpression(SubExpressionNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement lhs = left(data, workspace);
            if (lhs.IsNullOrUndefined())
            {
                return default;
            }

            return right(lhs, workspace);
        };
    }

    private static JMESPathEval CompileIndex(IndexNode node)
    {
        int index = node.Index;
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            if (data.ValueKind != JsonValueKind.Array)
            {
                return default;
            }

            int len = data.GetArrayLength();
            int actual = index < 0 ? len + index : index;
            if (actual < 0 || actual >= len)
            {
                return default;
            }

            return data[actual];
        };
    }

    private static JMESPathEval CompilePipe(PipeNode node)
    {
        // Fused path: ListProjection | sort(@) — collect projected elements directly
        // into a SequenceBuilder, sort in-place, materialize once (one doc instead of two).
        if (node.Right is FunctionCallNode { Arguments: [CurrentNode] } sortCall
            && sortCall.Name.AsSpan().SequenceEqual("sort"u8)
            && node.Left is ListProjectionNode listProj)
        {
            JMESPathEval outerLeft = CompileNode(listProj.Left);
            JMESPathEval outerRight = CompileNode(listProj.Right);

            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                JsonElement outerArr = outerLeft(data, workspace);
                if (outerArr.ValueKind != JsonValueKind.Array)
                {
                    return default;
                }

                JMESPathSequenceBuilder builder = default;
                try
                {
                    foreach (JsonElement outerItem in outerArr.EnumerateArray())
                    {
                        JsonElement projected = outerRight(outerItem, workspace);
                        if (!projected.IsNullOrUndefined())
                        {
                            builder.Add(projected);
                        }
                    }

                    int len = builder.Count;
                    if (len == 0)
                    {
                        return JMESPathCodeGenHelpers.EmptyArrayElement;
                    }

                    JsonValueKind kind = builder[0].ValueKind;
                    if (kind != JsonValueKind.Number && kind != JsonValueKind.String)
                    {
                        throw new JMESPathException("invalid-type: sort() expects an array of numbers or strings.");
                    }

                    for (int i = 1; i < len; i++)
                    {
                        if (builder[i].ValueKind != kind)
                        {
                            throw new JMESPathException("invalid-type: sort() requires all elements to be the same type.");
                        }
                    }

                    JMESPathCodeGenHelpers.InsertionSortByKind(ref builder, len, kind);

                    return builder.ToElement(workspace);
                }
                finally
                {
                    builder.ReturnArray();
                }
            };
        }

        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement lhs = left(data, workspace);
            return right(lhs, workspace);
        };
    }

    private static JMESPathEval CompileOr(OrNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement lhs = left(data, workspace);
            if (IsTruthy(lhs))
            {
                return lhs;
            }

            return right(data, workspace);
        };
    }

    private static JMESPathEval CompileAnd(AndNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement lhs = left(data, workspace);
            if (!IsTruthy(lhs))
            {
                return lhs;
            }

            return right(data, workspace);
        };
    }

    private static JMESPathEval CompileNot(NotNode node)
    {
        JMESPathEval expr = CompileNode(node.Expression);
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement val = expr(data, workspace);
            return IsTruthy(val) ? FalseElement : TrueElement;
        };
    }

    private static JMESPathEval CompileComparison(ComparisonNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);
        CompareOp op = node.Operator;

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement lhs = left(data, workspace);
            JsonElement rhs = right(data, workspace);

            if (op == CompareOp.Equal)
            {
                return DeepEquals(lhs, rhs) ? TrueElement : FalseElement;
            }

            if (op == CompareOp.NotEqual)
            {
                return !DeepEquals(lhs, rhs) ? TrueElement : FalseElement;
            }

            // Ordering comparisons require both operands to be numbers
            if (lhs.ValueKind != JsonValueKind.Number || rhs.ValueKind != JsonValueKind.Number)
            {
                return default;
            }

            double ld = lhs.GetDouble();
            double rd = rhs.GetDouble();

            bool result = op switch
            {
                CompareOp.LessThan => ld < rd,
                CompareOp.LessThanOrEqual => ld <= rd,
                CompareOp.GreaterThan => ld > rd,
                CompareOp.GreaterThanOrEqual => ld >= rd,
                _ => false,
            };

            return result ? TrueElement : FalseElement;
        };
    }

    private static JMESPathEval CompileMultiSelectList(MultiSelectListNode node)
    {
        JMESPathEval[] exprs = new JMESPathEval[node.Expressions.Length];
        for (int i = 0; i < node.Expressions.Length; i++)
        {
            exprs[i] = CompileNode(node.Expressions[i]);
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            if (data.IsNullOrUndefined())
            {
                return default;
            }

            JMESPathSequenceBuilder builder = default;
            try
            {
                for (int i = 0; i < exprs.Length; i++)
                {
                    JsonElement val = exprs[i](data, workspace);
                    builder.Add(val.IsNullOrUndefined() ? NullElement : val);
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    private static JMESPathEval CompileMultiSelectHash(MultiSelectHashNode node)
    {
        (byte[] key, JMESPathEval value)[] pairs =
            new (byte[], JMESPathEval)[node.Pairs.Length];
        for (int i = 0; i < node.Pairs.Length; i++)
        {
            pairs[i] = (node.Pairs[i].Key, CompileNode(node.Pairs[i].Value));
        }

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            if (data.IsNullOrUndefined())
            {
                return default;
            }

            // Evaluate all values first
            JsonElement[] vals = ArrayPool<JsonElement>.Shared.Rent(pairs.Length);
            try
            {
                for (int i = 0; i < pairs.Length; i++)
                {
                    JsonElement val = pairs[i].value(data, workspace);
                    vals[i] = val.IsNullOrUndefined() ? NullElement : val;
                }

                JsonDocumentBuilder<JsonElement.Mutable> doc =
                    JsonElement.CreateBuilder(
                        workspace,
                        (pairs, vals, pairs.Length),
                        static (in ((byte[] key, JMESPathEval value)[] Pairs, JsonElement[] Vals, int Count) ctx, ref JsonElement.ObjectBuilder builder) =>
                        {
                            for (int i = 0; i < ctx.Count; i++)
                            {
                                builder.AddProperty(ctx.Pairs[i].key, ctx.Vals[i]);
                            }
                        },
                        estimatedMemberCount: pairs.Length);

                return (JsonElement)doc.RootElement;
            }
            finally
            {
                vals.AsSpan(0, pairs.Length).Clear();
                ArrayPool<JsonElement>.Shared.Return(vals);
            }
        };
    }

    private static JMESPathEval CompileListProjection(ListProjectionNode node)
    {
        JMESPathEval left = CompileNode(node.Left);

        // Identity projection: right side is just @, so the result is the left array
        // with null elements filtered out. For non-sparse arrays this is a no-op.
        if (node.Right is CurrentNode)
        {
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                JsonElement arr = left(data, workspace);
                if (arr.ValueKind != JsonValueKind.Array)
                {
                    return default;
                }

                return arr;
            };
        }

        JMESPathEval right = CompileNode(node.Right);

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = left(data, workspace);
            if (arr.ValueKind != JsonValueKind.Array)
            {
                return default;
            }

            int len = arr.GetArrayLength();
            if (len == 0)
            {
                return arr;
            }

            JMESPathSequenceBuilder builder = default;
            try
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    JsonElement projected = right(item, workspace);
                    if (!projected.IsNullOrUndefined())
                    {
                        builder.Add(projected);
                    }
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    private static JMESPathEval CompileValueProjection(ValueProjectionNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement obj = left(data, workspace);
            if (obj.ValueKind != JsonValueKind.Object)
            {
                return default;
            }

            JMESPathSequenceBuilder builder = default;
            try
            {
                foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
                {
                    JsonElement projected = right(prop.Value, workspace);
                    if (!projected.IsNullOrUndefined())
                    {
                        builder.Add(projected);
                    }
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    private static JMESPathEval CompileFlattenProjection(FlattenProjectionNode node)
    {
        // Identity projection: right side is @, so just flatten without projecting.
        if (node.Right is CurrentNode)
        {
            // Fused: left is a ListProjection whose results we flatten directly.
            if (node.Left is ListProjectionNode outerProj)
            {
                JMESPathEval outerLeft = CompileNode(outerProj.Left);
                JMESPathEval outerRight = CompileNode(outerProj.Right);

                return (in JsonElement data, JsonWorkspace workspace) =>
                {
                    JsonElement outerArr = outerLeft(data, workspace);
                    if (outerArr.ValueKind != JsonValueKind.Array)
                    {
                        return default;
                    }

                    JMESPathSequenceBuilder builder = default;
                    try
                    {
                        foreach (JsonElement outerItem in outerArr.EnumerateArray())
                        {
                            JsonElement projected = outerRight(outerItem, workspace);
                            if (projected.IsNullOrUndefined())
                            {
                                continue;
                            }

                            if (projected.ValueKind == JsonValueKind.Array)
                            {
                                foreach (JsonElement nested in projected.EnumerateArray())
                                {
                                    builder.Add(nested);
                                }
                            }
                            else
                            {
                                builder.Add(projected);
                            }
                        }

                        return builder.ToElement(workspace);
                    }
                    finally
                    {
                        builder.ReturnArray();
                    }
                };
            }

            JMESPathEval left = CompileNode(node.Left);

            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                JsonElement arr = left(data, workspace);
                if (arr.ValueKind != JsonValueKind.Array)
                {
                    return default;
                }

                JMESPathSequenceBuilder builder = default;
                try
                {
                    foreach (JsonElement item in arr.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement nested in item.EnumerateArray())
                            {
                                builder.Add(nested);
                            }
                        }
                        else
                        {
                            builder.Add(item);
                        }
                    }

                    return builder.ToElement(workspace);
                }
                finally
                {
                    builder.ReturnArray();
                }
            };
        }

        // Fused: left is a ListProjection — iterate outer, project, flatten+project in one pass.
        if (node.Left is ListProjectionNode outerProj2)
        {
            JMESPathEval outerLeft = CompileNode(outerProj2.Left);
            JMESPathEval outerRight = CompileNode(outerProj2.Right);
            JMESPathEval right = CompileNode(node.Right);

            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                JsonElement outerArr = outerLeft(data, workspace);
                if (outerArr.ValueKind != JsonValueKind.Array)
                {
                    return default;
                }

                JMESPathSequenceBuilder builder = default;
                try
                {
                    foreach (JsonElement outerItem in outerArr.EnumerateArray())
                    {
                        JsonElement innerResult = outerRight(outerItem, workspace);
                        if (innerResult.IsNullOrUndefined())
                        {
                            continue;
                        }

                        if (innerResult.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement nested in innerResult.EnumerateArray())
                            {
                                JsonElement projected = right(nested, workspace);
                                if (!projected.IsNullOrUndefined())
                                {
                                    builder.Add(projected);
                                }
                            }
                        }
                        else
                        {
                            JsonElement projected = right(innerResult, workspace);
                            if (!projected.IsNullOrUndefined())
                            {
                                builder.Add(projected);
                            }
                        }
                    }

                    return builder.ToElement(workspace);
                }
                finally
                {
                    builder.ReturnArray();
                }
            };
        }

        {
            JMESPathEval left = CompileNode(node.Left);
            JMESPathEval right = CompileNode(node.Right);

            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                JsonElement arr = left(data, workspace);
                if (arr.ValueKind != JsonValueKind.Array)
                {
                    return default;
                }

                // Single-pass: flatten and project in one loop.
                JMESPathSequenceBuilder builder = default;
                try
                {
                    foreach (JsonElement item in arr.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement nested in item.EnumerateArray())
                            {
                                JsonElement projected = right(nested, workspace);
                                if (!projected.IsNullOrUndefined())
                                {
                                    builder.Add(projected);
                                }
                            }
                        }
                        else
                        {
                            JsonElement projected = right(item, workspace);
                            if (!projected.IsNullOrUndefined())
                            {
                                builder.Add(projected);
                            }
                        }
                    }

                    return builder.ToElement(workspace);
                }
                finally
                {
                    builder.ReturnArray();
                }
            };
        }
    }

    private static JMESPathEval CompileFilterProjection(FilterProjectionNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval condition = CompileNode(node.Condition);
        JMESPathEval right = CompileNode(node.Right);

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement arr = left(data, workspace);
            if (arr.ValueKind != JsonValueKind.Array)
            {
                return default;
            }

            JMESPathSequenceBuilder builder = default;
            try
            {
                foreach (JsonElement item in arr.EnumerateArray())
                {
                    JsonElement condResult = condition(item, workspace);
                    if (IsTruthy(condResult))
                    {
                        JsonElement projected = right(item, workspace);
                        if (!projected.IsNullOrUndefined())
                        {
                            builder.Add(projected);
                        }
                    }
                }

                return builder.ToElement(workspace);
            }
            finally
            {
                builder.ReturnArray();
            }
        };
    }

    private static JMESPathEval CompileSlice(SliceNode node)
    {
        int? start = node.Start;
        int? stop = node.Stop;
        int? step = node.Step;

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            if (data.ValueKind != JsonValueKind.Array)
            {
                return default;
            }

            int len = data.GetArrayLength();
            int actualStep = step ?? 1;

            if (actualStep == 0)
            {
                throw new JMESPathException("Slice step cannot be 0.");
            }

            int actualStart;
            int actualStop;

            if (actualStep > 0)
            {
                actualStart = start.HasValue ? NormalizeSliceIndex(start.Value, len, isStart: true, positiveStep: true) : 0;
                actualStop = stop.HasValue ? NormalizeSliceIndex(stop.Value, len, isStart: false, positiveStep: true) : len;
            }
            else
            {
                actualStart = start.HasValue ? NormalizeSliceIndex(start.Value, len, isStart: true, positiveStep: false) : len - 1;
                actualStop = stop.HasValue ? NormalizeSliceIndex(stop.Value, len, isStart: false, positiveStep: false) : -1;
            }

            // Copy elements via enumerator (O(n)) to avoid O(n²) indexed access
            JsonElement[] temp = ArrayPool<JsonElement>.Shared.Rent(len);
            try
            {
                int idx = 0;
                foreach (JsonElement item in data.EnumerateArray())
                {
                    temp[idx++] = item;
                }

                JMESPathSequenceBuilder builder = default;
                try
                {
                    if (actualStep > 0)
                    {
                        for (int i = actualStart; i < actualStop; i += actualStep)
                        {
                            builder.Add(temp[i]);
                        }
                    }
                    else
                    {
                        for (int i = actualStart; i > actualStop; i += actualStep)
                        {
                            builder.Add(temp[i]);
                        }
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
        };
    }

    private static JMESPathEval CompileFunctionCall(FunctionCallNode node)
    {
        string name = Encoding.UTF8.GetString(node.Name);
        JMESPathNode[] args = node.Arguments;

        return name switch
        {
            "abs" => CompileAbs(args),
            "avg" => CompileAvg(args),
            "ceil" => CompileCeil(args),
            "contains" => CompileContains(args),
            "ends_with" => CompileEndsWith(args),
            "floor" => CompileFloor(args),
            "join" => CompileJoin(args),
            "keys" => CompileKeys(args),
            "length" => CompileLength(args),
            "map" => CompileMap(args),
            "max" => CompileMax(args),
            "max_by" => CompileMaxBy(args),
            "merge" => CompileMerge(args),
            "min" => CompileMin(args),
            "min_by" => CompileMinBy(args),
            "not_null" => CompileNotNull(args),
            "reverse" => CompileReverse(args),
            "sort" => CompileSort(args),
            "sort_by" => CompileSortBy(args),
            "starts_with" => CompileStartsWith(args),
            "sum" => CompileSum(args),
            "to_array" => CompileToArray(args),
            "to_number" => CompileToNumber(args),
            "to_string" => CompileToString(args),
            "type" => CompileType(args),
            "values" => CompileValues(args),
            _ => throw new JMESPathException($"Unknown function: {name}()"),
        };
    }

    /// <summary>
    /// Normalizes a slice index per JMESPath spec.
    /// For negative indices: adds length, then clamps to 0 (positive step) or -1 (negative step stop).
    /// For positive indices: clamps to length (positive step) or length-1 (negative step start).
    /// </summary>
    private static int NormalizeSliceIndex(int index, int length, bool isStart, bool positiveStep)
    {
        if (index < 0)
        {
            index += length;
            if (index < 0)
            {
                // For positive step: clamp to 0 (valid array start)
                // For negative step: clamp to -1 (sentinel that stops the descending loop)
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
                // For negative step: start clamps to length-1, stop clamps to length
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

    private static bool IsTruthy(in JsonElement value)
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
                    using UnescapedUtf8JsonString utf8 = value.GetUtf8String();
                    return utf8.Span.Length > 0;
                }

            case JsonValueKind.Array:
                return value.GetArrayLength() > 0;
            case JsonValueKind.Object:
                return value.GetPropertyCount() > 0;
            default:
                return false;
        }
    }

    private static bool DeepEquals(in JsonElement left, in JsonElement right)
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
}
