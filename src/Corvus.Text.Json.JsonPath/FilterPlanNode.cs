// <copyright file="FilterPlanNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.RegularExpressions;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Base class for filter predicate plan nodes. These represent compiled filter
/// expressions evaluated by <see cref="PlanInterpreter"/> to produce
/// <see cref="Compiler.FilterResult"/> values.
/// </summary>
internal abstract class FilterPlanNode
{
}

// ── Logical connectives ──────────────────────────────────────────────

/// <summary>
/// Logical AND with left-to-right short-circuit evaluation.
/// </summary>
internal sealed class FilterLogicalAndPlan : FilterPlanNode
{
    internal FilterLogicalAndPlan(FilterPlanNode left, FilterPlanNode right)
    {
        Left = left;
        Right = right;
    }

    internal FilterPlanNode Left { get; }

    internal FilterPlanNode Right { get; }
}

/// <summary>
/// Logical OR with left-to-right short-circuit evaluation.
/// </summary>
internal sealed class FilterLogicalOrPlan : FilterPlanNode
{
    internal FilterLogicalOrPlan(FilterPlanNode left, FilterPlanNode right)
    {
        Left = left;
        Right = right;
    }

    internal FilterPlanNode Left { get; }

    internal FilterPlanNode Right { get; }
}

/// <summary>
/// Logical NOT.
/// </summary>
internal sealed class FilterLogicalNotPlan : FilterPlanNode
{
    internal FilterLogicalNotPlan(FilterPlanNode operand)
    {
        Operand = operand;
    }

    internal FilterPlanNode Operand { get; }
}

// ── Comparisons ──────────────────────────────────────────────────────

/// <summary>
/// General comparison: evaluate both sides, then compare using
/// <see cref="JsonPathCodeGenHelpers.CompareValues"/>.
/// </summary>
internal sealed class FilterComparisonPlan : FilterPlanNode
{
    internal FilterComparisonPlan(FilterPlanNode left, FilterPlanNode right, ComparisonOp op)
    {
        Left = left;
        Right = right;
        Op = op;
    }

    internal FilterPlanNode Left { get; }

    internal FilterPlanNode Right { get; }

    internal ComparisonOp Op { get; }
}

/// <summary>
/// Specialized comparison: expression vs numeric literal.
/// Avoids boxing through <see cref="JsonPathCodeGenHelpers.CompareValues"/> and
/// eliminates the right-side delegate evaluation.
/// </summary>
internal sealed class FilterNumericComparisonPlan : FilterPlanNode
{
    internal FilterNumericComparisonPlan(FilterPlanNode expr, double literalValue, ComparisonOp op)
    {
        Expr = expr;
        LiteralValue = literalValue;
        Op = op;
    }

    internal FilterPlanNode Expr { get; }

    internal double LiteralValue { get; }

    internal ComparisonOp Op { get; }
}

/// <summary>
/// Most-optimized numeric comparison: singular query navigation (e.g.
/// <c>@.price</c>) vs numeric literal. Inlines the navigation loop and
/// avoids all intermediate <see cref="Compiler.FilterResult"/> boxing.
/// </summary>
internal sealed class FilterSingularNumericPlan : FilterPlanNode
{
    internal FilterSingularNumericPlan(
        bool isRelative,
        SingularNav[] steps,
        double literalValue,
        ComparisonOp op)
    {
        IsRelative = isRelative;
        Steps = steps;
        LiteralValue = literalValue;
        Op = op;
    }

    internal bool IsRelative { get; }

    internal SingularNav[] Steps { get; }

    internal double LiteralValue { get; }

    internal ComparisonOp Op { get; }
}

/// <summary>
/// Specialized comparison: <c>length(expr) op number</c>.
/// </summary>
internal sealed class FilterLengthNumericPlan : FilterPlanNode
{
    internal FilterLengthNumericPlan(FilterPlanNode argument, double literalValue, ComparisonOp op)
    {
        Argument = argument;
        LiteralValue = literalValue;
        Op = op;
    }

    internal FilterPlanNode Argument { get; }

    internal double LiteralValue { get; }

    internal ComparisonOp Op { get; }
}

/// <summary>
/// Specialized comparison: expression vs <c>null</c>, <c>true</c>, or <c>false</c>.
/// Only checks <see cref="JsonValueKind"/>, avoiding full value comparison.
/// </summary>
internal sealed class FilterKindComparisonPlan : FilterPlanNode
{
    internal FilterKindComparisonPlan(FilterPlanNode expr, JsonValueKind targetKind, ComparisonOp op)
    {
        Expr = expr;
        TargetKind = targetKind;
        Op = op;
    }

    internal FilterPlanNode Expr { get; }

    internal JsonValueKind TargetKind { get; }

    internal ComparisonOp Op { get; }
}

/// <summary>
/// Specialized comparison: expression <c>==</c> or <c>!=</c> a string literal.
/// Uses <see cref="JsonPathCodeGenHelpers.DeepEquals"/> for the value comparison.
/// </summary>
internal sealed class FilterStringEqualityPlan : FilterPlanNode
{
    internal FilterStringEqualityPlan(FilterPlanNode expr, JsonElement literalElement, ComparisonOp op)
    {
        Expr = expr;
        LiteralElement = literalElement;
        Op = op;
    }

    internal FilterPlanNode Expr { get; }

    internal JsonElement LiteralElement { get; }

    internal ComparisonOp Op { get; }
}

// ── Query nodes (inside filters) ─────────────────────────────────────

/// <summary>
/// Singular query inside a filter: <c>@.a.b</c> or <c>$[0].name</c>.
/// Navigates a chain of name/index steps with no allocation.
/// </summary>
internal sealed class FilterSingularQueryPlan : FilterPlanNode
{
    internal FilterSingularQueryPlan(bool isRelative, SingularNav[] steps)
    {
        IsRelative = isRelative;
        Steps = steps;
    }

    internal bool IsRelative { get; }

    internal SingularNav[] Steps { get; }
}

/// <summary>
/// Empty query with no segments: <c>@</c> or <c>$</c> (returns the root/current
/// node itself as a single-element node list).
/// </summary>
internal sealed class FilterEmptyQueryPlan : FilterPlanNode
{
    internal FilterEmptyQueryPlan(bool isRelative)
    {
        IsRelative = isRelative;
    }

    internal bool IsRelative { get; }
}

/// <summary>
/// General (non-singular) query inside a filter. Evaluates the inner plan
/// using a pooled <see cref="JsonPathResult"/> buffer.
/// </summary>
internal sealed class FilterGeneralQueryPlan : FilterPlanNode
{
    internal FilterGeneralQueryPlan(bool isRelative, PlanNode innerPlan)
    {
        IsRelative = isRelative;
        InnerPlan = innerPlan;
    }

    internal bool IsRelative { get; }

    internal PlanNode InnerPlan { get; }
}

// ── Literals ─────────────────────────────────────────────────────────

/// <summary>
/// A literal JSON value inside a filter expression.
/// </summary>
internal sealed class FilterLiteralPlan : FilterPlanNode
{
    internal FilterLiteralPlan(JsonElement value)
    {
        Value = value;
    }

    internal JsonElement Value { get; }
}

// ── Functions ────────────────────────────────────────────────────────

/// <summary>
/// <c>length(arg)</c> function: returns the length of a string, array, or object.
/// </summary>
internal sealed class FilterLengthFunctionPlan : FilterPlanNode
{
    internal FilterLengthFunctionPlan(FilterPlanNode argument)
    {
        Argument = argument;
    }

    internal FilterPlanNode Argument { get; }
}

/// <summary>
/// <c>count(arg)</c> function: returns the number of nodes in a node list.
/// </summary>
internal sealed class FilterCountFunctionPlan : FilterPlanNode
{
    internal FilterCountFunctionPlan(FilterPlanNode argument)
    {
        Argument = argument;
    }

    internal FilterPlanNode Argument { get; }
}

/// <summary>
/// <c>value(arg)</c> function: extracts a single value from a node list.
/// </summary>
internal sealed class FilterValueFunctionPlan : FilterPlanNode
{
    internal FilterValueFunctionPlan(FilterPlanNode argument)
    {
        Argument = argument;
    }

    internal FilterPlanNode Argument { get; }
}

/// <summary>
/// <c>match(str, pattern)</c> or <c>search(str, pattern)</c> function.
/// When the pattern is a string literal, the regex is pre-compiled at plan
/// creation time.
/// </summary>
internal sealed class FilterMatchFunctionPlan : FilterPlanNode
{
    internal FilterMatchFunctionPlan(
        FilterPlanNode stringArg,
        Regex? precompiledRegex,
        FilterPlanNode? dynamicPatternArg,
        bool fullMatch)
    {
        StringArg = stringArg;
        PrecompiledRegex = precompiledRegex;
        DynamicPatternArg = dynamicPatternArg;
        FullMatch = fullMatch;
    }

    /// <summary>Gets the argument that produces the string to test.</summary>
    internal FilterPlanNode StringArg { get; }

    /// <summary>Gets the pre-compiled regex, or null for dynamic patterns.</summary>
    internal Regex? PrecompiledRegex { get; }

    /// <summary>Gets the dynamic pattern argument, or null if pre-compiled.</summary>
    internal FilterPlanNode? DynamicPatternArg { get; }

    /// <summary>Gets a value indicating whether this is <c>match()</c> (true) or <c>search()</c> (false).</summary>
    internal bool FullMatch { get; }
}

/// <summary>
/// A custom function call. Stores the <see cref="IJsonPathFunction"/> reference directly
/// so the plan interpreter can dispatch without a registry lookup.
/// </summary>
internal sealed class FilterCustomFunctionPlan : FilterPlanNode
{
    internal FilterCustomFunctionPlan(
        string name,
        IJsonPathFunction function,
        FilterPlanNode[] arguments,
        JsonPathFunctionType[] parameterTypes)
    {
        Name = name;
        Function = function;
        Arguments = arguments;
        ParameterTypes = parameterTypes;
    }

    /// <summary>Gets the function name (for diagnostics).</summary>
    internal string Name { get; }

    /// <summary>Gets the function implementation.</summary>
    internal IJsonPathFunction Function { get; }

    /// <summary>Gets the planned argument nodes.</summary>
    internal FilterPlanNode[] Arguments { get; }

    /// <summary>Gets the declared parameter types (parallel to <see cref="Arguments"/>).</summary>
    internal JsonPathFunctionType[] ParameterTypes { get; }
}
