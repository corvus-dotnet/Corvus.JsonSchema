// <copyright file="Compiler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Compiles JSONPath expression strings into streaming delegate trees for efficient evaluation.
/// </summary>
/// <remarks>
/// <para>
/// The compiler builds a push-based streaming pipeline: each segment processes a single input
/// node and chains matching results directly to the next segment downstream, eliminating
/// intermediate materialization buffers. The terminal segment writes to the output
/// <see cref="JsonPathResult"/>.
/// </para>
/// </remarks>
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
            return new CompiledJsonPath(static (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
            {
                result.Append(node);
            });
        }

        // Build the pipeline bottom-up: the terminal action appends to the result,
        // and each segment wraps the downstream action.
        NodeEval downstream = static (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            result.Append(node);
        };

        for (int i = ast.Segments.Length - 1; i >= 0; i--)
        {
            downstream = CompileSegment(ast.Segments[i], downstream);
        }

        return new CompiledJsonPath(downstream);
    }

    /// <summary>
    /// A compiled JSONPath query. Holds the streaming entry point and provides
    /// zero-allocation execution via <see cref="ExecuteNodes"/>.
    /// </summary>
    internal sealed class CompiledJsonPath
    {
        private readonly NodeEval _entryPoint;

        internal CompiledJsonPath(NodeEval entryPoint) => _entryPoint = entryPoint;

        /// <summary>
        /// Executes the query, writing matched nodes into <paramref name="result"/>.
        /// </summary>
        internal void ExecuteNodes(in JsonElement root, ref JsonPathResult result)
        {
            _entryPoint(root, root, ref result);
        }
    }

    /// <summary>
    /// Processes a single input node and writes matched results downstream.
    /// </summary>
    internal delegate void NodeEval(
        in JsonElement root,
        in JsonElement node,
        ref JsonPathResult result);

    private static NodeEval CompileSegment(SegmentNode segment, NodeEval downstream)
    {
        if (segment is DescendantSegmentNode)
        {
            return CompileDescendantSegment(segment.Selectors, downstream);
        }

        return CompileChildSegment(segment.Selectors, downstream);
    }

    private static NodeEval CompileChildSegment(ImmutableArray<SelectorNode> selectors, NodeEval downstream)
    {
        NodeEval[] compiledSelectors = new NodeEval[selectors.Length];
        for (int i = 0; i < selectors.Length; i++)
        {
            compiledSelectors[i] = CompileSelector(selectors[i], downstream);
        }

        if (compiledSelectors.Length == 1)
        {
            NodeEval single = compiledSelectors[0];
            return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
            {
                single(root, node, ref result);
            };
        }

        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            for (int i = 0; i < compiledSelectors.Length; i++)
            {
                compiledSelectors[i](root, node, ref result);
            }
        };
    }

    private static NodeEval CompileDescendantSegment(ImmutableArray<SelectorNode> selectors, NodeEval downstream)
    {
        // Specialization: single name selector → recursive DFS with direct TryGetProperty
        if (selectors.Length == 1 && selectors[0] is NameSelectorNode nameSelector)
        {
            byte[] utf8Name = nameSelector.Name;
            return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
            {
                VisitDescendantsForName(root, node, utf8Name, downstream, ref result);
            };
        }

        NodeEval[] compiledSelectors = new NodeEval[selectors.Length];
        for (int i = 0; i < selectors.Length; i++)
        {
            compiledSelectors[i] = CompileSelector(selectors[i], downstream);
        }

        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            VisitDescendants(root, node, compiledSelectors, ref result);
        };
    }

    private static void VisitDescendantsForName(
        in JsonElement root,
        in JsonElement node,
        byte[] utf8Name,
        NodeEval downstream,
        ref JsonPathResult result)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty(utf8Name, out JsonElement value))
            {
                downstream(root, value, ref result);
            }

            foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
            {
                JsonElement child = prop.Value;
                if (child.ValueKind == JsonValueKind.Object || child.ValueKind == JsonValueKind.Array)
                {
                    VisitDescendantsForName(root, child, utf8Name, downstream, ref result);
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                {
                    VisitDescendantsForName(root, item, utf8Name, downstream, ref result);
                }
            }
        }
    }

    private static void VisitDescendants(
        in JsonElement root,
        in JsonElement node,
        NodeEval[] selectors,
        ref JsonPathResult result)
    {
        // Apply selectors at this node.
        for (int i = 0; i < selectors.Length; i++)
        {
            selectors[i](root, node, ref result);
        }

        // Recurse into container children only — primitives have no children.
        if (node.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
            {
                VisitDescendants(root, prop.Value, selectors, ref result);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                VisitDescendants(root, item, selectors, ref result);
            }
        }
    }

    private static NodeEval CompileSelector(SelectorNode selector, NodeEval downstream)
    {
        return selector switch
        {
            NameSelectorNode name => CompileNameSelector(name, downstream),
            WildcardSelectorNode => CompileWildcardSelector(downstream),
            IndexSelectorNode idx => CompileIndexSelector(idx, downstream),
            SliceSelectorNode slice => CompileSliceSelector(slice, downstream),
            FilterSelectorNode filter => CompileFilterSelector(filter, downstream),
            _ => throw new JsonPathException($"Unknown selector type: {selector.GetType().Name}"),
        };
    }

    private static NodeEval CompileNameSelector(NameSelectorNode name, NodeEval downstream)
    {
        byte[] utf8Name = name.Name;
        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            if (node.ValueKind == JsonValueKind.Object &&
                node.TryGetProperty(utf8Name, out JsonElement value))
            {
                downstream(root, value, ref result);
            }
        };
    }

    private static NodeEval CompileWildcardSelector(NodeEval downstream)
    {
        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
                {
                    downstream(root, prop.Value, ref result);
                }
            }
            else if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    downstream(root, item, ref result);
                }
            }
        };
    }

    private static NodeEval CompileIndexSelector(IndexSelectorNode idx, NodeEval downstream)
    {
        long index = idx.Index;
        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            if (node.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int len = node.GetArrayLength();
            long resolved = index >= 0 ? index : len + index;
            if (resolved >= 0 && resolved < len)
            {
                downstream(root, node[(int)resolved], ref result);
            }
        };
    }

    private static NodeEval CompileSliceSelector(SliceSelectorNode slice, NodeEval downstream)
    {
        long? startVal = slice.Start;
        long? endVal = slice.End;
        long? stepVal = slice.Step;

        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
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
                    downstream(root, node[(int)i], ref result);
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
                    downstream(root, node[(int)i], ref result);
                }
            }
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long NormalizeSliceIndex(long index, int len)
    {
        return index >= 0 ? index : len + index;
    }

    private static NodeEval CompileFilterSelector(FilterSelectorNode filter, NodeEval downstream)
    {
        FilterEval compiledFilter = CompileFilterExpression(filter.Expression);

        return (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            if (node.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in node.EnumerateArray())
                {
                    if (EvalFilterAsTruthy(compiledFilter, root, item))
                    {
                        downstream(root, item, ref result);
                    }
                }
            }
            else if (node.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> prop in node.EnumerateObject())
                {
                    if (EvalFilterAsTruthy(compiledFilter, root, prop.Value))
                    {
                        downstream(root, prop.Value, ref result);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    /// <summary>
    /// Determines whether a filter query is a singular query — i.e., each segment
    /// has exactly one selector that is a name or index. Singular queries can only
    /// produce 0 or 1 nodes and don't need intermediate buffers.
    /// </summary>
    private static bool IsSingularQuery(ImmutableArray<SegmentNode> segments)
    {
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] is not ChildSegmentNode)
            {
                return false;
            }

            if (segments[i].Selectors.Length != 1)
            {
                return false;
            }

            if (segments[i].Selectors[0] is not (NameSelectorNode or IndexSelectorNode))
            {
                return false;
            }
        }

        return true;
    }

    private static FilterEval CompileFilterQuery(FilterQueryNode query)
    {
        bool isRelative = query.IsRelative;

        if (query.Segments.Length == 0)
        {
            return (in JsonElement root, in JsonElement current) =>
            {
                JsonElement target = isRelative ? current : root;
                return FilterResult.FromNodes(1, target);
            };
        }

        // Fast path: singular queries (e.g. @.price, @.a.b, $[0].name)
        // use direct property/index chain — no buffers at all.
        if (IsSingularQuery(query.Segments))
        {
            return CompileSingularFilterQuery(query.Segments, isRelative);
        }

        // General path: non-singular queries use streaming pipeline.
        return CompileGeneralFilterQuery(query.Segments, isRelative);
    }

    private static FilterEval CompileSingularFilterQuery(
        ImmutableArray<SegmentNode> segments, bool isRelative)
    {
        // Build a chain of lookup steps: (node) → TryGetProperty/index → node → ...
        SingularStep[] steps = new SingularStep[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            SelectorNode sel = segments[i].Selectors[0];
            steps[i] = sel switch
            {
                NameSelectorNode name => new SingularStep(name.Name, 0, isName: true),
                IndexSelectorNode idx => new SingularStep(null, idx.Index, isName: false),
                _ => throw new JsonPathException("Unexpected selector in singular query."),
            };
        }

        return (in JsonElement root, in JsonElement current) =>
        {
            JsonElement node = isRelative ? current : root;
            for (int i = 0; i < steps.Length; i++)
            {
                ref readonly SingularStep step = ref steps[i];
                if (step.IsName)
                {
                    if (node.ValueKind != JsonValueKind.Object ||
                        !node.TryGetProperty(step.Name!, out node))
                    {
                        return FilterResult.FromNodes(0, default);
                    }
                }
                else
                {
                    if (node.ValueKind != JsonValueKind.Array)
                    {
                        return FilterResult.FromNodes(0, default);
                    }

                    int len = node.GetArrayLength();
                    long resolved = step.Index >= 0 ? step.Index : len + step.Index;
                    if (resolved < 0 || resolved >= len)
                    {
                        return FilterResult.FromNodes(0, default);
                    }

                    node = node[(int)resolved];
                }
            }

            return FilterResult.FromNodes(1, node);
        };
    }

    private readonly struct SingularStep
    {
        public SingularStep(byte[]? name, long index, bool isName)
        {
            Name = name;
            Index = index;
            IsName = isName;
        }

        public byte[]? Name { get; }

        public long Index { get; }

        public bool IsName { get; }
    }

    private static FilterEval CompileGeneralFilterQuery(
        ImmutableArray<SegmentNode> segments, bool isRelative)
    {
        // Build a streaming pipeline for the inner query, collecting into a
        // temporary JsonPathResult.
        NodeEval terminal = static (in JsonElement root, in JsonElement node, ref JsonPathResult result) =>
        {
            result.Append(node);
        };

        NodeEval chain = terminal;
        for (int i = segments.Length - 1; i >= 0; i--)
        {
            chain = CompileSegment(segments[i], chain);
        }

        NodeEval innerPipeline = chain;

        return (in JsonElement root, in JsonElement current) =>
        {
            JsonElement target = isRelative ? current : root;
            JsonPathResult innerResult = JsonPathResult.CreatePooled(16);
            try
            {
                innerPipeline(root, target, ref innerResult);
                return FilterResult.FromNodes(innerResult.Count, innerResult.Count > 0 ? innerResult[0] : default);
            }
            finally
            {
                innerResult.Dispose();
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