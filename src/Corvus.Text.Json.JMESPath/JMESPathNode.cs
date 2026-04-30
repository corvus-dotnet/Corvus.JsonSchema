// <copyright file="JMESPathNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Base class for all JMESPath AST nodes.
/// </summary>
internal abstract class JMESPathNode
{
    /// <summary>Gets or sets the byte position in the source expression.</summary>
    public int Position { get; set; }
}

/// <summary>An unquoted or quoted identifier (property access).</summary>
internal sealed class IdentifierNode(byte[] name) : JMESPathNode
{
    /// <summary>Gets the UTF-8 encoded property name.</summary>
    public byte[] Name { get; } = name;
}

/// <summary>The <c>@</c> current node reference.</summary>
internal sealed class CurrentNode : JMESPathNode
{
}

/// <summary>A sub-expression: <c>left.right</c>.</summary>
internal sealed class SubExpressionNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the left-hand expression.</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the right-hand expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>An array index expression: <c>[N]</c>.</summary>
internal sealed class IndexNode : JMESPathNode
{
    /// <summary>Gets or sets the index value (negative for reverse indexing).</summary>
    public int Index { get; set; }
}

/// <summary>An array slice expression: <c>[start:stop:step]</c>.</summary>
internal sealed class SliceNode : JMESPathNode
{
    /// <summary>Gets or sets the start index (null = default).</summary>
    public int? Start { get; set; }

    /// <summary>Gets or sets the stop index (null = default).</summary>
    public int? Stop { get; set; }

    /// <summary>Gets or sets the step value (null = 1).</summary>
    public int? Step { get; set; }
}

/// <summary>A raw string literal: <c>'text'</c>.</summary>
internal sealed class RawStringNode(byte[] value) : JMESPathNode
{
    /// <summary>Gets the UTF-8 encoded raw string value.</summary>
    public byte[] Value { get; } = value;
}

/// <summary>A JSON literal: <c>`json_value`</c>.</summary>
internal sealed class LiteralNode(byte[] jsonValue) : JMESPathNode
{
    /// <summary>Gets the UTF-8 encoded JSON value.</summary>
    public byte[] JsonValue { get; } = jsonValue;
}

/// <summary>The pipe expression: <c>left | right</c>.</summary>
internal sealed class PipeNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the left-hand expression.</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the right-hand expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>The logical OR expression: <c>left || right</c>.</summary>
internal sealed class OrNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the left-hand expression.</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the right-hand expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>The logical AND expression: <c>left &amp;&amp; right</c>.</summary>
internal sealed class AndNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the left-hand expression.</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the right-hand expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>The logical NOT expression: <c>!expression</c>.</summary>
internal sealed class NotNode(JMESPathNode expression) : JMESPathNode
{
    /// <summary>Gets the negated expression.</summary>
    public JMESPathNode Expression { get; } = expression;
}

/// <summary>A comparison expression.</summary>
internal sealed class ComparisonNode(JMESPathNode left, CompareOp op, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the comparison operator.</summary>
    public CompareOp Operator { get; } = op;

    /// <summary>Gets the left-hand expression.</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the right-hand expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>Comparison operators for JMESPath.</summary>
internal enum CompareOp
{
    /// <summary>Equal (==).</summary>
    Equal,

    /// <summary>Not equal (!=).</summary>
    NotEqual,

    /// <summary>Less than (&lt;).</summary>
    LessThan,

    /// <summary>Less than or equal (&lt;=).</summary>
    LessThanOrEqual,

    /// <summary>Greater than (&gt;).</summary>
    GreaterThan,

    /// <summary>Greater than or equal (&gt;=).</summary>
    GreaterThanOrEqual,
}

/// <summary>A multi-select list expression: <c>[expr1, expr2, ...]</c>.</summary>
internal sealed class MultiSelectListNode(JMESPathNode[] expressions) : JMESPathNode
{
    /// <summary>Gets the expressions.</summary>
    public JMESPathNode[] Expressions { get; } = expressions;
}

/// <summary>A multi-select hash expression: <c>{key1: expr1, key2: expr2}</c>.</summary>
internal sealed class MultiSelectHashNode(MultiSelectHashNode.KeyValuePair[] pairs) : JMESPathNode
{
    /// <summary>Gets the key-value pairs.</summary>
    public KeyValuePair[] Pairs { get; } = pairs;

    /// <summary>A key-value pair in a multi-select hash.</summary>
    internal sealed class KeyValuePair(byte[] key, JMESPathNode value)
    {
        /// <summary>Gets the UTF-8 encoded key name.</summary>
        public byte[] Key { get; } = key;

        /// <summary>Gets the value expression.</summary>
        public JMESPathNode Value { get; } = value;
    }
}

/// <summary>
/// A list (array wildcard) projection: <c>left[*].right</c>.
/// Evaluates <c>left</c> to get an array, then projects <c>right</c> onto each element.
/// </summary>
internal sealed class ListProjectionNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the source expression (must evaluate to an array).</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the projected expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>
/// An object wildcard projection: <c>left.*.right</c>.
/// Evaluates <c>left</c> to get an object, then projects <c>right</c> onto each value.
/// </summary>
internal sealed class ValueProjectionNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the source expression (must evaluate to an object).</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the projected expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>
/// A flatten projection: <c>left[].right</c>.
/// Evaluates <c>left</c> to get an array, flattens one level, then projects <c>right</c>.
/// </summary>
internal sealed class FlattenProjectionNode(JMESPathNode left, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the source expression (must evaluate to an array).</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the projected expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>
/// A filter projection: <c>left[?condition].right</c>.
/// Evaluates <c>left</c> to get an array, filters by <c>condition</c>, then projects <c>right</c>.
/// </summary>
internal sealed class FilterProjectionNode(JMESPathNode left, JMESPathNode condition, JMESPathNode right) : JMESPathNode
{
    /// <summary>Gets the source expression (must evaluate to an array).</summary>
    public JMESPathNode Left { get; } = left;

    /// <summary>Gets the filter predicate.</summary>
    public JMESPathNode Condition { get; } = condition;

    /// <summary>Gets the projected expression.</summary>
    public JMESPathNode Right { get; } = right;
}

/// <summary>An expression reference: <c>&amp;expression</c>.</summary>
internal sealed class ExpressionRefNode(JMESPathNode expression) : JMESPathNode
{
    /// <summary>Gets the referenced expression.</summary>
    public JMESPathNode Expression { get; } = expression;
}

/// <summary>A function call expression: <c>name(arg1, arg2, ...)</c>.</summary>
internal sealed class FunctionCallNode(byte[] name, JMESPathNode[] arguments) : JMESPathNode
{
    /// <summary>Gets the UTF-8 encoded function name.</summary>
    public byte[] Name { get; } = name;

    /// <summary>Gets the argument expressions.</summary>
    public JMESPathNode[] Arguments { get; } = arguments;
}
