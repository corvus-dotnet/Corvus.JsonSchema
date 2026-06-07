// <copyright file="SimpleConditionEvaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// Compiles and evaluates an Arazzo <c>simple</c> criterion condition.
/// </summary>
/// <remarks>
/// <para>
/// The condition is parsed <em>once</em> (via <see cref="Compile"/>) into an immutable expression
/// tree; <see cref="Evaluate"/> then walks that tree without re-parsing, resolving operands to
/// struct <see cref="Comparand"/> values. Supported grammar:
/// </para>
/// <code>
/// or         := and ( "||" and )*
/// and        := comparison ( "&amp;&amp;" comparison )*
/// comparison := "(" or ")" | operand ( ( "==" | "!=" | "&lt;" | "&lt;=" | "&gt;" | "&gt;=" ) operand )?
/// operand    := runtimeExpression | number | "'string'" | "\"string\"" | "true" | "false" | "null"
/// </code>
/// <para>
/// A lone operand evaluates true only if it resolves to boolean <c>true</c>. Any comparison
/// involving an unresolved operand evaluates to <see langword="false"/>. Ordering operators
/// (<c>&lt; &lt;= &gt; &gt;=</c>) apply to numbers only.
/// </para>
/// </remarks>
public sealed class SimpleConditionEvaluator
{
    private readonly Node root;

    private SimpleConditionEvaluator(Node root) => this.root = root;

    private enum Op
    {
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    /// <summary>
    /// Compiles a <c>simple</c> condition into a reusable evaluator.
    /// </summary>
    /// <param name="condition">The condition string.</param>
    /// <returns>The compiled evaluator.</returns>
    /// <exception cref="FormatException">The condition is malformed.</exception>
    public static SimpleConditionEvaluator Compile(string condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var parser = new Parser(condition);
        Node node = parser.ParseExpression();
        parser.ExpectEnd();
        return new SimpleConditionEvaluator(node);
    }

    /// <summary>
    /// Evaluates the condition against the supplied context.
    /// </summary>
    /// <param name="context">The workflow execution context.</param>
    /// <returns>The boolean result of the condition.</returns>
    public bool Evaluate(WorkflowExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return this.root.Evaluate(context);
    }

    private abstract class Node
    {
        public abstract bool Evaluate(WorkflowExecutionContext context);
    }

    private sealed class OrNode(Node left, Node right) : Node
    {
        public override bool Evaluate(WorkflowExecutionContext context)
            => left.Evaluate(context) || right.Evaluate(context);
    }

    private sealed class AndNode(Node left, Node right) : Node
    {
        public override bool Evaluate(WorkflowExecutionContext context)
            => left.Evaluate(context) && right.Evaluate(context);
    }

    private sealed class TruthyNode(Operand operand) : Node
    {
        public override bool Evaluate(WorkflowExecutionContext context)
        {
            Comparand value = operand.Resolve(context);
            return value.Kind == ComparandKind.Boolean && value.Boolean;
        }
    }

    private sealed class ComparisonNode(Operand left, Op op, Operand right) : Node
    {
        public override bool Evaluate(WorkflowExecutionContext context)
        {
            Comparand l = left.Resolve(context);
            Comparand r = right.Resolve(context);

            if (l.Kind == ComparandKind.Undefined || r.Kind == ComparandKind.Undefined)
            {
                return false;
            }

            switch (op)
            {
                case Op.Equal:
                    return l.ValueEquals(r);
                case Op.NotEqual:
                    return !l.ValueEquals(r);
                default:
                    if (!l.TryCompareNumeric(r, out int c))
                    {
                        return false;
                    }

                    return op switch
                    {
                        Op.LessThan => c < 0,
                        Op.LessThanOrEqual => c <= 0,
                        Op.GreaterThan => c > 0,
                        Op.GreaterThanOrEqual => c >= 0,
                        _ => false,
                    };
            }
        }
    }

    private readonly struct Operand
    {
        private readonly ArazzoExpression expression;
        private readonly Comparand literal;
        private readonly bool isLiteral;

        private Operand(ArazzoExpression expression, Comparand literal, bool isLiteral)
        {
            this.expression = expression;
            this.literal = literal;
            this.isLiteral = isLiteral;
        }

        public static Operand FromExpression(ArazzoExpression expression) => new(expression, default, false);

        public static Operand FromLiteral(Comparand literal) => new(default, literal, true);

        public Comparand Resolve(WorkflowExecutionContext context)
            => this.isLiteral ? this.literal : context.ResolveComparand(this.expression);
    }

    private ref struct Parser(string text)
    {
        private readonly ReadOnlySpan<char> span = text;
        private int position = 0;

        public Node ParseExpression() => this.ParseOr();

        public void ExpectEnd()
        {
            this.SkipWhitespace();
            if (this.position != this.span.Length)
            {
                throw new FormatException($"Unexpected trailing content in condition at position {this.position}.");
            }
        }

        private Node ParseOr()
        {
            Node left = this.ParseAnd();
            while (this.TryConsume("||"))
            {
                left = new OrNode(left, this.ParseAnd());
            }

            return left;
        }

        private Node ParseAnd()
        {
            Node left = this.ParseComparison();
            while (this.TryConsume("&&"))
            {
                left = new AndNode(left, this.ParseComparison());
            }

            return left;
        }

        private Node ParseComparison()
        {
            this.SkipWhitespace();
            if (this.TryConsume("("))
            {
                Node grouped = this.ParseOr();
                if (!this.TryConsume(")"))
                {
                    throw new FormatException("Expected ')' in condition.");
                }

                return grouped;
            }

            Operand left = this.ParseOperand();
            if (this.TryParseOperator(out Op op))
            {
                return new ComparisonNode(left, op, this.ParseOperand());
            }

            return new TruthyNode(left);
        }

        private Operand ParseOperand()
        {
            this.SkipWhitespace();
            ReadOnlySpan<char> token = this.ReadOperandToken();
            if (token.IsEmpty)
            {
                throw new FormatException($"Expected an operand in condition at position {this.position}.");
            }

            if (token[0] == '$')
            {
                return Operand.FromExpression(ArazzoExpression.Parse(token.ToString()));
            }

            Comparand literal = Comparand.ParseLiteral(token);
            if (literal.Kind == ComparandKind.Undefined)
            {
                throw new FormatException($"Unrecognized literal '{token.ToString()}' in condition.");
            }

            return Operand.FromLiteral(literal);
        }

        private ReadOnlySpan<char> ReadOperandToken()
        {
            int start = this.position;
            if (this.position < this.span.Length && (this.span[this.position] == '\'' || this.span[this.position] == '"'))
            {
                char quote = this.span[this.position];
                this.position++;
                while (this.position < this.span.Length && this.span[this.position] != quote)
                {
                    this.position++;
                }

                if (this.position < this.span.Length)
                {
                    this.position++; // closing quote
                }

                return this.span[start..this.position];
            }

            while (this.position < this.span.Length && !IsDelimiter(this.span[this.position]))
            {
                this.position++;
            }

            return this.span[start..this.position];
        }

        private bool TryParseOperator(out Op op)
        {
            this.SkipWhitespace();
            if (this.TryConsume("==")) { op = Op.Equal; return true; }
            if (this.TryConsume("!=")) { op = Op.NotEqual; return true; }
            if (this.TryConsume("<=")) { op = Op.LessThanOrEqual; return true; }
            if (this.TryConsume(">=")) { op = Op.GreaterThanOrEqual; return true; }
            if (this.TryConsume("<")) { op = Op.LessThan; return true; }
            if (this.TryConsume(">")) { op = Op.GreaterThan; return true; }
            op = default;
            return false;
        }

        private bool TryConsume(string symbol)
        {
            this.SkipWhitespace();
            if (this.span[this.position..].StartsWith(symbol))
            {
                this.position += symbol.Length;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (this.position < this.span.Length && char.IsWhiteSpace(this.span[this.position]))
            {
                this.position++;
            }
        }

        private static bool IsDelimiter(char c)
            => char.IsWhiteSpace(c) || c is '&' or '|' or '=' or '!' or '<' or '>' or '(' or ')';
    }
}