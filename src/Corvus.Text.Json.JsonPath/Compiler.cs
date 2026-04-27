// <copyright file="Compiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Compiles JSONPath expression strings into delegate trees for efficient evaluation.
/// </summary>
internal static class Compiler
{
    /// <summary>
    /// Compiles a JSONPath expression string into a <see cref="CompiledJsonPath"/>.
    /// </summary>
    /// <param name="expression">The JSONPath expression.</param>
    /// <returns>A compiled query that can be executed against JSON data.</returns>
    public static CompiledJsonPath Compile(string expression)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(expression);
        return Compile(utf8);
    }

    /// <summary>
    /// Compiles a UTF-8 JSONPath expression into a <see cref="CompiledJsonPath"/>.
    /// </summary>
    /// <param name="utf8Expression">The UTF-8 encoded JSONPath expression.</param>
    /// <returns>A compiled query that can be executed against JSON data.</returns>
    public static CompiledJsonPath Compile(byte[] utf8Expression)
    {
        QueryNode ast = Parser.Parse(utf8Expression);

        if (ast.Segments.Length == 0)
        {
            return new CompiledJsonPath(null);
        }

        SegmentEval[] pipeline = new SegmentEval[ast.Segments.Length];
        for (int i = 0; i < ast.Segments.Length; i++)
        {
            pipeline[i] = CompileSegment(ast.Segments[i]);
        }

        return new CompiledJsonPath(pipeline);
    }

    /// <summary>
    /// A compiled JSONPath query. Holds the segment pipeline and provides
    /// zero-allocation execution via <see cref="ExecuteNodes"/>.
    /// </summary>
    internal sealed class CompiledJsonPath
    {
        private readonly SegmentEval[]? _pipeline;

        internal CompiledJsonPath(SegmentEval[]? pipeline) => _pipeline = pipeline;

        /// <summary>
        /// Executes the query, writing matched nodes into <paramref name="result"/>.
        /// </summary>
        internal void ExecuteNodes(in JsonElement root, ref JsonPathResult result)
        {
            if (_pipeline is null)
            {
                result.Append(root);
                return;
            }

            // Internal ping-pong buffer backed by ArrayPool so it is safe
            // to return to the caller via the ref parameter after swapping.
            JsonPathResult b = JsonPathResult.CreatePooled(16);
            result.Append(root);

            try
            {
                for (int i = 0; i < _pipeline.Length; i++)
                {
                    b.Clear();
                    _pipeline[i](root, ref result, ref b);

                    if (b.Count == 0)
                    {
                        result.Clear();
                        return;
                    }

                    SwapResults(ref result, ref b);
                }
            }
            finally
            {
                b.Dispose();
            }
        }
    }

    internal delegate void SegmentEval(
        in JsonElement root,
        ref JsonPathResult input,
        ref JsonPathResult output);

    private static SegmentEval CompileSegment(SegmentNode segment)
    {
        SelectorEval[] selectors = new SelectorEval[segment.Selectors.Length];
        for (int i = 0; i < segment.Selectors.Length; i++)
        {
            selectors[i] = CompileSelector(segment.Selectors[i]);
        }

        if (segment is DescendantSegmentNode)
        {
            return (in JsonElement root, ref JsonPathResult input, ref JsonPathResult output) =>
            {
                EvalDescendantSegment(root, ref input, selectors, ref output);
            };
        }

        return (in JsonElement root, ref JsonPathResult input, ref JsonPathResult output) =>
        {
            EvalChildSegment(root, ref input, selectors, ref output);
        };
    }

    private static void EvalChildSegment(
        in JsonElement root,
        ref JsonPathResult input,
        SelectorEval[] selectors,
        ref JsonPathResult output)
    {
        ReadOnlySpan<JsonElement> inputNodes = input.Nodes;
        for (int i = 0; i < inputNodes.Length; i++)
        {
            for (int j = 0; j < selectors.Length; j++)
            {
                selectors[j](root, inputNodes[i], ref output);
            }
        }
    }

    private static void EvalDescendantSegment(
        in JsonElement root,
        ref JsonPathResult input,
        SelectorEval[] selectors,
        ref JsonPathResult output)
    {
        ReadOnlySpan<JsonElement> inputNodes = input.Nodes;
        for (int i = 0; i < inputNodes.Length; i++)
        {
            VisitDescendants(root, inputNodes[i], selectors, ref output);
        }
    }

    private static void VisitDescendants(
        in JsonElement root,
        in JsonElement node,
        SelectorEval[] selectors,
        ref JsonPathResult output)
    {
        // Iterative DFS with an explicit stack to avoid stack overflow on deep documents.
        int stackCapacity = 64;
        JsonElement[] rentedStack = ArrayPool<JsonElement>.Shared.Rent(stackCapacity);

        try
        {
            int stackSize = 0;
            rentedStack[stackSize++] = node;

            while (stackSize > 0)
            {
                JsonElement current = rentedStack[--stackSize];

                for (int i = 0; i < selectors.Length; i++)
                {
                    selectors[i](root, current, ref output);
                }

                if (current.ValueKind == JsonValueKind.Object)
                {
                    int childStart = stackSize;
                    foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
                    {
                        if (stackSize >= stackCapacity)
                        {
                            GrowStack(ref rentedStack, ref stackCapacity, stackSize);
                        }

                        rentedStack[stackSize++] = prop.Value;
                    }

                    ReverseSpan(rentedStack.AsSpan(childStart, stackSize - childStart));
                }
                else if (current.ValueKind == JsonValueKind.Array)
                {
                    int childStart = stackSize;
                    foreach (JsonElement item in current.EnumerateArray())
                    {
                        if (stackSize >= stackCapacity)
                        {
                            GrowStack(ref rentedStack, ref stackCapacity, stackSize);
                        }

                        rentedStack[stackSize++] = item;
                    }

                    ReverseSpan(rentedStack.AsSpan(childStart, stackSize - childStart));
                }
            }
        }
        finally
        {
            ArrayPool<JsonElement>.Shared.Return(rentedStack, clearArray: true);
        }
    }

    private static void GrowStack(ref JsonElement[] rentedStack, ref int capacity, int currentSize)
    {
        int newCapacity = capacity * 2;
        JsonElement[] newArray = ArrayPool<JsonElement>.Shared.Rent(newCapacity);
        Array.Copy(rentedStack, newArray, currentSize);
        ArrayPool<JsonElement>.Shared.Return(rentedStack);
        rentedStack = newArray;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapResults(ref JsonPathResult a, ref JsonPathResult b)
    {
        JsonPathResult tmp = a;
        a = b;
        b = tmp;
    }

    private delegate void SelectorEval(
        in JsonElement root,
        in JsonElement node,
        ref JsonPathResult output);

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
        return (in JsonElement root, in JsonElement node, ref JsonPathResult output) =>
        {
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty(utf8Name, out JsonElement value))
            {
                output.Append(value);
            }
        };
    }

    private static SelectorEval CompileWildcardSelector()
    {
        return static (in JsonElement root, in JsonElement node, ref JsonPathResult output) =>
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
                {
                    output.Append(prop.Value);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    output.Append(item);
                }
            }
        };
    }

    private static SelectorEval CompileIndexSelector(IndexSelectorNode idx)
    {
        long index = idx.Index;
        return (in JsonElement root, in JsonElement node, ref JsonPathResult output) =>
        {
            if (node.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int len = node.GetArrayLength();
            long resolved = index >= 0 ? index : len + index;
            if (resolved >= 0 && resolved < len)
            {
                output.Append(node[(int)resolved]);
            }
        };
    }

    private static SelectorEval CompileSliceSelector(SliceSelectorNode slice)
    {
        long? startVal = slice.Start;
        long? endVal = slice.End;
        long? stepVal = slice.Step;

        return (in JsonElement root, in JsonElement node, ref JsonPathResult output) =>
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
                    output.Append(node[(int)i]);
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
                    output.Append(node[(int)i]);
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

        return (in JsonElement root, in JsonElement node, ref JsonPathResult output) =>
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    if (EvalFilterAsTruthy(compiledFilter, root, item))
                    {
                        output.Append(item);
                    }
                }
            }
            else if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
                {
                    if (EvalFilterAsTruthy(compiledFilter, root, prop.Value))
                    {
                        output.Append(prop.Value);
                    }
                }
            }
        };
    }

    private delegate FilterResult FilterEval(in JsonElement root, in JsonElement current);

    private readonly struct FilterResult
    {
        private FilterResult(FilterResultKind kind, bool logical, JsonElement value, int nodeCount, JsonElement firstNode)
        {
            Kind = kind;
            Logical = logical;
            Value = value;
            NodeCount = nodeCount;
            FirstNode = firstNode;
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

        public int NodeCount { get; }

        public JsonElement FirstNode { get; }

        public static FilterResult FromLogical(bool value) =>
            new(FilterResultKind.LogicalType, value, default, 0, default);

        public static FilterResult FromValue(JsonElement value) =>
            new(FilterResultKind.ValueType, false, value, 0, default);

        public static FilterResult FromNodes(int count, JsonElement firstNode) =>
            new(FilterResultKind.NodesType, false, default, count, firstNode);

        public static FilterResult NothingResult { get; } =
            new(FilterResultKind.Nothing, false, default, 0, default);

        public bool AsTruthy()
        {
            return Kind switch
            {
                FilterResultKind.LogicalType => Logical,
                FilterResultKind.NodesType => NodeCount > 0,
                _ => false,
            };
        }

        public FilterResult AsComparable()
        {
            if (Kind == FilterResultKind.ValueType)
            {
                return this;
            }

            if (Kind == FilterResultKind.NodesType && NodeCount == 1)
            {
                return FromValue(FirstNode);
            }

            return NothingResult;
        }
    }

    private static bool EvalFilterAsTruthy(FilterEval filter, in JsonElement root, in JsonElement current)
    {
        FilterResult result = filter(root, current);
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

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult l = left(root, current);
            if (!l.AsTruthy())
            {
                return FilterResult.FromLogical(false);
            }

            FilterResult r = right(root, current);
            return FilterResult.FromLogical(r.AsTruthy());
        };
    }

    private static FilterEval CompileLogicalOr(LogicalOrNode or)
    {
        FilterEval left = CompileFilterExpression(or.Left);
        FilterEval right = CompileFilterExpression(or.Right);

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult l = left(root, current);
            if (l.AsTruthy())
            {
                return FilterResult.FromLogical(true);
            }

            FilterResult r = right(root, current);
            return FilterResult.FromLogical(r.AsTruthy());
        };
    }

    private static FilterEval CompileLogicalNot(LogicalNotNode not)
    {
        FilterEval operand = CompileFilterExpression(not.Operand);

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult result = operand(root, current);
            return FilterResult.FromLogical(!result.AsTruthy());
        };
    }

    private static FilterEval CompileComparison(ComparisonNode cmp)
    {
        FilterEval left = CompileFilterExpression(cmp.Left);
        FilterEval right = CompileFilterExpression(cmp.Right);
        int op = (int)cmp.Op;

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult l = left(root, current).AsComparable();
            FilterResult r = right(root, current).AsComparable();

            return FilterResult.FromLogical(
                JsonPathCodeGenHelpers.CompareValues(l.Value, r.Value, op));
        };
    }

    private static FilterEval CompileFilterQuery(FilterQueryNode query)
    {
        SegmentEval[] pipeline = new SegmentEval[query.Segments.Length];
        for (int i = 0; i < query.Segments.Length; i++)
        {
            pipeline[i] = CompileSegment(query.Segments[i]);
        }

        bool isRelative = query.IsRelative;

        if (pipeline.Length == 0)
        {
            return (in JsonElement root, in JsonElement current) =>
            {
                JsonElement target = isRelative ? current : root;
                return FilterResult.FromNodes(1, target);
            };
        }

        return (in JsonElement root, in JsonElement current) =>
        {
            JsonElement target = isRelative ? current : root;
            JsonPathResult a = JsonPathResult.CreatePooled(16);
            JsonPathResult b = JsonPathResult.CreatePooled(16);
            a.Append(target);

            try
            {
                for (int i = 0; i < pipeline.Length; i++)
                {
                    b.Clear();
                    pipeline[i](root, ref a, ref b);

                    if (b.Count == 0)
                    {
                        return FilterResult.FromNodes(0, default);
                    }

                    SwapResults(ref a, ref b);
                }

                return FilterResult.FromNodes(a.Count, a[0]);
            }
            finally
            {
                a.Dispose();
                b.Dispose();
            }
        };
    }

    private static FilterEval CompileLiteral(LiteralNode lit)
    {
        JsonElement value = JsonElement.ParseValue(Encoding.UTF8.GetBytes(lit.RawJson));
        return (in JsonElement root, in JsonElement current) =>
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
            "match" => CompileMatchFunction(compiledArgs, func.Arguments.Length > 1 ? func.Arguments[1] : null, fullMatch: true),
            "search" => CompileMatchFunction(compiledArgs, func.Arguments.Length > 1 ? func.Arguments[1] : null, fullMatch: false),
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

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult result = arg(root, current).AsComparable();
            if (result.Kind != FilterResult.FilterResultKind.ValueType)
            {
                return FilterResult.NothingResult;
            }

            JsonElement val = result.Value;
            int len = JsonPathCodeGenHelpers.LengthValue(val);

            if (len < 0)
            {
                return FilterResult.NothingResult;
            }

            return FilterResult.FromValue(JsonPathCodeGenHelpers.IntToElement(len));
        };
    }

    private static FilterEval CompileCountFunction(FilterEval[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonPathException("count() requires exactly 1 argument.");
        }

        FilterEval arg = args[0];

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult result = arg(root, current);
            if (result.Kind != FilterResult.FilterResultKind.NodesType)
            {
                return FilterResult.NothingResult;
            }

            int count = result.NodeCount;
            return FilterResult.FromValue(JsonPathCodeGenHelpers.IntToElement(count));
        };
    }

    private static FilterEval CompileValueFunction(FilterEval[] args)
    {
        if (args.Length != 1)
        {
            throw new JsonPathException("value() requires exactly 1 argument.");
        }

        FilterEval arg = args[0];

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult result = arg(root, current);
            if (result.Kind != FilterResult.FilterResultKind.NodesType)
            {
                return FilterResult.NothingResult;
            }

            if (result.NodeCount == 1)
            {
                return FilterResult.FromValue(result.FirstNode);
            }

            return FilterResult.NothingResult;
        };
    }

    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new();

    private static FilterEval CompileMatchFunction(FilterEval[] args, FilterExpressionNode? patternAstNode, bool fullMatch)
    {
        if (args.Length != 2)
        {
            throw new JsonPathException(fullMatch ? "match() requires exactly 2 arguments." : "search() requires exactly 2 arguments.");
        }

        FilterEval strArg = args[0];
        FilterEval patternArg = args[1];

        if (patternAstNode is LiteralNode patLit && patLit.Kind == LiteralKind.String)
        {
            JsonElement patElement = JsonElement.ParseValue(Encoding.UTF8.GetBytes(patLit.RawJson));
            string pattern = patElement.GetString()!;
            Regex? compiled = CompileRegex(pattern, fullMatch);

            if (compiled is null)
            {
                return static (in JsonElement root, in JsonElement current) =>
                    FilterResult.NothingResult;
            }

            return (in JsonElement root, in JsonElement current) =>
            {
                FilterResult strResult = strArg(root, current).AsComparable();
                if (strResult.Kind != FilterResult.FilterResultKind.ValueType ||
                    strResult.Value.ValueKind != JsonValueKind.String)
                {
                    return FilterResult.NothingResult;
                }

                return FilterResult.FromLogical(MatchInput(compiled, strResult.Value));
            };
        }

        return (in JsonElement root, in JsonElement current) =>
        {
            FilterResult strResult = strArg(root, current).AsComparable();
            FilterResult patResult = patternArg(root, current).AsComparable();

            if (strResult.Kind != FilterResult.FilterResultKind.ValueType ||
                patResult.Kind != FilterResult.FilterResultKind.ValueType ||
                strResult.Value.ValueKind != JsonValueKind.String ||
                patResult.Value.ValueKind != JsonValueKind.String)
            {
                return FilterResult.NothingResult;
            }

            string pattern = patResult.Value.GetString()!;
            string cacheKey = fullMatch ? "m:" + pattern : "s:" + pattern;

            Regex? regex = RegexCache.GetOrAdd(cacheKey, _ => CompileRegex(pattern, fullMatch));

            if (regex is null)
            {
                return FilterResult.NothingResult;
            }

            return FilterResult.FromLogical(MatchInput(regex, strResult.Value));
        };
    }

    private static Regex? CompileRegex(string pattern, bool fullMatch)
    {
        try
        {
            string dotnetPattern = JsonPathCodeGenHelpers.TranslateIRegexp(pattern);
            string wrapped = fullMatch ? "^(?:" + dotnetPattern + ")$" : dotnetPattern;
            return new Regex(wrapped, RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchInput(Regex regex, in JsonElement input)
    {
#if NET
        using UnescapedUtf16JsonString utf16 = input.GetUtf16String();
        return regex.IsMatch(utf16.Span);
#else
        return regex.IsMatch(input.GetString()!);
#endif
    }
}