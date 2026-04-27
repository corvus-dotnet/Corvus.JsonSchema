// <copyright file="Compiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Compiles JSONPath expression strings into delegate trees for efficient evaluation.
/// </summary>
internal static partial class Compiler
{
    /// <summary>
    /// Compiles a JSONPath expression string into an evaluation delegate.
    /// </summary>
    /// <param name="expression">The JSONPath expression.</param>
    /// <returns>A compiled evaluation delegate.</returns>
    public static JsonPathEval Compile(string expression)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(expression);
        return Compile(utf8);
    }

    /// <summary>
    /// Compiles a UTF-8 JSONPath expression into an evaluation delegate.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 encoded JSONPath expression.</param>
    /// <returns>A compiled evaluation delegate.</returns>
    public static JsonPathEval Compile(byte[] utf8Expression)
    {
        QueryNode ast = Parser.Parse(utf8Expression);
        return CompileQuery(ast);
    }

    private static JsonPathEval CompileQuery(QueryNode query)
    {
        if (query.Segments.Length == 0)
        {
            return static (in JsonElement root, JsonWorkspace workspace) =>
            {
                return BuildSingletonArray(root, workspace);
            };
        }

        SegmentEval[] pipeline = new SegmentEval[query.Segments.Length];
        for (int i = 0; i < query.Segments.Length; i++)
        {
            pipeline[i] = CompileSegment(query.Segments[i]);
        }

        return (in JsonElement root, JsonWorkspace workspace) =>
        {
            JsonElement current = BuildSingletonArray(root, workspace);

            for (int i = 0; i < pipeline.Length; i++)
            {
                current = pipeline[i](root, current, workspace);
                if (current.IsNullOrUndefined() || current.GetArrayLength() == 0)
                {
                    return JsonPathSequenceBuilder.EmptyArrayElement;
                }
            }

            return current;
        };
    }

    private delegate JsonElement SegmentEval(in JsonElement root, in JsonElement nodeList, JsonWorkspace workspace);

    private static SegmentEval CompileSegment(SegmentNode segment)
    {
        SelectorEval[] selectors = new SelectorEval[segment.Selectors.Length];
        for (int i = 0; i < segment.Selectors.Length; i++)
        {
            selectors[i] = CompileSelector(segment.Selectors[i]);
        }

        if (segment is DescendantSegmentNode)
        {
            return (in JsonElement root, in JsonElement nodeList, JsonWorkspace workspace) =>
            {
                return EvalDescendantSegment(root, nodeList, selectors, workspace);
            };
        }

        return (in JsonElement root, in JsonElement nodeList, JsonWorkspace workspace) =>
        {
            return EvalChildSegment(root, nodeList, selectors, workspace);
        };
    }

    private static JsonElement EvalChildSegment(
        in JsonElement root,
        in JsonElement nodeList,
        SelectorEval[] selectors,
        JsonWorkspace workspace)
    {
        JsonPathSequenceBuilder builder = default;
        try
        {
            foreach (JsonElement node in nodeList.EnumerateArray())
            {
                for (int i = 0; i < selectors.Length; i++)
                {
                    selectors[i](root, node, ref builder, workspace);
                }
            }

            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    private static JsonElement EvalDescendantSegment(
        in JsonElement root,
        in JsonElement nodeList,
        SelectorEval[] selectors,
        JsonWorkspace workspace)
    {
        JsonPathSequenceBuilder builder = default;
        try
        {
            foreach (JsonElement node in nodeList.EnumerateArray())
            {
                VisitDescendants(root, node, selectors, ref builder, workspace);
            }

            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }

    private static void VisitDescendants(
        in JsonElement root,
        in JsonElement node,
        SelectorEval[] selectors,
        ref JsonPathSequenceBuilder resultBuilder,
        JsonWorkspace workspace)
    {
        // Iterative DFS with an explicit stack to avoid stack overflow on deep documents.
        int stackCapacity = 32;
        JsonElement[] rentedStack = ArrayPool<JsonElement>.Shared.Rent(stackCapacity);
        Span<JsonElement> stack = rentedStack;

        try
        {
            int stackSize = 0;
            stack[stackSize++] = node;

            while (stackSize > 0)
            {
                JsonElement current = stack[--stackSize];

                for (int i = 0; i < selectors.Length; i++)
                {
                    selectors[i](root, current, ref resultBuilder, workspace);
                }

                // Push children in reverse order so they are visited in document order
                if (current.ValueKind == JsonValueKind.Object)
                {
                    int childStart = stackSize;
                    foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
                    {
                        if (stackSize >= stackCapacity)
                        {
                            GrowStack(ref stack, ref rentedStack, ref stackCapacity);
                        }

                        stack[stackSize++] = prop.Value;
                    }

                    ReverseSpan(stack.Slice(childStart, stackSize - childStart));
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    int childStart = stackSize;
                    foreach (JsonElement item in current.EnumerateArray())
                    {
                        if (stackSize >= stackCapacity)
                        {
                            GrowStack(ref stack, ref rentedStack, ref stackCapacity);
                        }

                        stack[stackSize++] = item;
                    }

                    ReverseSpan(stack.Slice(childStart, stackSize - childStart));
                }
            }
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(rentedStack);
        }
    }

    private static void GrowStack(ref Span<JsonElement> stack, ref JsonElement[] rentedStack, ref int capacity)
    {
        int newCapacity = capacity * 2;
        JsonElement[] newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        stack.Slice(0, capacity).CopyTo(newArray);

        if (rentedStack != null)
        {
            ArrayPool<JsonElement>.Shared.Return(rentedStack);
        }

        rentedStack = newArray;
        stack = newArray;
        capacity = newCapacity;
    }

    private static void ReverseSpan(Span<JsonElement> span)
    {
        int lo = 0;
        int hi = span.Length - 1;
        while (lo < hi)
        {
            (span[lo], span[hi]) = (span[hi], span[lo]);
            lo++;
            hi--;
        }
    }

    private delegate void SelectorEval(
        in JsonElement root,
        in JsonElement node,
        ref JsonPathSequenceBuilder resultBuilder,
        JsonWorkspace workspace);

    private static SelectorEval CompileSelector(SelectorNode selector)
    {
        return selector switch
        {
            NameSelectorNode name => CompileNameSelector(name),
            WildcardSelectorNode => CompileWildcardSelector(),
            IndexSelectorNode idx => CompileIndexSelector(idx),
            SliceSelectorNode slice => CompileSliceSelector(slice),
            FilterSelectorNode filter => CompileFilterSelector(filter),
            _ => throw new JsonPathException($"Unknown selector type: {selector.GetType().Name}"),
        };
    }

    private static SelectorEval CompileNameSelector(NameSelectorNode name)
    {
        byte[] utf8Name = name.Name;
        return (in JsonElement root, in JsonElement node, ref JsonPathSequenceBuilder resultBuilder, JsonWorkspace workspace) =>
        {
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty(utf8Name, out JsonElement value))
            {
                resultBuilder.Add(value);
            }
        };
    }

    private static SelectorEval CompileWildcardSelector()
    {
        return static (in JsonElement root, in JsonElement node, ref JsonPathSequenceBuilder resultBuilder, JsonWorkspace workspace) =>
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
                {
                    resultBuilder.Add(prop.Value);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    resultBuilder.Add(item);
                }
            }
        };
    }

    private static SelectorEval CompileIndexSelector(IndexSelectorNode idx)
    {
        long index = idx.Index;
        return (in JsonElement root, in JsonElement node, ref JsonPathSequenceBuilder resultBuilder, JsonWorkspace workspace) =>
        {
            if (node.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int len = node.GetArrayLength();
            long resolved = index >= 0 ? index : len + index;
            if (resolved >= 0 && resolved < len)
            {
                resultBuilder.Add(node[(int)resolved]);
            }
        };
    }

    private static SelectorEval CompileSliceSelector(SliceSelectorNode slice)
    {
        long? startVal = slice.Start;
        long? endVal = slice.End;
        long? stepVal = slice.Step;

        return (in JsonElement root, in JsonElement node, ref JsonPathSequenceBuilder resultBuilder, JsonWorkspace workspace) =>
        {
            if (node.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int len = node.GetArrayLength();
            long s = stepVal ?? 1;

            if (s == 0)
            {
                return;
            }

            long lower;
            long upper;

            if (s > 0)
            {
                lower = NormalizeSliceIndex(startVal ?? 0, len);
                upper = NormalizeSliceIndex(endVal ?? len, len);

                lower = Math.Max(lower, 0);
                upper = Math.Min(upper, len);

                for (long i = lower; i < upper; i += s)
                {
                    resultBuilder.Add(node[(int)i]);
                }
            }
            else
            {
                lower = NormalizeSliceIndex(endVal ?? -len - 1, len);
                upper = NormalizeSliceIndex(startVal ?? len - 1, len);

                upper = Math.Min(upper, len - 1);
                lower = Math.Max(lower, -1);

                for (long i = upper; i > lower; i += s)
                {
                    resultBuilder.Add(node[(int)i]);
                }
            }
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long NormalizeSliceIndex(long index, int len)
    {
        return index >= 0 ? index : len + index;
    }

    private static SelectorEval CompileFilterSelector(FilterSelectorNode filter)
    {
        FilterEval compiledFilter = CompileFilterExpression(filter.Expression);

        return (in JsonElement root, in JsonElement node, ref JsonPathSequenceBuilder resultBuilder, JsonWorkspace workspace) =>
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    if (EvalFilterAsTruthy(compiledFilter, root, item, workspace))
                    {
                        resultBuilder.Add(item);
                    }
                }
            }
            else if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
                {
                    if (EvalFilterAsTruthy(compiledFilter, root, prop.Value, workspace))
                    {
                        resultBuilder.Add(prop.Value);
                    }
                }
            }
        };
    }

    private delegate FilterResult FilterEval(in JsonElement root, in JsonElement current, JsonWorkspace workspace);

    private readonly struct FilterResult
    {
        private FilterResult(FilterResultKind kind, bool logical, JsonElement value, JsonElement nodeList)
        {
            Kind = kind;
            Logical = logical;
            Value = value;
            NodeList = nodeList;
        }

        public enum FilterResultKind : byte
        {
            LogicalType,
            ValueType,
            NodesType,
            Nothing,
        }

        public FilterResultKind Kind { get; }

        public bool Logical { get; }

        public JsonElement Value { get; }

        public JsonElement NodeList { get; }

        public static FilterResult FromLogical(bool value) =>
            new(FilterResultKind.LogicalType, value, default, default);

        public static FilterResult FromValue(JsonElement value) =>
            new(FilterResultKind.ValueType, false, value, default);

        public static FilterResult FromNodes(JsonElement nodeList) =>
            new(FilterResultKind.NodesType, false, default, nodeList);

        public static FilterResult NothingResult { get; } =
            new(FilterResultKind.Nothing, false, default, default);

        public bool AsTruthy()
        {
            return Kind switch
            {
                FilterResultKind.LogicalType => Logical,
                FilterResultKind.NodesType => !NodeList.IsNullOrUndefined() && NodeList.GetArrayLength() > 0,
                _ => false,
            };
        }

        public FilterResult AsComparable()
        {
            if (Kind == FilterResultKind.ValueType)
            {
                return this;
            }

            if (Kind == FilterResultKind.NodesType &&
                !NodeList.IsNullOrUndefined() &&
                NodeList.GetArrayLength() == 1)
            {
                JsonElement single = default;
                foreach (JsonElement item in NodeList.EnumerateArray())
                {
                    single = item;
                    break;
                }

                return FromValue(single);
            }

            return NothingResult;
        }
    }

    private static bool EvalFilterAsTruthy(FilterEval filter, in JsonElement root, in JsonElement current, JsonWorkspace workspace)
    {
        FilterResult result = filter(root, current, workspace);
        return result.AsTruthy();
    }

    private static FilterEval CompileFilterExpression(FilterExpressionNode node)
    {
        return node switch
        {
            LogicalAndNode and => CompileLogicalAnd(and),
            LogicalOrNode or => CompileLogicalOr(or),
            LogicalNotNode not => CompileLogicalNot(not),
            ComparisonNode cmp => CompileComparison(cmp),
            FilterQueryNode query => CompileFilterQuery(query),
            FunctionCallNode func => CompileFunctionCall(func),
            LiteralNode lit => CompileLiteral(lit),
            ParenExpressionNode paren => CompileFilterExpression(paren.Inner),
            _ => throw new JsonPathException($"Unknown filter expression type: {node.GetType().Name}"),
        };
    }

    private static FilterEval CompileLogicalAnd(LogicalAndNode and)
    {
        FilterEval left = CompileFilterExpression(and.Left);
        FilterEval right = CompileFilterExpression(and.Right);

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult l = left(root, current, workspace);
            if (!l.AsTruthy())
            {
                return FilterResult.FromLogical(false);
            }

            FilterResult r = right(root, current, workspace);
            return FilterResult.FromLogical(r.AsTruthy());
        };
    }

    private static FilterEval CompileLogicalOr(LogicalOrNode or)
    {
        FilterEval left = CompileFilterExpression(or.Left);
        FilterEval right = CompileFilterExpression(or.Right);

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult l = left(root, current, workspace);
            if (l.AsTruthy())
            {
                return FilterResult.FromLogical(true);
            }

            FilterResult r = right(root, current, workspace);
            return FilterResult.FromLogical(r.AsTruthy());
        };
    }

    private static FilterEval CompileLogicalNot(LogicalNotNode not)
    {
        FilterEval operand = CompileFilterExpression(not.Operand);

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult result = operand(root, current, workspace);
            return FilterResult.FromLogical(!result.AsTruthy());
        };
    }

    private static FilterEval CompileComparison(ComparisonNode cmp)
    {
        FilterEval left = CompileFilterExpression(cmp.Left);
        FilterEval right = CompileFilterExpression(cmp.Right);
        ComparisonOp op = cmp.Op;

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult l = left(root, current, workspace).AsComparable();
            FilterResult r = right(root, current, workspace).AsComparable();

            if (l.Kind == FilterResult.FilterResultKind.Nothing ||
                r.Kind == FilterResult.FilterResultKind.Nothing)
            {
                bool bothNothing = l.Kind == FilterResult.FilterResultKind.Nothing &&
                                   r.Kind == FilterResult.FilterResultKind.Nothing;

                return op switch
                {
                    ComparisonOp.Equal => FilterResult.FromLogical(bothNothing),
                    ComparisonOp.NotEqual => FilterResult.FromLogical(!bothNothing),
                    _ => FilterResult.FromLogical(false),
                };
            }

            return FilterResult.FromLogical(CompareValues(l.Value, r.Value, op));
        };
    }

    private static bool CompareValues(in JsonElement left, in JsonElement right, ComparisonOp op)
    {
        JsonValueKind lk = left.ValueKind;
        JsonValueKind rk = right.ValueKind;

        if (op == ComparisonOp.Equal || op == ComparisonOp.NotEqual)
        {
            bool eq = DeepEquals(left, right);
            return op == ComparisonOp.Equal ? eq : !eq;
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
                ComparisonOp.LessThan => lv < rv,
                ComparisonOp.LessThanOrEqual => lv <= rv,
                ComparisonOp.GreaterThan => lv > rv,
                ComparisonOp.GreaterThanOrEqual => lv >= rv,
                _ => false,
            };
        }

        if (lk == JsonValueKind.String)
        {
            int cmp = string.CompareOrdinal(
                left.GetString(),
                right.GetString());
            return op switch
            {
                ComparisonOp.LessThan => cmp < 0,
                ComparisonOp.LessThanOrEqual => cmp <= 0,
                ComparisonOp.GreaterThan => cmp > 0,
                ComparisonOp.GreaterThanOrEqual => cmp >= 0,
                _ => false,
            };
        }

        // RFC 9535: <=, >= are defined as (< or ==) and (> or ==)
        // For types where < is always false (bool, null, object, array):
        // <= reduces to ==, >= reduces to ==, < is false, > is false
        if (op == ComparisonOp.LessThanOrEqual || op == ComparisonOp.GreaterThanOrEqual)
        {
            return DeepEquals(left, right);
        }

        return false;
    }

    private static bool DeepEquals(in JsonElement left, in JsonElement right)
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

    private static FilterEval CompileFilterQuery(FilterQueryNode query)
    {
        SegmentEval[] pipeline = new SegmentEval[query.Segments.Length];
        for (int i = 0; i < query.Segments.Length; i++)
        {
            pipeline[i] = CompileSegment(query.Segments[i]);
        }

        bool isRelative = query.IsRelative;

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            JsonElement target = isRelative ? current : root;
            JsonElement nodeList = BuildSingletonArray(target, workspace);

            for (int i = 0; i < pipeline.Length; i++)
            {
                nodeList = pipeline[i](root, nodeList, workspace);
                if (nodeList.IsNullOrUndefined() || nodeList.GetArrayLength() == 0)
                {
                    return FilterResult.FromNodes(JsonPathSequenceBuilder.EmptyArrayElement);
                }
            }

            return FilterResult.FromNodes(nodeList);
        };
    }

    private static FilterEval CompileLiteral(LiteralNode lit)
    {
        JsonElement value = JsonElement.ParseValue(Encoding.UTF8.GetBytes(lit.RawJson));
        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            return FilterResult.FromValue(value);
        };
    }

    private static FilterEval CompileFunctionCall(FunctionCallNode func)
    {
        FilterEval[] compiledArgs = new FilterEval[func.Arguments.Length];
        for (int i = 0; i < func.Arguments.Length; i++)
        {
            compiledArgs[i] = CompileFilterExpression(func.Arguments[i]);
        }

        return func.Name switch
        {
            "length" => CompileLengthFunction(compiledArgs),
            "count" => CompileCountFunction(compiledArgs),
            "value" => CompileValueFunction(compiledArgs),
            "match" => CompileMatchFunction(compiledArgs),
            "search" => CompileSearchFunction(compiledArgs),
            _ => throw new JsonPathException($"Unknown function: '{func.Name}'."),
        };
    }

    private static FilterEval CompileLengthFunction(FilterEval[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonPathException("length() requires exactly 1 argument.");
        }

        FilterEval arg = args[0];

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult result = arg(root, current, workspace).AsComparable();
            if (result.Kind != FilterResult.FilterResultKind.ValueType)
            {
                return FilterResult.NothingResult;
            }

            JsonElement val = result.Value;
            int len = val.ValueKind switch
            {
                JsonValueKind.String => val.GetString()!.Length,
                JsonValueKind.Array => val.GetArrayLength(),
                JsonValueKind.Object => val.GetPropertyCount(),
                _ => -1,
            };

            if (len < 0)
            {
                return FilterResult.NothingResult;
            }

            return FilterResult.FromValue(JsonElement.ParseValue(Encoding.UTF8.GetBytes(len.ToString())));
        };
    }

    private static FilterEval CompileCountFunction(FilterEval[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonPathException("count() requires exactly 1 argument.");
        }

        FilterEval arg = args[0];

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult result = arg(root, current, workspace);
            if (result.Kind != FilterResult.FilterResultKind.NodesType)
            {
                return FilterResult.NothingResult;
            }

            int count = result.NodeList.IsNullOrUndefined() ? 0 : result.NodeList.GetArrayLength();
            return FilterResult.FromValue(JsonElement.ParseValue(Encoding.UTF8.GetBytes(count.ToString())));
        };
    }

    private static FilterEval CompileValueFunction(FilterEval[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonPathException("value() requires exactly 1 argument.");
        }

        FilterEval arg = args[0];

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult result = arg(root, current, workspace);
            if (result.Kind != FilterResult.FilterResultKind.NodesType)
            {
                return FilterResult.NothingResult;
            }

            if (!result.NodeList.IsNullOrUndefined() && result.NodeList.GetArrayLength() == 1)
            {
                foreach (JsonElement item in result.NodeList.EnumerateArray())
                {
                    return FilterResult.FromValue(item);
                }
            }

            return FilterResult.NothingResult;
        };
    }

    private static FilterEval CompileMatchFunction(FilterEval[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonPathException("match() requires exactly 2 arguments.");
        }

        FilterEval strArg = args[0];
        FilterEval patternArg = args[1];

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult strResult = strArg(root, current, workspace).AsComparable();
            FilterResult patResult = patternArg(root, current, workspace).AsComparable();

            if (strResult.Kind != FilterResult.FilterResultKind.ValueType ||
                patResult.Kind != FilterResult.FilterResultKind.ValueType ||
                strResult.Value.ValueKind != JsonValueKind.String ||
                patResult.Value.ValueKind != JsonValueKind.String)
            {
                return FilterResult.NothingResult;
            }

            string input = strResult.Value.GetString()!;
            string pattern = patResult.Value.GetString()!;

            try
            {
                string dotnetPattern = TranslateIRegexp(pattern);
                System.Text.RegularExpressions.Regex regex = new(
                    "^(?:" + dotnetPattern + ")$",
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                return FilterResult.FromLogical(regex.IsMatch(input));
            }
            catch (ArgumentException)
            {
                return FilterResult.NothingResult;
            }
        };
    }

    private static FilterEval CompileSearchFunction(FilterEval[] args)
    {
        if (args.Length != 2)
        {
            throw new JsonPathException("search() requires exactly 2 arguments.");
        }

        FilterEval strArg = args[0];
        FilterEval patternArg = args[1];

        return (in JsonElement root, in JsonElement current, JsonWorkspace workspace) =>
        {
            FilterResult strResult = strArg(root, current, workspace).AsComparable();
            FilterResult patResult = patternArg(root, current, workspace).AsComparable();

            if (strResult.Kind != FilterResult.FilterResultKind.ValueType ||
                patResult.Kind != FilterResult.FilterResultKind.ValueType ||
                strResult.Value.ValueKind != JsonValueKind.String ||
                patResult.Value.ValueKind != JsonValueKind.String)
            {
                return FilterResult.NothingResult;
            }

            string input = strResult.Value.GetString()!;
            string pattern = patResult.Value.GetString()!;

            try
            {
                string dotnetPattern = TranslateIRegexp(pattern);
                System.Text.RegularExpressions.Regex regex = new(
                    dotnetPattern,
                    System.Text.RegularExpressions.RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                return FilterResult.FromLogical(regex.IsMatch(input));
            }
            catch (ArgumentException)
            {
                return FilterResult.NothingResult;
            }
        };
    }

    /// <summary>
    /// Translates an I-Regexp (RFC 9485) pattern to a .NET regex pattern.
    /// Key differences:
    /// - I-Regexp '.' matches any code point except U+000A (\n) and U+000D (\r)
    /// - .NET '.' matches any char except U+000A (\n) — so \r leaks through
    /// - I-Regexp '.' should match supplementary plane characters (surrogate pairs)
    /// </summary>
    private static string TranslateIRegexp(string pattern)
    {
        // Replace unescaped '.' with a pattern that:
        // 1. Excludes \r and \n (I-Regexp semantics)
        // 2. Matches surrogate pairs as single code points
        const string iRegexpDot = @"(?:[^\r\n\uD800-\uDFFF]|[\uD800-\uDBFF][\uDC00-\uDFFF])";

        StringBuilder sb = new(pattern.Length * 2);
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\' && i + 1 < pattern.Length)
            {
                // Pass through escape sequences unchanged
                sb.Append(c);
                sb.Append(pattern[i + 1]);
                i++;
            }
            else if (c == '[')
            {
                // Pass through character classes unchanged (dots inside [...] are literal)
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
                    sb.Append(pattern[i]); // closing ']'
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

    private static JsonElement BuildSingletonArray(in JsonElement item, JsonWorkspace workspace)
    {
        JsonPathSequenceBuilder builder = default;
        try
        {
            builder.Add(item);
            return builder.ToElement(workspace);
        }
        finally
        {
            builder.ReturnArray();
        }
    }
}
