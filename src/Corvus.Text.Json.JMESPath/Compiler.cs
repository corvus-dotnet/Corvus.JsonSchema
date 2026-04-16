// <copyright file="Compiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
        JsonElement trueElem = JsonElement.ParseValue("true"u8);
        JsonElement falseElem = JsonElement.ParseValue("false"u8);
        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement val = expr(data, workspace);
            return IsTruthy(val) ? falseElem : trueElem;
        };
    }

    private static JMESPathEval CompileComparison(ComparisonNode node)
    {
        JMESPathEval left = CompileNode(node.Left);
        JMESPathEval right = CompileNode(node.Right);
        CompareOp op = node.Operator;
        JsonElement trueElem = JsonElement.ParseValue("true"u8);
        JsonElement falseElem = JsonElement.ParseValue("false"u8);

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            JsonElement lhs = left(data, workspace);
            JsonElement rhs = right(data, workspace);

            if (op == CompareOp.Equal)
            {
                return DeepEquals(lhs, rhs) ? trueElem : falseElem;
            }

            if (op == CompareOp.NotEqual)
            {
                return !DeepEquals(lhs, rhs) ? trueElem : falseElem;
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

            return result ? trueElem : falseElem;
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

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, exprs.Length);
            JsonElement.Mutable root = doc.RootElement;

            for (int i = 0; i < exprs.Length; i++)
            {
                JsonElement val = exprs[i](data, workspace);
                root.AddItem(val.IsNullOrUndefined() ? NullElement : val);
            }

            return (JsonElement)root;
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

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateObjectBuilder(workspace, pairs.Length);
            JsonElement.Mutable root = doc.RootElement;

            for (int i = 0; i < pairs.Length; i++)
            {
                JsonElement val = pairs[i].value(data, workspace);
                root.SetProperty(pairs[i].key, val.IsNullOrUndefined() ? NullElement : val);
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileListProjection(ListProjectionNode node)
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

            int len = arr.GetArrayLength();
            if (len == 0)
            {
                return arr;
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                JsonElement projected = right(item, workspace);
                if (!projected.IsNullOrUndefined())
                {
                    root.AddItem(projected);
                }
            }

            return (JsonElement)root;
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

            int count = obj.GetPropertyCount();
            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, count);
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonProperty<JsonElement> prop in obj.EnumerateObject())
            {
                JsonElement projected = right(prop.Value, workspace);
                if (!projected.IsNullOrUndefined())
                {
                    root.AddItem(projected);
                }
            }

            return (JsonElement)root;
        };
    }

    private static JMESPathEval CompileFlattenProjection(FlattenProjectionNode node)
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

            // First flatten: merge nested arrays
            JsonDocumentBuilder<JsonElement.Mutable> flatDoc =
                JsonElement.CreateArrayBuilder(workspace, arr.GetArrayLength());
            JsonElement.Mutable flatRoot = flatDoc.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement nested in item.EnumerateArray())
                    {
                        flatRoot.AddItem(nested);
                    }
                }
                else
                {
                    flatRoot.AddItem(item);
                }
            }

            // Then project
            JsonElement flattened = (JsonElement)flatRoot;
            int len = flattened.GetArrayLength();
            if (len == 0)
            {
                return flattened;
            }

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in flattened.EnumerateArray())
            {
                JsonElement projected = right(item, workspace);
                if (!projected.IsNullOrUndefined())
                {
                    root.AddItem(projected);
                }
            }

            return (JsonElement)root;
        };
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

            int len = arr.GetArrayLength();
            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;

            foreach (JsonElement item in arr.EnumerateArray())
            {
                JsonElement condResult = condition(item, workspace);
                if (IsTruthy(condResult))
                {
                    JsonElement projected = right(item, workspace);
                    if (!projected.IsNullOrUndefined())
                    {
                        root.AddItem(projected);
                    }
                }
            }

            return (JsonElement)root;
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

            JsonDocumentBuilder<JsonElement.Mutable> doc =
                JsonElement.CreateArrayBuilder(workspace, len);
            JsonElement.Mutable root = doc.RootElement;

            if (actualStep > 0)
            {
                for (int i = actualStart; i < actualStop; i += actualStep)
                {
                    root.AddItem(data[i]);
                }
            }
            else
            {
                for (int i = actualStart; i > actualStop; i += actualStep)
                {
                    root.AddItem(data[i]);
                }
            }

            return (JsonElement)root;
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

        return value.ValueKind switch
        {
            JsonValueKind.False => false,
            JsonValueKind.True => true,
            JsonValueKind.Null => false,
            JsonValueKind.Number => true,
            JsonValueKind.String => value.GetString()?.Length > 0,
            JsonValueKind.Array => value.GetArrayLength() > 0,
            JsonValueKind.Object => value.GetPropertyCount() > 0,
            _ => false,
        };
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

        return left.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null => true,
            JsonValueKind.Number => left.GetDouble() == right.GetDouble(),
            JsonValueKind.String => string.Equals(left.GetString(), right.GetString(), StringComparison.Ordinal),
            JsonValueKind.Array => ArrayEquals(left, right),
            JsonValueKind.Object => ObjectEquals(left, right),
            _ => false,
        };
    }

    private static bool ArrayEquals(in JsonElement left, in JsonElement right)
    {
        int len = left.GetArrayLength();
        if (len != right.GetArrayLength())
        {
            return false;
        }

        for (int i = 0; i < len; i++)
        {
            if (!DeepEquals(left[i], right[i]))
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
            if (!right.TryGetProperty(prop.Name, out JsonElement rightVal)
                || !DeepEquals(prop.Value, rightVal))
            {
                return false;
            }
        }

        return true;
    }
}
