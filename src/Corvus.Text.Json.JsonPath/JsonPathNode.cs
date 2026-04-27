// <copyright file="JsonPathNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// Base class for all JSONPath AST nodes.
/// </summary>
internal abstract class JsonPathNode
{
}

/// <summary>
/// The root query node: <c>$ segments*</c>.
/// </summary>
internal sealed class QueryNode : JsonPathNode
{
    public QueryNode(ImmutableArray<SegmentNode> segments)
    {
        Segments = segments;
    }

    public ImmutableArray<SegmentNode> Segments { get; }
}

/// <summary>
/// Base class for segment nodes: child (<c>.</c> or <c>[...]</c>) and descendant (<c>..</c>).
/// Each segment contains an ordered list of selectors.
/// </summary>
internal abstract class SegmentNode : JsonPathNode
{
    protected SegmentNode(ImmutableArray<SelectorNode> selectors)
    {
        Selectors = selectors;
    }

    public ImmutableArray<SelectorNode> Selectors { get; }
}

/// <summary>
/// A child segment: <c>.</c>name, <c>[</c>selectors<c>]</c>, or <c>.*</c>.
/// </summary>
internal sealed class ChildSegmentNode : SegmentNode
{
    public ChildSegmentNode(ImmutableArray<SelectorNode> selectors)
        : base(selectors)
    {
    }
}

/// <summary>
/// A descendant segment: <c>..</c>name, <c>..[</c>selectors<c>]</c>, or <c>..*</c>.
/// </summary>
internal sealed class DescendantSegmentNode : SegmentNode
{
    public DescendantSegmentNode(ImmutableArray<SelectorNode> selectors)
        : base(selectors)
    {
    }
}

/// <summary>
/// Base class for selector nodes within a segment.
/// </summary>
internal abstract class SelectorNode : JsonPathNode
{
}

/// <summary>
/// A name selector: <c>'name'</c> or dot-notation <c>.name</c>.
/// The <see cref="Name"/> is the raw UTF-8 bytes of the property name (unescaped).
/// </summary>
internal sealed class NameSelectorNode : SelectorNode
{
    public NameSelectorNode(byte[] name)
    {
        Name = name;
    }

    public byte[] Name { get; }
}

/// <summary>
/// A wildcard selector: <c>*</c>.
/// </summary>
internal sealed class WildcardSelectorNode : SelectorNode
{
}

/// <summary>
/// An index selector: <c>[n]</c> with optional negative indexing.
/// </summary>
internal sealed class IndexSelectorNode : SelectorNode
{
    public IndexSelectorNode(long index)
    {
        Index = index;
    }

    public long Index { get; }
}

/// <summary>
/// An array slice selector: <c>[start:end:step]</c>.
/// Null values indicate "omitted" per RFC 9535 semantics.
/// </summary>
internal sealed class SliceSelectorNode : SelectorNode
{
    public SliceSelectorNode(long? start, long? end, long? step)
    {
        Start = start;
        End = end;
        Step = step;
    }

    public long? Start { get; }

    public long? End { get; }

    public long? Step { get; }
}

/// <summary>
/// A filter selector: <c>[?expr]</c>.
/// </summary>
internal sealed class FilterSelectorNode : SelectorNode
{
    public FilterSelectorNode(FilterExpressionNode expression)
    {
        Expression = expression;
    }

    public FilterExpressionNode Expression { get; }
}

// ── Filter expression nodes ──────────────────────────────────────────

/// <summary>
/// Base class for filter expression AST nodes.
/// </summary>
internal abstract class FilterExpressionNode : JsonPathNode
{
}

/// <summary>
/// A comparison expression: <c>left op right</c>.
/// </summary>
internal sealed class ComparisonNode : FilterExpressionNode
{
    public ComparisonNode(FilterExpressionNode left, ComparisonOp op, FilterExpressionNode right)
    {
        Left = left;
        Op = op;
        Right = right;
    }

    public FilterExpressionNode Left { get; }

    public ComparisonOp Op { get; }

    public FilterExpressionNode Right { get; }
}

/// <summary>
/// Comparison operators.
/// </summary>
internal enum ComparisonOp : byte
{
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
}

/// <summary>
/// A logical AND expression: <c>left &amp;&amp; right</c>.
/// </summary>
internal sealed class LogicalAndNode : FilterExpressionNode
{
    public LogicalAndNode(FilterExpressionNode left, FilterExpressionNode right)
    {
        Left = left;
        Right = right;
    }

    public FilterExpressionNode Left { get; }

    public FilterExpressionNode Right { get; }
}

/// <summary>
/// A logical OR expression: <c>left || right</c>.
/// </summary>
internal sealed class LogicalOrNode : FilterExpressionNode
{
    public LogicalOrNode(FilterExpressionNode left, FilterExpressionNode right)
    {
        Left = left;
        Right = right;
    }

    public FilterExpressionNode Left { get; }

    public FilterExpressionNode Right { get; }
}

/// <summary>
/// A logical NOT expression: <c>!expr</c>.
/// </summary>
internal sealed class LogicalNotNode : FilterExpressionNode
{
    public LogicalNotNode(FilterExpressionNode operand)
    {
        Operand = operand;
    }

    public FilterExpressionNode Operand { get; }
}

/// <summary>
/// A filter query: a relative (<c>@</c>) or absolute (<c>$</c>) JSONPath query
/// used inside a filter expression. Evaluates to a node list (existence test)
/// or is wrapped in <c>value()</c> to produce a comparable value.
/// </summary>
internal sealed class FilterQueryNode : FilterExpressionNode
{
    public FilterQueryNode(bool isRelative, ImmutableArray<SegmentNode> segments)
    {
        IsRelative = isRelative;
        Segments = segments;
    }

    /// <summary>
    /// Gets a value indicating whether this is a relative query (<c>@</c>) rather than
    /// an absolute query (<c>$</c>).
    /// </summary>
    public bool IsRelative { get; }

    public ImmutableArray<SegmentNode> Segments { get; }
}

/// <summary>
/// A function call in a filter expression: <c>name(args...)</c>.
/// </summary>
internal sealed class FunctionCallNode : FilterExpressionNode
{
    public FunctionCallNode(string name, ImmutableArray<FilterExpressionNode> arguments)
    {
        Name = name;
        Arguments = arguments;
    }

    public string Name { get; }

    public ImmutableArray<FilterExpressionNode> Arguments { get; }
}

/// <summary>
/// A literal value in a filter expression: string, number, true, false, or null.
/// Stored as a pre-parsed <see cref="JsonElement"/>.
/// </summary>
internal sealed class LiteralNode : FilterExpressionNode
{
    public LiteralNode(JsonElement value)
    {
        Value = value;
    }

    public JsonElement Value { get; }
}

/// <summary>
/// A parenthesized expression: <c>(expr)</c>.
/// </summary>
internal sealed class ParenExpressionNode : FilterExpressionNode
{
    public ParenExpressionNode(FilterExpressionNode inner)
    {
        Inner = inner;
    }

    public FilterExpressionNode Inner { get; }
}
