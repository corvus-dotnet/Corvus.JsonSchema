// <copyright file="SecurityRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A compiled tag-based authorization rule (design §14.2): a boolean expression over a row's
/// <see cref="SecurityTag"/> labels and a principal's claims, in (a reuse of) the Arazzo <c>simple</c>-criterion
/// grammar — <c>==</c>/<c>!=</c>, <c>&amp;&amp;</c>/<c>||</c>/<c>!</c>, and grouping. A principal's claims resolve
/// to a rule; the rule decides which rows the principal may see or act on.
/// </summary>
/// <remarks>
/// <para>Operands:</para>
/// <list type="bullet">
/// <item>a bare identifier (e.g. <c>tenant</c>) is a <b>security-tag key</b> — it resolves to the row's values for that key;</item>
/// <item><c>$claim.&lt;name&gt;</c> resolves to the <b>principal's claim</b> values for that name;</item>
/// <item>a quoted string (<c>'acme'</c>) is a <b>literal</b>.</item>
/// </list>
/// <para>
/// Each operand resolves to a <em>set</em> of string values, so <c>==</c> is "the sets intersect" and <c>!=</c>
/// is "they do not" — which gives the natural meaning for multi-valued labels and claims (e.g.
/// <c>team == 'payments'</c> is "the row carries a <c>team=payments</c> label", and
/// <c>tenant == $claim.tenant</c> is "the row's tenant is one the principal holds"). A bare operand with no
/// operator is truthy when its set is non-empty (e.g. <c>tenant</c> means "the row has a tenant label").
/// Comparison is ordinal (case-sensitive).
/// </para>
/// </remarks>
public sealed class SecurityRule
{
    private readonly Node root;

    private SecurityRule(Node root) => this.root = root;

    private enum Op
    {
        Equal,
        NotEqual,
    }

    private enum OperandKind
    {
        /// <summary>A bare identifier: a security-tag key resolved against the row's labels.</summary>
        TagKey,

        /// <summary>A <c>$claim.&lt;name&gt;</c> reference resolved against the principal's claims.</summary>
        Claim,

        /// <summary>A quoted literal value.</summary>
        Literal,
    }

    /// <summary>Compiles a rule expression. Parse once; evaluate many times.</summary>
    /// <param name="rule">The rule expression.</param>
    /// <returns>The compiled rule.</returns>
    /// <exception cref="FormatException">The expression is malformed.</exception>
    public static SecurityRule Compile(string rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var parser = new Parser(rule);
        Node node = parser.ParseExpression();
        parser.ExpectEnd();
        return new SecurityRule(node);
    }

    /// <summary>Evaluates the rule against a row's security tags and a principal's claims.</summary>
    /// <param name="securityTags">The row's security-tag labels (a run's or catalog version's).</param>
    /// <param name="claims">The principal's claims: claim name → its values.</param>
    /// <returns><see langword="true"/> if the rule admits the row to the principal.</returns>
    public bool IsSatisfiedBy(IReadOnlyList<SecurityTag> securityTags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(securityTags);
        ArgumentNullException.ThrowIfNull(claims);
        return this.root.Evaluate(securityTags, claims);
    }

    private static bool Intersects(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        foreach (string x in a)
        {
            foreach (string y in b)
            {
                if (string.Equals(x, y, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private abstract class Node
    {
        public abstract bool Evaluate(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims);
    }

    private sealed class OrNode(Node left, Node right) : Node
    {
        public override bool Evaluate(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => left.Evaluate(tags, claims) || right.Evaluate(tags, claims);
    }

    private sealed class AndNode(Node left, Node right) : Node
    {
        public override bool Evaluate(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => left.Evaluate(tags, claims) && right.Evaluate(tags, claims);
    }

    private sealed class NotNode(Node inner) : Node
    {
        public override bool Evaluate(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => !inner.Evaluate(tags, claims);
    }

    private sealed class ComparisonNode(Operand left, Op op, Operand right) : Node
    {
        public override bool Evaluate(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
        {
            bool intersects = Intersects(left.Resolve(tags, claims), right.Resolve(tags, claims));
            return op == Op.Equal ? intersects : !intersects;
        }
    }

    private sealed class TruthyNode(Operand operand) : Node
    {
        public override bool Evaluate(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => operand.Resolve(tags, claims).Count > 0;
    }

    private readonly struct Operand(OperandKind kind, string value)
    {
        public IReadOnlyList<string> Resolve(IReadOnlyList<SecurityTag> tags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
        {
            switch (kind)
            {
                case OperandKind.Literal:
                    return [value];

                case OperandKind.Claim:
                    return claims.TryGetValue(value, out IReadOnlyList<string>? values) ? values : [];

                default:
                    var result = new List<string>();
                    foreach (SecurityTag tag in tags)
                    {
                        if (string.Equals(tag.Key, value, StringComparison.Ordinal))
                        {
                            result.Add(tag.Value);
                        }
                    }

                    return result;
            }
        }
    }

    private ref struct Parser(string text)
    {
        private const string ClaimPrefix = "$claim.";
        private readonly ReadOnlySpan<char> span = text;
        private int position = 0;

        public Node ParseExpression() => this.ParseOr();

        public void ExpectEnd()
        {
            this.SkipWhitespace();
            if (this.position != this.span.Length)
            {
                throw new FormatException($"Unexpected trailing content in security rule at position {this.position}.");
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
            Node left = this.ParseNot();
            while (this.TryConsume("&&"))
            {
                left = new AndNode(left, this.ParseNot());
            }

            return left;
        }

        private Node ParseNot()
        {
            this.SkipWhitespace();
            if (this.position < this.span.Length
                && this.span[this.position] == '!'
                && (this.position + 1 >= this.span.Length || this.span[this.position + 1] != '='))
            {
                this.position++;
                return new NotNode(this.ParseNot());
            }

            return this.ParseComparison();
        }

        private Node ParseComparison()
        {
            this.SkipWhitespace();
            if (this.TryConsume("("))
            {
                Node grouped = this.ParseOr();
                if (!this.TryConsume(")"))
                {
                    throw new FormatException("Expected ')' in security rule.");
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
                throw new FormatException($"Expected an operand in security rule at position {this.position}.");
            }

            if (token[0] == '\'' || token[0] == '"')
            {
                return new Operand(OperandKind.Literal, Unquote(token));
            }

            string text = token.ToString();
            return text.StartsWith(ClaimPrefix, StringComparison.Ordinal)
                ? new Operand(OperandKind.Claim, text[ClaimPrefix.Length..])
                : new Operand(OperandKind.TagKey, text);
        }

        private static string Unquote(ReadOnlySpan<char> token)
        {
            char quote = token[0];
            ReadOnlySpan<char> inner = token[1..^1];
            return inner.Contains(quote) ? inner.ToString().Replace($"{quote}{quote}", $"{quote}", StringComparison.Ordinal) : inner.ToString();
        }

        private ReadOnlySpan<char> ReadOperandToken()
        {
            int start = this.position;
            if (this.position < this.span.Length && (this.span[this.position] == '\'' || this.span[this.position] == '"'))
            {
                char quote = this.span[this.position];
                this.position++;
                while (this.position < this.span.Length)
                {
                    if (this.span[this.position] == quote)
                    {
                        if (this.position + 1 < this.span.Length && this.span[this.position + 1] == quote)
                        {
                            this.position += 2;
                            continue;
                        }

                        this.position++;
                        break;
                    }

                    this.position++;
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
            op = default;
            return false;
        }

        private static bool IsDelimiter(char c)
            => char.IsWhiteSpace(c) || c is '(' or ')' or '=' or '!' or '&' or '|';

        private void SkipWhitespace()
        {
            while (this.position < this.span.Length && char.IsWhiteSpace(this.span[this.position]))
            {
                this.position++;
            }
        }

        private bool TryConsume(string token)
        {
            this.SkipWhitespace();
            if (this.span[this.position..].StartsWith(token, StringComparison.Ordinal))
            {
                this.position += token.Length;
                return true;
            }

            return false;
        }
    }
}