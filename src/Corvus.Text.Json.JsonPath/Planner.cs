// <copyright file="Planner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Lowers a parsed JSONPath <see cref="QueryNode"/> AST into an execution
/// plan tree of <see cref="PlanNode"/> steps. The plan captures all
/// optimisations (singleton chain fusion, hash-based name-set dispatch,
/// specialised comparisons) so that both the runtime <see cref="PlanInterpreter"/>
/// and the code generator can consume the same optimised structure.
/// </summary>
internal static class Planner
{
    /// <summary>
    /// Compiles a <see cref="QueryNode"/> AST into a <see cref="PlanNode"/> tree.
    /// </summary>
    internal static PlanNode Plan(QueryNode ast)
    {
        ImmutableArray<SegmentNode> segments = ast.Segments;

        if (segments.Length == 0)
        {
            return EmitStep.Instance;
        }

        return PlanSegments(segments, 0);
    }

    /// <summary>
    /// Plans the segment chain starting at <paramref name="startIndex"/>,
    /// fusing consecutive singleton segments into a <see cref="SingletonChainStep"/>
    /// where possible.
    /// </summary>
    private static PlanNode PlanSegments(ImmutableArray<SegmentNode> segments, int startIndex)
    {
        if (startIndex >= segments.Length)
        {
            return EmitStep.Instance;
        }

        // Try to fuse a run of consecutive singleton child segments.
        int singletonRun = CountSingletonRun(segments, startIndex);

        if (singletonRun >= 2)
        {
            // Fuse the run into a SingletonChainStep.
            SingularNav[] steps = new SingularNav[singletonRun];
            for (int i = 0; i < singletonRun; i++)
            {
                SelectorNode sel = segments[startIndex + i].Selectors[0];
                steps[i] = sel switch
                {
                    NameSelectorNode name => new SingularNav(name.Name),
                    IndexSelectorNode idx => new SingularNav(idx.Index),
                    _ => throw new JsonPathException("Unexpected selector in singleton chain."),
                };
            }

            PlanNode continuation = PlanSegments(segments, startIndex + singletonRun);
            return new SingletonChainStep(steps, continuation);
        }

        // Not fusable — plan this segment individually.
        return PlanSegment(segments[startIndex], segments, startIndex + 1);
    }

    /// <summary>
    /// Plans a single segment, creating the appropriate plan node and wiring
    /// it to the continuation from the remaining segments.
    /// </summary>
    private static PlanNode PlanSegment(
        SegmentNode segment,
        ImmutableArray<SegmentNode> allSegments,
        int nextIndex)
    {
        PlanNode continuation = PlanSegments(allSegments, nextIndex);

        if (segment is DescendantSegmentNode)
        {
            return PlanDescendantSegment(segment.Selectors, continuation);
        }

        return PlanChildSegment(segment.Selectors, continuation);
    }

    // ── Child segments ───────────────────────────────────────────────
    private static PlanNode PlanChildSegment(
        ImmutableArray<SelectorNode> selectors,
        PlanNode continuation)
    {
        if (selectors.Length == 1)
        {
            return PlanSelector(selectors[0], continuation);
        }

        // Multiple selectors: check if ALL are name selectors with unique names → NameSetStep
        if (AllNameSelectors(selectors) && AllUniqueNames(selectors))
        {
            return BuildNameSetStep(selectors, continuation);
        }

        // Mixed selectors: plan each individually, wrap in MultiSelectorStep
        PlanNode[] plans = new PlanNode[selectors.Length];
        for (int i = 0; i < selectors.Length; i++)
        {
            plans[i] = PlanSelector(selectors[i], continuation);
        }

        return new MultiSelectorStep(plans);
    }

    private static PlanNode PlanSelector(SelectorNode selector, PlanNode continuation)
    {
        return selector switch
        {
            NameSelectorNode name => new NavigateNameStep(name.Name, continuation),
            IndexSelectorNode idx => new NavigateIndexStep(idx.Index, continuation),
            WildcardSelectorNode => new WildcardStep(continuation),
            SliceSelectorNode slice => new SliceStep(slice.Start, slice.End, slice.Step, continuation),
            FilterSelectorNode filter => new FilterStep(PlanFilterExpression(filter.Expression), continuation),
            _ => throw new JsonPathException($"Unknown selector type: {selector.GetType().Name}"),
        };
    }

    private static NameSetStep BuildNameSetStep(
        ImmutableArray<SelectorNode> selectors,
        PlanNode continuation)
    {
        NameSetEntry[] entries = new NameSetEntry[selectors.Length];
        byte[][] names = new byte[selectors.Length][];

        for (int i = 0; i < selectors.Length; i++)
        {
            byte[] utf8Name = ((NameSelectorNode)selectors[i]).Name;
            entries[i] = new NameSetEntry(utf8Name, continuation);
            names[i] = utf8Name;
        }

        return new NameSetStep(entries, new NameDispatchTable(names));
    }

    // ── Descendant segments ──────────────────────────────────────────
    private static PlanNode PlanDescendantSegment(
        ImmutableArray<SelectorNode> selectors,
        PlanNode continuation)
    {
        // Specialization: single name selector → DescendantNameStep
        if (selectors.Length == 1 && selectors[0] is NameSelectorNode singleName)
        {
            return new DescendantNameStep(singleName.Name, continuation);
        }

        // Specialization: multiple name selectors with unique names → DescendantNameSetStep
        if (AllNameSelectors(selectors) && AllUniqueNames(selectors))
        {
            return BuildDescendantNameSetStep(selectors, continuation);
        }

        // General: plan each selector with its continuation baked in
        PlanNode[] selectorPlans = new PlanNode[selectors.Length];
        for (int i = 0; i < selectors.Length; i++)
        {
            selectorPlans[i] = PlanSelector(selectors[i], continuation);
        }

        return new DescendantStep(selectorPlans);
    }

    private static DescendantNameSetStep BuildDescendantNameSetStep(
        ImmutableArray<SelectorNode> selectors,
        PlanNode continuation)
    {
        NameSetEntry[] entries = new NameSetEntry[selectors.Length];
        byte[][] names = new byte[selectors.Length][];

        for (int i = 0; i < selectors.Length; i++)
        {
            byte[] utf8Name = ((NameSelectorNode)selectors[i]).Name;
            entries[i] = new NameSetEntry(utf8Name, continuation);
            names[i] = utf8Name;
        }

        return new DescendantNameSetStep(entries, new NameDispatchTable(names));
    }

    // ── Filter expressions ───────────────────────────────────────────
    private static FilterPlanNode PlanFilterExpression(FilterExpressionNode node)
    {
        return node switch
        {
            LogicalAndNode and => new FilterLogicalAndPlan(
                PlanFilterExpression(and.Left),
                PlanFilterExpression(and.Right)),

            LogicalOrNode or => new FilterLogicalOrPlan(
                PlanFilterExpression(or.Left),
                PlanFilterExpression(or.Right)),

            LogicalNotNode not => new FilterLogicalNotPlan(
                PlanFilterExpression(not.Operand)),

            ComparisonNode cmp => PlanComparison(cmp),
            FilterQueryNode query => PlanFilterQuery(query),
            FunctionCallNode func => PlanFunctionCall(func),
            LiteralNode lit => PlanLiteral(lit),

            ParenExpressionNode paren => PlanFilterExpression(paren.Inner),

            _ => throw new JsonPathException($"Unknown filter expression type: {node.GetType().Name}"),
        };
    }

    // ── Comparisons (with specializations) ───────────────────────────
    private static FilterPlanNode PlanComparison(ComparisonNode cmp)
    {
        FilterPlanNode? specialized = TryPlanSpecializedComparison(cmp);
        if (specialized is not null)
        {
            return specialized;
        }

        return new FilterComparisonPlan(
            PlanFilterExpression(cmp.Left),
            PlanFilterExpression(cmp.Right),
            cmp.Op);
    }

    private static FilterPlanNode? TryPlanSpecializedComparison(ComparisonNode cmp)
    {
        // Normalize: literal on right
        FilterExpressionNode exprSide;
        LiteralNode? literalSide;
        ComparisonOp op;

        if (cmp.Right is LiteralNode rightLit)
        {
            exprSide = cmp.Left;
            literalSide = rightLit;
            op = cmp.Op;
        }
        else if (cmp.Left is LiteralNode leftLit)
        {
            exprSide = cmp.Right;
            literalSide = leftLit;
            op = FlipOp(cmp.Op);
        }
        else
        {
            return null;
        }

        while (exprSide is ParenExpressionNode paren)
        {
            exprSide = paren.Inner;
        }

        if (literalSide.Kind == LiteralKind.Number)
        {
            if (!double.TryParse(literalSide.RawJson, NumberStyles.Any, CultureInfo.InvariantCulture, out double literalValue))
            {
                return null;
            }

            // length(expr) op number
            if (exprSide is FunctionCallNode { Name: "length" } lenFunc && lenFunc.Arguments.Length == 1)
            {
                return new FilterLengthNumericPlan(
                    PlanFilterExpression(lenFunc.Arguments[0]),
                    literalValue,
                    op);
            }

            // Singular query op number — most-optimized path
            if (exprSide is FilterQueryNode query && IsSingularQuery(query.Segments))
            {
                return new FilterSingularNumericPlan(
                    query.IsRelative,
                    BuildSingularSteps(query.Segments),
                    literalValue,
                    op);
            }

            // General: expr op number
            return new FilterNumericComparisonPlan(
                PlanFilterExpression(exprSide),
                literalValue,
                op);
        }

        if (literalSide.Kind is LiteralKind.Null or LiteralKind.True or LiteralKind.False)
        {
            JsonValueKind targetKind = literalSide.Kind switch
            {
                LiteralKind.Null => JsonValueKind.Null,
                LiteralKind.True => JsonValueKind.True,
                _ => JsonValueKind.False,
            };
            return new FilterKindComparisonPlan(
                PlanFilterExpression(exprSide),
                targetKind,
                op);
        }

        if (literalSide.Kind == LiteralKind.String && op is ComparisonOp.Equal or ComparisonOp.NotEqual)
        {
            JsonElement literalElement = JsonElement.ParseValue(Encoding.UTF8.GetBytes(literalSide.RawJson));
            return new FilterStringEqualityPlan(
                PlanFilterExpression(exprSide),
                literalElement,
                op);
        }

        return null;
    }

    // ── Filter queries ───────────────────────────────────────────────
    private static FilterPlanNode PlanFilterQuery(FilterQueryNode query)
    {
        if (query.Segments.Length == 0)
        {
            return new FilterEmptyQueryPlan(query.IsRelative);
        }

        if (IsSingularQuery(query.Segments))
        {
            return new FilterSingularQueryPlan(
                query.IsRelative,
                BuildSingularSteps(query.Segments));
        }

        // General: build a full sub-plan for the inner query
        PlanNode innerPlan = PlanSegments(query.Segments, 0);
        return new FilterGeneralQueryPlan(query.IsRelative, innerPlan);
    }

    // ── Function calls ───────────────────────────────────────────────
    private static FilterPlanNode PlanFunctionCall(FunctionCallNode func)
    {
        return func.Name switch
        {
            "length" when func.Arguments.Length == 1 =>
                new FilterLengthFunctionPlan(PlanFilterExpression(func.Arguments[0])),

            "count" when func.Arguments.Length == 1 =>
                new FilterCountFunctionPlan(PlanFilterExpression(func.Arguments[0])),

            "value" when func.Arguments.Length == 1 =>
                new FilterValueFunctionPlan(PlanFilterExpression(func.Arguments[0])),

            "match" when func.Arguments.Length == 2 =>
                PlanMatchFunction(func, fullMatch: true),

            "search" when func.Arguments.Length == 2 =>
                PlanMatchFunction(func, fullMatch: false),

            _ => throw new JsonPathException($"Unknown function or wrong arity: '{func.Name}'."),
        };
    }

    private static FilterMatchFunctionPlan PlanMatchFunction(FunctionCallNode func, bool fullMatch)
    {
        FilterPlanNode strArg = PlanFilterExpression(func.Arguments[0]);
        Regex? precompiled = null;
        FilterPlanNode? dynamicPatternArg = null;

        if (func.Arguments[1] is LiteralNode patLit && patLit.Kind == LiteralKind.String)
        {
            JsonElement patElement = JsonElement.ParseValue(Encoding.UTF8.GetBytes(patLit.RawJson));
            string pattern = patElement.GetString()!;
            precompiled = CompileRegex(pattern, fullMatch);
        }
        else
        {
            dynamicPatternArg = PlanFilterExpression(func.Arguments[1]);
        }

        return new FilterMatchFunctionPlan(strArg, precompiled, dynamicPatternArg, fullMatch);
    }

    // ── Literals ─────────────────────────────────────────────────────
    private static FilterLiteralPlan PlanLiteral(LiteralNode lit)
    {
        JsonElement value = JsonElement.ParseValue(Encoding.UTF8.GetBytes(lit.RawJson));
        return new FilterLiteralPlan(value);
    }

    // ── Helpers ──────────────────────────────────────────────────────
    private static bool AllNameSelectors(ImmutableArray<SelectorNode> selectors)
    {
        for (int i = 0; i < selectors.Length; i++)
        {
            if (selectors[i] is not NameSelectorNode)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AllUniqueNames(ImmutableArray<SelectorNode> selectors)
    {
        for (int i = 0; i < selectors.Length; i++)
        {
            byte[] nameI = ((NameSelectorNode)selectors[i]).Name;
            for (int j = i + 1; j < selectors.Length; j++)
            {
                byte[] nameJ = ((NameSelectorNode)selectors[j]).Name;
                if (nameI.AsSpan().SequenceEqual(nameJ))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int CountSingletonRun(ImmutableArray<SegmentNode> segments, int start)
    {
        int count = 0;
        for (int i = start; i < segments.Length; i++)
        {
            if (segments[i] is not ChildSegmentNode)
            {
                break;
            }

            if (segments[i].Selectors.Length != 1)
            {
                break;
            }

            if (segments[i].Selectors[0] is not (NameSelectorNode or IndexSelectorNode))
            {
                break;
            }

            count++;
        }

        return count;
    }

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

    private static SingularNav[] BuildSingularSteps(ImmutableArray<SegmentNode> segments)
    {
        SingularNav[] steps = new SingularNav[segments.Length];
        for (int i = 0; i < segments.Length; i++)
        {
            SelectorNode sel = segments[i].Selectors[0];
            steps[i] = sel switch
            {
                NameSelectorNode name => new SingularNav(name.Name),
                IndexSelectorNode idx => new SingularNav(idx.Index),
                _ => throw new JsonPathException("Unexpected selector in singular query."),
            };
        }

        return steps;
    }

    private static ComparisonOp FlipOp(ComparisonOp op)
    {
        return op switch
        {
            ComparisonOp.LessThan => ComparisonOp.GreaterThan,
            ComparisonOp.LessThanOrEqual => ComparisonOp.GreaterThanOrEqual,
            ComparisonOp.GreaterThan => ComparisonOp.LessThan,
            ComparisonOp.GreaterThanOrEqual => ComparisonOp.LessThanOrEqual,
            _ => op,
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
}
