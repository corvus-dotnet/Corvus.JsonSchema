// <copyright file="PlanInterpreter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Interprets an execution plan tree (<see cref="PlanNode"/>) against JSON data,
/// writing matched nodes into a <see cref="JsonPathResult"/>. This is a static
/// helper class — all state is passed as parameters, making it fully thread-safe.
/// </summary>
/// <remarks>
/// <para>
/// The interpreter dispatches on the concrete <see cref="PlanNode"/> type at each
/// step. Plan nodes are immutable and cached; the interpreter allocates nothing on
/// the managed heap for the common case (no regex, result buffer doesn't overflow).
/// </para>
/// </remarks>
internal static class PlanInterpreter
{
    private static readonly ConcurrentDictionary<string, Regex?> RegexCache = new();

    /// <summary>
    /// Executes the plan tree starting from the root of the JSON document.
    /// </summary>
    /// <param name="plan">The plan node tree to execute.</param>
    /// <param name="root">The root JSON element (for absolute queries).</param>
    /// <param name="result">The result buffer to append matched nodes to.</param>
    internal static void Execute(PlanNode plan, in JsonElement root, ref JsonPathResult result)
    {
        ExecuteStep(plan, root, root, ref result);
    }

    /// <summary>
    /// Executes a single plan step against the current JSON element.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void ExecuteStep(PlanNode plan, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        switch (plan)
        {
            case EmitStep:
                result.Append(current);
                break;

            case NavigateNameStep name:
                ExecuteNavigateName(name, root, current, ref result);
                break;

            case NavigateIndexStep idx:
                ExecuteNavigateIndex(idx, root, current, ref result);
                break;

            case SingletonChainStep chain:
                ExecuteSingletonChain(chain, root, current, ref result);
                break;

            case NameSetStep nameSet:
                ExecuteNameSet(nameSet, root, current, ref result);
                break;

            case WildcardNameStep wildcardName:
                ExecuteWildcardName(wildcardName, root, current, ref result);
                break;

            case WildcardStep wildcard:
                ExecuteWildcard(wildcard, root, current, ref result);
                break;

            case SliceStep slice:
                ExecuteSlice(slice, root, current, ref result);
                break;

            case FilterSingularNumericStep filterNum:
                ExecuteFilterSingularNumeric(filterNum, root, current, ref result);
                break;

            case FilterStep filter:
                ExecuteFilter(filter, root, current, ref result);
                break;

            case MultiSelectorStep multi:
                ExecuteMultiSelector(multi, root, current, ref result);
                break;

            case DescendantNameStep descName:
                ExecuteDescendantName(descName, root, current, ref result);
                break;

            case DescendantNameSetStep descNameSet:
                ExecuteDescendantNameSet(descNameSet, root, current, ref result);
                break;

            case DescendantStep desc:
                ExecuteDescendant(desc, root, current, ref result);
                break;

            default:
                throw new JsonPathException($"Unknown plan node type: {plan.GetType().Name}");
        }
    }

    // ── Singleton navigation ─────────────────────────────────────────
    private static void ExecuteNavigateName(NavigateNameStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind == JsonValueKind.Object)
        {
            current.EnsurePropertyMap();
            if (current.TryGetProperty(step.Utf8Name, out JsonElement value))
            {
                ExecuteStep(step.Continuation, root, value, ref result);
            }
        }
    }

    private static void ExecuteNavigateIndex(NavigateIndexStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int len = current.GetArrayLength();
        long resolved = step.Index >= 0 ? step.Index : len + step.Index;
        if (resolved >= 0 && resolved < len)
        {
            ExecuteStep(step.Continuation, root, current[(int)resolved], ref result);
        }
    }

    private static void ExecuteSingletonChain(SingletonChainStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        JsonElement node = current;
        SingularNav[] steps = step.Steps;

        for (int i = 0; i < steps.Length; i++)
        {
            ref readonly SingularNav nav = ref steps[i];
            if (nav.IsName)
            {
                if (node.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                node.EnsurePropertyMap();
                if (!node.TryGetProperty(nav.Utf8Name!, out node))
                {
                    return;
                }
            }
            else
            {
                if (node.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                int len = node.GetArrayLength();
                long resolved = nav.Index >= 0 ? nav.Index : len + nav.Index;
                if (resolved < 0 || resolved >= len)
                {
                    return;
                }

                node = node[(int)resolved];
            }
        }

        ExecuteStep(step.Continuation, root, node, ref result);
    }

    // ── Fused multi-name enumeration ─────────────────────────────────
    private static void ExecuteNameSet(NameSetStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        NameSetEntry[] entries = step.Entries;
        int count = entries.Length;
        NameDispatchTable dispatch = step.Dispatch;

        // Rent a slot array for matched values. JsonElement contains managed
        // references so we can't stackalloc.
        JsonElement[]? rentedSlots = ArrayPool<JsonElement>.Shared.Rent(count);
        try
        {
            // Track which slots have been filled using a bitmask for small sets.
            // For >64 entries (extremely rare in JSONPath), we'd need a different
            // approach, but union selectors with 64+ names are practically unheard of.
            ulong foundMask = 0;

            // Enumerate the object ONCE. For each property, hash-dispatch to find
            // if it matches any wanted name. Overwrite on subsequent matches so the
            // last occurrence wins (matching TryGetProperty semantics for duplicate
            // JSON property names).
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                using UnescapedUtf8JsonString nameSpan = prop.Utf8NameSpan;
                if (dispatch.TryGetSlotIndex(nameSpan.Span, out int slot))
                {
                    rentedSlots[slot] = prop.Value;
                    foundMask |= 1UL << slot;
                }
            }

            // Emit results in selector declaration order.
            for (int i = 0; i < count; i++)
            {
                if ((foundMask & (1UL << i)) != 0)
                {
                    ExecuteStep(entries[i].Continuation, root, rentedSlots[i], ref result);
                }
            }
        }
        finally
        {
            // Clear the rented slots to avoid retaining IJsonDocument references.
            Array.Clear(rentedSlots, 0, count);
            ArrayPool<JsonElement>.Shared.Return(rentedSlots);
        }
    }

    // ── Iteration steps ──────────────────────────────────────────────
    private static void ExecuteWildcard(WildcardStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                ExecuteStep(step.Body, root, prop.Value, ref result);
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in current.EnumerateArray())
            {
                ExecuteStep(step.Body, root, item, ref result);
            }
        }
    }

    /// <summary>
    /// Fused wildcard + name navigation: iterates children and for each object
    /// child looks up the named property directly, avoiding per-element type
    /// dispatch through <see cref="ExecuteStep"/>.
    /// </summary>
    private static void ExecuteWildcardName(WildcardNameStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind == JsonValueKind.Array)
        {
            byte[] utf8Name = step.Utf8Name;
            foreach (JsonElement item in current.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty(utf8Name, out JsonElement value))
                {
                    ExecuteStep(step.Continuation, root, value, ref result);
                }
            }
        }
        else if (current.ValueKind == JsonValueKind.Object)
        {
            byte[] utf8Name = step.Utf8Name;
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                JsonElement child = prop.Value;
                if (child.ValueKind == JsonValueKind.Object && child.TryGetProperty(utf8Name, out JsonElement value))
                {
                    ExecuteStep(step.Continuation, root, value, ref result);
                }
            }
        }
    }

    private static void ExecuteSlice(SliceStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int len = current.GetArrayLength();
        long s = step.Step ?? 1;

        if (s == 0)
        {
            return;
        }

        if (s > 0)
        {
            long lower = NormalizeSliceIndex(step.Start ?? 0, len);
            long upper = NormalizeSliceIndex(step.End ?? len, len);

            lower = Math.Max(lower, 0);
            upper = Math.Min(upper, len);

            for (long i = lower; i < upper; i += s)
            {
                ExecuteStep(step.Body, root, current[(int)i], ref result);
            }
        }
        else
        {
            long lower = NormalizeSliceIndex(step.End ?? -len - 1, len);
            long upper = NormalizeSliceIndex(step.Start ?? len - 1, len);

            upper = Math.Min(upper, len - 1);
            lower = Math.Max(lower, -1);

            for (long i = upper; i > lower; i += s)
            {
                ExecuteStep(step.Body, root, current[(int)i], ref result);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long NormalizeSliceIndex(long index, int len)
    {
        return index >= 0 ? index : len + index;
    }

    private static void ExecuteFilter(FilterStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in current.EnumerateArray())
            {
                if (EvalFilterAsTruthy(step.Predicate, root, item))
                {
                    ExecuteStep(step.Body, root, item, ref result);
                }
            }
        }
        else if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                if (EvalFilterAsTruthy(step.Predicate, root, prop.Value))
                {
                    ExecuteStep(step.Body, root, prop.Value, ref result);
                }
            }
        }
    }

    /// <summary>
    /// Fused filter for the common <c>[?@.prop OP number]</c> pattern.
    /// Inlines the singular path navigation and numeric comparison directly
    /// into the iteration loop, avoiding <see cref="EvalFilter"/> dispatch.
    /// </summary>
    private static void ExecuteFilterSingularNumeric(FilterSingularNumericStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        SingularNav[] navSteps = step.Steps;
        double literal = step.LiteralValue;
        ComparisonOp op = step.Op;

        if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in current.EnumerateArray())
            {
                if (EvalSingularNumericInline(navSteps, op, literal, root, item))
                {
                    ExecuteStep(step.Body, root, item, ref result);
                }
            }
        }
        else if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                JsonElement val = prop.Value;
                if (EvalSingularNumericInline(navSteps, op, literal, root, val))
                {
                    ExecuteStep(step.Body, root, val, ref result);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvalSingularNumericInline(
        SingularNav[] steps,
        ComparisonOp op,
        double literal,
        in JsonElement root,
        in JsonElement current)
    {
        if (TryNavigateSingularPath(steps, true, root, current, out JsonElement node) &&
            node.ValueKind == JsonValueKind.Number)
        {
            return CompareDouble(node.GetDouble(), literal, op);
        }

        // Path not found or non-numeric: only NotEqual returns true.
        return op == ComparisonOp.NotEqual;
    }

    private static void ExecuteMultiSelector(MultiSelectorStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        PlanNode[] selectors = step.Selectors;
        for (int i = 0; i < selectors.Length; i++)
        {
            ExecuteStep(selectors[i], root, current, ref result);
        }
    }

    // ── Descendant traversal ─────────────────────────────────────────
    private static void ExecuteDescendantName(DescendantNameStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        if (current.ValueKind == JsonValueKind.Object)
        {
            // Single-pass: enumerate once, checking each property name and
            // recursing into container children in the same iteration.
            byte[] utf8Name = step.Utf8Name;
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                if (prop.NameEquals(utf8Name))
                {
                    ExecuteStep(step.Continuation, root, prop.Value, ref result);
                }

                JsonElement child = prop.Value;
                if (child.ValueKind == JsonValueKind.Object || child.ValueKind == JsonValueKind.Array)
                {
                    ExecuteDescendantName(step, root, child, ref result);
                }
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in current.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                {
                    ExecuteDescendantName(step, root, item, ref result);
                }
            }
        }
    }

    private static void ExecuteDescendantNameSet(DescendantNameSetStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        NameSetEntry[] entries = step.Entries;
        int count = entries.Length;
        NameDispatchTable dispatch = step.Dispatch;

        if (current.ValueKind == JsonValueKind.Object)
        {
            // At this object node, enumerate once with hash dispatch.
            JsonElement[]? rentedSlots = ArrayPool<JsonElement>.Shared.Rent(count);
            try
            {
                ulong foundMask = 0;

                foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
                {
                    using UnescapedUtf8JsonString nameSpan = prop.Utf8NameSpan;
                    if (dispatch.TryGetSlotIndex(nameSpan.Span, out int slot))
                    {
                        rentedSlots[slot] = prop.Value;
                        foundMask |= 1UL << slot;
                    }
                }

                // Emit in selector order.
                for (int i = 0; i < count; i++)
                {
                    if ((foundMask & (1UL << i)) != 0)
                    {
                        ExecuteStep(entries[i].Continuation, root, rentedSlots[i], ref result);
                    }
                }
            }
            finally
            {
                Array.Clear(rentedSlots, 0, count);
                ArrayPool<JsonElement>.Shared.Return(rentedSlots);
            }

            // Recurse into all children (document order).
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                JsonElement child = prop.Value;
                if (child.ValueKind == JsonValueKind.Object || child.ValueKind == JsonValueKind.Array)
                {
                    ExecuteDescendantNameSet(step, root, child, ref result);
                }
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in current.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.Array)
                {
                    ExecuteDescendantNameSet(step, root, item, ref result);
                }
            }
        }
    }

    private static void ExecuteDescendant(DescendantStep step, in JsonElement root, in JsonElement current, ref JsonPathResult result)
    {
        // Apply all selectors at this node.
        PlanNode[] selectorPlans = step.SelectorPlans;
        for (int i = 0; i < selectorPlans.Length; i++)
        {
            ExecuteStep(selectorPlans[i], root, current, ref result);
        }

        // Recurse into container children.
        if (current.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> prop in current.EnumerateObject())
            {
                ExecuteDescendant(step, root, prop.Value, ref result);
            }
        }
        else if (current.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in current.EnumerateArray())
            {
                ExecuteDescendant(step, root, item, ref result);
            }
        }
    }

    // ── Filter expression evaluation ─────────────────────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvalFilterAsTruthy(FilterPlanNode plan, in JsonElement root, in JsonElement current)
    {
        FilterResult result = EvalFilter(plan, root, current);
        return result.AsTruthy();
    }

    private static FilterResult EvalFilter(FilterPlanNode plan, in JsonElement root, in JsonElement current)
    {
        return plan switch
        {
            FilterLogicalAndPlan and => EvalLogicalAnd(and, root, current),
            FilterLogicalOrPlan or => EvalLogicalOr(or, root, current),
            FilterLogicalNotPlan not => EvalLogicalNot(not, root, current),
            FilterComparisonPlan cmp => EvalComparison(cmp, root, current),
            FilterNumericComparisonPlan num => EvalNumericComparison(num, root, current),
            FilterSingularNumericPlan sing => EvalSingularNumericComparison(sing, root, current),
            FilterLengthNumericPlan len => EvalLengthNumericComparison(len, root, current),
            FilterKindComparisonPlan kind => EvalKindComparison(kind, root, current),
            FilterStringEqualityPlan str => EvalStringEquality(str, root, current),
            FilterSingularQueryPlan sq => EvalSingularQuery(sq, root, current),
            FilterEmptyQueryPlan eq => EvalEmptyQuery(eq, root, current),
            FilterGeneralQueryPlan gq => EvalGeneralQuery(gq, root, current),
            FilterLiteralPlan lit => FilterResult.FromValue(lit.Value),
            FilterLengthFunctionPlan lenFn => EvalLengthFunction(lenFn, root, current),
            FilterCountFunctionPlan countFn => EvalCountFunction(countFn, root, current),
            FilterValueFunctionPlan valFn => EvalValueFunction(valFn, root, current),
            FilterMatchFunctionPlan matchFn => EvalMatchFunction(matchFn, root, current),
            _ => throw new JsonPathException($"Unknown filter plan node type: {plan.GetType().Name}"),
        };
    }

    // ── Logical connectives ──────────────────────────────────────────
    private static FilterResult EvalLogicalAnd(FilterLogicalAndPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult left = EvalFilter(plan.Left, root, current);
        if (!left.AsTruthy())
        {
            return FilterResult.FromLogical(false);
        }

        FilterResult right = EvalFilter(plan.Right, root, current);
        return FilterResult.FromLogical(right.AsTruthy());
    }

    private static FilterResult EvalLogicalOr(FilterLogicalOrPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult left = EvalFilter(plan.Left, root, current);
        if (left.AsTruthy())
        {
            return FilterResult.FromLogical(true);
        }

        FilterResult right = EvalFilter(plan.Right, root, current);
        return FilterResult.FromLogical(right.AsTruthy());
    }

    private static FilterResult EvalLogicalNot(FilterLogicalNotPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult operand = EvalFilter(plan.Operand, root, current);
        return FilterResult.FromLogical(!operand.AsTruthy());
    }

    // ── Comparisons ──────────────────────────────────────────────────
    private static FilterResult EvalComparison(FilterComparisonPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult left = EvalFilter(plan.Left, root, current).AsComparable();
        FilterResult right = EvalFilter(plan.Right, root, current).AsComparable();
        return FilterResult.FromLogical(
            JsonPathCodeGenHelpers.CompareValues(left.Value, right.Value, (int)plan.Op));
    }

    private static FilterResult EvalNumericComparison(FilterNumericComparisonPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult exprResult = EvalFilter(plan.Expr, root, current).AsComparable();
        if (exprResult.Kind != FilterResult.FilterResultKind.ValueType ||
            exprResult.Value.ValueKind != JsonValueKind.Number)
        {
            return FilterResult.FromLogical(plan.Op == ComparisonOp.NotEqual);
        }

        return FilterResult.FromLogical(CompareDouble(exprResult.Value.GetDouble(), plan.LiteralValue, plan.Op));
    }

    private static FilterResult EvalSingularNumericComparison(FilterSingularNumericPlan plan, in JsonElement root, in JsonElement current)
    {
        if (!TryNavigateSingularPath(plan.Steps, plan.IsRelative, root, current, out JsonElement node))
        {
            return FilterResult.FromLogical(plan.Op == ComparisonOp.NotEqual);
        }

        if (node.ValueKind != JsonValueKind.Number)
        {
            return FilterResult.FromLogical(plan.Op == ComparisonOp.NotEqual);
        }

        return FilterResult.FromLogical(CompareDouble(node.GetDouble(), plan.LiteralValue, plan.Op));
    }

    private static FilterResult EvalLengthNumericComparison(FilterLengthNumericPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult argResult = EvalFilter(plan.Argument, root, current).AsComparable();
        if (argResult.Kind != FilterResult.FilterResultKind.ValueType)
        {
            return FilterResult.FromLogical(plan.Op == ComparisonOp.NotEqual);
        }

        int len = JsonPathCodeGenHelpers.LengthValue(argResult.Value);
        if (len < 0)
        {
            return FilterResult.FromLogical(plan.Op == ComparisonOp.NotEqual);
        }

        return FilterResult.FromLogical(CompareDouble(len, plan.LiteralValue, plan.Op));
    }

    private static FilterResult EvalKindComparison(FilterKindComparisonPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult exprResult = EvalFilter(plan.Expr, root, current).AsComparable();
        JsonValueKind vk = exprResult.Kind == FilterResult.FilterResultKind.ValueType
            ? exprResult.Value.ValueKind
            : JsonValueKind.Undefined;

        bool matched = plan.Op switch
        {
            ComparisonOp.Equal or ComparisonOp.LessThanOrEqual or ComparisonOp.GreaterThanOrEqual
                => vk == plan.TargetKind,
            ComparisonOp.NotEqual => vk != plan.TargetKind,
            _ => false,
        };
        return FilterResult.FromLogical(matched);
    }

    private static FilterResult EvalStringEquality(FilterStringEqualityPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult exprResult = EvalFilter(plan.Expr, root, current).AsComparable();
        if (exprResult.Kind != FilterResult.FilterResultKind.ValueType)
        {
            return FilterResult.FromLogical(plan.Op == ComparisonOp.NotEqual);
        }

        bool eq = JsonPathCodeGenHelpers.DeepEquals(exprResult.Value, plan.LiteralElement);
        return FilterResult.FromLogical(plan.Op == ComparisonOp.Equal ? eq : !eq);
    }

    // ── Filter queries ───────────────────────────────────────────────
    private static FilterResult EvalSingularQuery(FilterSingularQueryPlan plan, in JsonElement root, in JsonElement current)
    {
        if (!TryNavigateSingularPath(plan.Steps, plan.IsRelative, root, current, out JsonElement node))
        {
            return FilterResult.FromNodes(0, default);
        }

        return FilterResult.FromNodes(1, node);
    }

    private static FilterResult EvalEmptyQuery(FilterEmptyQueryPlan plan, in JsonElement root, in JsonElement current)
    {
        JsonElement target = plan.IsRelative ? current : root;
        return FilterResult.FromNodes(1, target);
    }

    private static FilterResult EvalGeneralQuery(FilterGeneralQueryPlan plan, in JsonElement root, in JsonElement current)
    {
        JsonElement target = plan.IsRelative ? current : root;
        JsonPathResult innerResult = JsonPathResult.CreatePooled(16);
        try
        {
            ExecuteStep(plan.InnerPlan, root, target, ref innerResult);
            return FilterResult.FromNodes(innerResult.Count, innerResult.Count > 0 ? innerResult[0] : default);
        }
        finally
        {
            innerResult.Dispose();
        }
    }

    // ── Functions ────────────────────────────────────────────────────
    private static FilterResult EvalLengthFunction(FilterLengthFunctionPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult argResult = EvalFilter(plan.Argument, root, current).AsComparable();
        if (argResult.Kind != FilterResult.FilterResultKind.ValueType)
        {
            return FilterResult.NothingResult;
        }

        int len = JsonPathCodeGenHelpers.LengthValue(argResult.Value);
        if (len < 0)
        {
            return FilterResult.NothingResult;
        }

        return FilterResult.FromValue(JsonPathCodeGenHelpers.IntToElement(len));
    }

    private static FilterResult EvalCountFunction(FilterCountFunctionPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult argResult = EvalFilter(plan.Argument, root, current);
        if (argResult.Kind != FilterResult.FilterResultKind.NodesType)
        {
            return FilterResult.NothingResult;
        }

        return FilterResult.FromValue(JsonPathCodeGenHelpers.IntToElement(argResult.NodeCount));
    }

    private static FilterResult EvalValueFunction(FilterValueFunctionPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult argResult = EvalFilter(plan.Argument, root, current);
        if (argResult.Kind != FilterResult.FilterResultKind.NodesType)
        {
            return FilterResult.NothingResult;
        }

        if (argResult.NodeCount == 1)
        {
            return FilterResult.FromValue(argResult.FirstNode);
        }

        return FilterResult.NothingResult;
    }

    private static FilterResult EvalMatchFunction(FilterMatchFunctionPlan plan, in JsonElement root, in JsonElement current)
    {
        FilterResult strResult = EvalFilter(plan.StringArg, root, current).AsComparable();

        if (plan.PrecompiledRegex is not null)
        {
            if (strResult.Kind != FilterResult.FilterResultKind.ValueType ||
                strResult.Value.ValueKind != JsonValueKind.String)
            {
                return FilterResult.NothingResult;
            }

            return FilterResult.FromLogical(MatchInput(plan.PrecompiledRegex, strResult.Value));
        }

        // Dynamic pattern
        FilterResult patResult = EvalFilter(plan.DynamicPatternArg!, root, current).AsComparable();

        if (strResult.Kind != FilterResult.FilterResultKind.ValueType ||
            patResult.Kind != FilterResult.FilterResultKind.ValueType ||
            strResult.Value.ValueKind != JsonValueKind.String ||
            patResult.Value.ValueKind != JsonValueKind.String)
        {
            return FilterResult.NothingResult;
        }

        string pattern = patResult.Value.GetString()!;
        string cacheKey = plan.FullMatch ? "m:" + pattern : "s:" + pattern;

        Regex? regex = RegexCache.GetOrAdd(cacheKey, _ => CompileRegex(pattern, plan.FullMatch));

        if (regex is null)
        {
            return FilterResult.NothingResult;
        }

        return FilterResult.FromLogical(MatchInput(regex, strResult.Value));
    }

    // ── Shared helpers ───────────────────────────────────────────────
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryNavigateSingularPath(
        SingularNav[] steps,
        bool isRelative,
        in JsonElement root,
        in JsonElement current,
        out JsonElement result)
    {
        result = isRelative ? current : root;
        for (int i = 0; i < steps.Length; i++)
        {
            ref readonly SingularNav step = ref steps[i];
            if (step.IsName)
            {
                if (result.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                // No EnsurePropertyMap here: filter evaluation typically visits each
                // element once, so the cost of building a property map exceeds the
                // savings from hash lookup on small objects.
                if (!result.TryGetProperty(step.Utf8Name!, out result))
                {
                    return false;
                }
            }
            else
            {
                if (result.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                int len = result.GetArrayLength();
                long resolved = step.Index >= 0 ? step.Index : len + step.Index;
                if (resolved < 0 || resolved >= len)
                {
                    return false;
                }

                result = result[(int)resolved];
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CompareDouble(double left, double right, ComparisonOp op)
    {
        return op switch
        {
            ComparisonOp.Equal => left == right,
            ComparisonOp.NotEqual => left != right,
            ComparisonOp.LessThan => left < right,
            ComparisonOp.LessThanOrEqual => left <= right,
            ComparisonOp.GreaterThan => left > right,
            ComparisonOp.GreaterThanOrEqual => left >= right,
            _ => false,
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

    // ── FilterResult (shared between PlanInterpreter) ────────────────
    internal readonly struct FilterResult
    {
        private FilterResult(FilterResultKind kind, bool logical, JsonElement value, int nodeCount, JsonElement firstNode)
        {
            Kind = kind;
            Logical = logical;
            Value = value;
            NodeCount = nodeCount;
            FirstNode = firstNode;
        }

        internal enum FilterResultKind : byte
        {
            LogicalType,
            ValueType,
            NodesType,
            Nothing,
        }

        internal FilterResultKind Kind { get; }

        internal bool Logical { get; }

        internal JsonElement Value { get; }

        internal int NodeCount { get; }

        internal JsonElement FirstNode { get; }

        internal static FilterResult FromLogical(bool value) =>
            new(FilterResultKind.LogicalType, value, default, 0, default);

        internal static FilterResult FromValue(JsonElement value) =>
            new(FilterResultKind.ValueType, false, value, 0, default);

        internal static FilterResult FromNodes(int count, JsonElement firstNode) =>
            new(FilterResultKind.NodesType, false, default, count, firstNode);

        internal static FilterResult NothingResult { get; } =
            new(FilterResultKind.Nothing, false, default, 0, default);

        internal bool AsTruthy()
        {
            return Kind switch
            {
                FilterResultKind.LogicalType => Logical,
                FilterResultKind.NodesType => NodeCount > 0,
                _ => false,
            };
        }

        internal FilterResult AsComparable()
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
}
