// <copyright file="SecurityRule.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;

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
/// <para>
/// Two further templates cover the common tag-based constructs (design §14.2):
/// </para>
/// <list type="bullet">
/// <item><b>Set membership</b> — <c>tenant in ('acme', 'globex')</c> is "the row carries a <c>tenant</c> label whose
/// value is one of the listed literals" (the multi-valued sugar for an <c>||</c> of equalities; negate with <c>!</c>).</item>
/// <item><b>Ordered comparison</b> — <c>classification &lt;= 'confidential'</c> (and <c>&lt;</c>/<c>&gt;</c>/<c>&gt;=</c>)
/// ranks the row's <c>classification</c> labels against a configured ascending ordering (a
/// <see cref="SecurityLabelOrderings"/> captured at <see cref="Compile(string, SecurityLabelOrderings)"/>). The left
/// side is the ordered <b>dimension</b>; the right side is a literal or <c>$claim</c> bound. Multi-value is
/// <em>conservative</em>: every row value for the dimension must satisfy the bound, where a multi-valued bound takes
/// its most permissive rank for the principal (the highest for an upper bound, the lowest for a lower bound). An
/// <b>unranked</b> row value, an <b>unordered</b> dimension, or a bound with no ranked value <b>denies</b> (fail-closed).</item>
/// </list>
/// <para>
/// <b>Evaluation is bytes-to-bytes (design §14.2 / §16.5.4).</b> A row's tags arrive as a deferred
/// <see cref="SecurityTagSet"/> holder (unescaped UTF-8); the row tags are parsed <b>once</b> into a pooled scratch
/// buffer + slice table (the <see cref="SecurityTagSpanSort"/> core) and every operand/claims comparison runs on the
/// unescaped UTF-8 spans. The rule's literal/tag-key operand values are encoded to UTF-8 once at
/// <see cref="Compile(string, SecurityLabelOrderings)"/>, and the principal's claims once per request (<see cref="Utf8ClaimSet"/>), so a list/search
/// scan evaluates each candidate row without materialising a single managed <see cref="SecurityTag"/> string or
/// <see cref="List{T}"/>. Ordinal UTF-8 byte equality is exactly the ordinal string equality of the same code points.
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

    private enum ComparisonToken
    {
        None,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
    }

    private enum RankComparison
    {
        /// <summary><c>&lt;</c> — every row value's rank is strictly below the bound.</summary>
        LessThan,

        /// <summary><c>&lt;=</c> — every row value's rank is at or below the bound.</summary>
        LessThanOrEqual,

        /// <summary><c>&gt;</c> — every row value's rank is strictly above the bound.</summary>
        GreaterThan,

        /// <summary><c>&gt;=</c> — every row value's rank is at or above the bound.</summary>
        GreaterThanOrEqual,
    }

    private enum OperandKind
    {
        /// <summary>A bare identifier: a security-tag key resolved against the row's labels.</summary>
        TagKey,

        /// <summary>A <c>$claim.&lt;name&gt;</c> reference resolved against the principal's claims.</summary>
        Claim,

        /// <summary>A quoted literal value.</summary>
        Literal,

        /// <summary>A <c>$claims.*</c> key-agnostic predicate over the whole claim set (e.g. <c>$claims.superset</c>); a complete boolean atom, not a comparand.</summary>
        ClaimsPredicate,
    }

    private enum ClaimsQuantifier
    {
        /// <summary><c>$claims.superset</c> — every row tag is covered by the principal's claims (ABAC clearance).</summary>
        Superset,

        /// <summary><c>$claims.intersects</c> — at least one row tag is covered by the principal's claims.</summary>
        Intersects,
    }

    /// <summary>Compiles a rule expression with no configured label orderings (every ordered comparison denies). Parse once; evaluate many times.</summary>
    /// <param name="rule">The rule expression.</param>
    /// <returns>The compiled rule.</returns>
    /// <exception cref="FormatException">The expression is malformed.</exception>
    public static SecurityRule Compile(string rule) => Compile(rule, SecurityLabelOrderings.Empty);

    /// <summary>Compiles a rule expression, baking in the deployment's <paramref name="orderings"/> so each ordered
    /// comparison (<c>&lt;</c>/<c>&lt;=</c>/<c>&gt;</c>/<c>&gt;=</c>) resolves a label to its rank without threading the
    /// orderings through evaluation or SQL translation. Parse once; evaluate many times.</summary>
    /// <param name="rule">The rule expression.</param>
    /// <param name="orderings">The ascending label ordering per ordered dimension (an unordered dimension makes an ordered comparison deny).</param>
    /// <returns>The compiled rule.</returns>
    /// <exception cref="FormatException">The expression is malformed.</exception>
    public static SecurityRule Compile(string rule, SecurityLabelOrderings orderings)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(orderings);
        var parser = new Parser(rule, orderings);
        Node node = parser.ParseExpression();
        parser.ExpectEnd();
        return new SecurityRule(node);
    }

    /// <summary>Evaluates the rule against a row's security tags and a principal's claims — the bytes-to-bytes path
    /// (the row tags are walked as unescaped UTF-8, no managed <see cref="SecurityTag"/> is materialised).</summary>
    /// <param name="securityTags">The row's security tags (a run's or catalog version's), as the deferred holder.</param>
    /// <param name="claims">The principal's claims: claim name → its values.</param>
    /// <returns><see langword="true"/> if the rule admits the row to the principal.</returns>
    public bool IsSatisfiedBy(in SecurityTagSet securityTags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var utf8Claims = new Utf8ClaimSet(claims);

        // The single-rule public entry point (used for one-off read/write checks and the rule tests); the warm
        // multi-rule filter scan goes through SecurityFilter, which parses each row once for the whole rule set.
        return EvaluateAll([this], securityTags, in utf8Claims);
    }

    /// <summary>Evaluates the rule against a row's security tags (as a materialised tag list) and a principal's claims.
    /// A convenience over the deferred-holder path for callers (and tests) that already hold a list; it delegates to the
    /// same bytes-to-bytes evaluator via <see cref="SecurityTagSet.FromTags"/>.</summary>
    /// <param name="securityTags">The row's security-tag labels (a run's or catalog version's).</param>
    /// <param name="claims">The principal's claims: claim name → its values.</param>
    /// <returns><see langword="true"/> if the rule admits the row to the principal.</returns>
    public bool IsSatisfiedBy(IReadOnlyList<SecurityTag> securityTags, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(securityTags);
        return this.IsSatisfiedBy(SecurityTagSet.FromTags(securityTags), claims);
    }

    /// <summary>
    /// Translates the rule into a SQL <c>WHERE</c> boolean fragment (design §14.4) that selects exactly the rows
    /// <see cref="IsSatisfiedBy(in SecurityTagSet, IReadOnlyDictionary{string, IReadOnlyList{string}})"/> would admit,
    /// using the backend's dialect/schema fragments. The principal's claims are resolved to bound values here (they are
    /// query-time constants); tag-key operands become correlated <c>EXISTS</c> subqueries over the row's security tags.
    /// </summary>
    /// <param name="emitter">The backend's SQL fragment provider (stateful per query; accumulates bound parameters).</param>
    /// <param name="claims">The principal's claims: claim name → its values.</param>
    /// <returns>A boolean SQL fragment.</returns>
    public string ToSqlPredicate(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
    {
        ArgumentNullException.ThrowIfNull(emitter);
        ArgumentNullException.ThrowIfNull(claims);
        return this.root.ToSql(emitter, claims);
    }

    // Evaluates every rule in the set against one row's tags, parsing the row tags ONCE into pooled scratch + a slice
    // table (the SecurityTagSpanSort core), so a multi-rule filter pays one parse per row. The slices index unescaped
    // UTF-8 in the scratch buffer; nothing escapes to the heap (the buffers are pooled and returned).
    internal static bool EvaluateAll(IReadOnlyList<SecurityRule> rules, in SecurityTagSet securityTags, in Utf8ClaimSet claims)
    {
        if (securityTags.IsEmpty)
        {
            // No row tags: evaluate over an empty span set (a tag-key/truthy operand resolves to empty; a
            // $claims.superset predicate is vacuously true). The fail-closed untagged-row denial lives in SecurityFilter.
            var emptyRows = default(RowTags);
            foreach (SecurityRule rule in rules)
            {
                if (!rule.root.Evaluate(emptyRows, in claims))
                {
                    return false;
                }
            }

            return true;
        }

        ReadOnlySpan<byte> json = securityTags.RawJson;
        int count = securityTags.Count;
        byte[] scratch = ArrayPool<byte>.Shared.Rent(json.Length);
        SecurityTagSpanSort.TagSlice[]? rented = count > SecurityTagSpanSort.StackTagCapacity
            ? ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Rent(count)
            : null;
        try
        {
            scoped Span<SecurityTagSpanSort.TagSlice> table = rented is not null
                ? rented
                : stackalloc SecurityTagSpanSort.TagSlice[SecurityTagSpanSort.StackTagCapacity];
            Span<SecurityTagSpanSort.TagSlice> slices = table[..count];
            SecurityTagSpanSort.Parse(json, scratch, slices);
            var rows = new RowTags(scratch, slices);
            foreach (SecurityRule rule in rules)
            {
                if (!rule.root.Evaluate(rows, in claims))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
            if (rented is not null)
            {
                ArrayPool<SecurityTagSpanSort.TagSlice>.Shared.Return(rented);
            }
        }
    }

    // ── bytes-to-bytes evaluation primitives ────────────────────────────────────────────────────────────────────────

    // Does any value the row carries under tag key `left` intersect the value set the `right` operand resolves to?
    private static bool SetsIntersect(in Operand left, in Operand right, RowTags rows, in Utf8ClaimSet claims)
    {
        if (left.IsTagKey)
        {
            return TagKeyIntersects(left.ValueUtf8, right, rows, claims);
        }

        if (right.IsTagKey)
        {
            return TagKeyIntersects(right.ValueUtf8, left, rows, claims);
        }

        // Both operands are query-time constants (claims/literals): a constant intersection over UTF-8 values.
        return KnownIntersects(left, right, claims);
    }

    private static bool TagKeyIntersects(ReadOnlySpan<byte> tagKey, in Operand other, RowTags rows, in Utf8ClaimSet claims)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows.Key(i).SequenceEqual(tagKey) && OtherContains(other, rows.Value(i), rows, claims))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OtherContains(in Operand other, ReadOnlySpan<byte> value, RowTags rows, in Utf8ClaimSet claims)
    {
        if (other.IsTagKey)
        {
            ReadOnlySpan<byte> otherKey = other.ValueUtf8;
            for (int j = 0; j < rows.Count; j++)
            {
                if (rows.Key(j).SequenceEqual(otherKey) && rows.Value(j).SequenceEqual(value))
                {
                    return true;
                }
            }

            return false;
        }

        return KnownContains(other, value, claims);
    }

    // Whether a query-time-known operand (literal or claim) resolves to a set containing `value`.
    private static bool KnownContains(in Operand known, ReadOnlySpan<byte> value, in Utf8ClaimSet claims)
    {
        if (known.IsLiteral)
        {
            return value.SequenceEqual(known.ValueUtf8);
        }

        // A claim operand: the principal's values for the claim name.
        if (claims.TryGetValues(known.ValueUtf8, out byte[][] values))
        {
            foreach (byte[] claimValue in values)
            {
                if (value.SequenceEqual(claimValue))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool KnownIntersects(in Operand left, in Operand right, in Utf8ClaimSet claims)
    {
        if (left.IsLiteral)
        {
            return KnownContains(right, left.ValueUtf8, claims);
        }

        // left is a claim: any of its values present in right's set.
        if (claims.TryGetValues(left.ValueUtf8, out byte[][] values))
        {
            foreach (byte[] claimValue in values)
            {
                if (KnownContains(right, claimValue, claims))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Whether an operand's value set is non-empty (a bare-operand truthiness test).
    private static bool OperandNonEmpty(in Operand operand, RowTags rows, in Utf8ClaimSet claims)
    {
        if (operand.IsTagKey)
        {
            ReadOnlySpan<byte> key = operand.ValueUtf8;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows.Key(i).SequenceEqual(key))
                {
                    return true;
                }
            }

            return false;
        }

        // A literal always resolves to a single value; a claim is non-empty iff the principal holds it.
        return operand.IsLiteral || (claims.TryGetValues(operand.ValueUtf8, out byte[][] values) && values.Length > 0);
    }

    // Whether a fixed UTF-8 value set (a literal list / a dimension's admissible labels) contains `value`.
    private static bool ContainsUtf8(byte[][] set, ReadOnlySpan<byte> value)
    {
        foreach (byte[] candidate in set)
        {
            if (value.SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private abstract class Node
    {
        public abstract bool Evaluate(RowTags rows, in Utf8ClaimSet claims);

        public abstract string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims);
    }

    private sealed class OrNode(Node left, Node right) : Node
    {
        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
            => left.Evaluate(rows, in claims) || right.Evaluate(rows, in claims);

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => emitter.OrElse(left.ToSql(emitter, claims), right.ToSql(emitter, claims));
    }

    private sealed class AndNode(Node left, Node right) : Node
    {
        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
            => left.Evaluate(rows, in claims) && right.Evaluate(rows, in claims);

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => emitter.AndAlso(left.ToSql(emitter, claims), right.ToSql(emitter, claims));
    }

    private sealed class NotNode(Node inner) : Node
    {
        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
            => !inner.Evaluate(rows, in claims);

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => emitter.Negate(inner.ToSql(emitter, claims));
    }

    private sealed class ComparisonNode(Operand left, Op op, Operand right) : Node
    {
        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
        {
            bool intersects = SetsIntersect(left, right, rows, in claims);
            return op == Op.Equal ? intersects : !intersects;
        }

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
        {
            string intersects;
            if (left.IsTagKey && right.IsTagKey)
            {
                // Both operands are row data: two tag keys share a value.
                intersects = emitter.ExistsTagKeysShareValue(emitter.Parameter(left.Value), emitter.Parameter(right.Value));
            }
            else if (left.IsTagKey || right.IsTagKey)
            {
                // One operand is row data (a tag key); the other resolves to query-time-known values.
                Operand tagKey = left.IsTagKey ? left : right;
                IReadOnlyList<string> values = (left.IsTagKey ? right : left).ResolveKnown(claims);
                if (values.Count == 0)
                {
                    // No candidate values → the sets cannot intersect.
                    intersects = emitter.FalseLiteral;
                }
                else
                {
                    var valuePlaceholders = new List<string>(values.Count);
                    string keyPlaceholder = emitter.Parameter(tagKey.Value);
                    foreach (string value in values)
                    {
                        valuePlaceholders.Add(emitter.Parameter(value));
                    }

                    intersects = emitter.ExistsTagValueIn(keyPlaceholder, valuePlaceholders);
                }
            }
            else
            {
                // Both operands are query-time constants (claims/literals): a constant intersection.
                intersects = ConstantIntersects(left.ResolveKnown(claims), right.ResolveKnown(claims)) ? emitter.TrueLiteral : emitter.FalseLiteral;
            }

            return op == Op.Equal ? intersects : emitter.Negate(intersects);
        }
    }

    private sealed class TruthyNode(Operand operand) : Node
    {
        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
            => OperandNonEmpty(operand, rows, in claims);

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => operand.IsTagKey
                ? emitter.ExistsTagKey(emitter.Parameter(operand.Value))
                : (operand.ResolveKnown(claims).Count > 0 ? emitter.TrueLiteral : emitter.FalseLiteral);
    }

    // Set membership: `left in ('v1', 'v2', …)` — does left's value set intersect the fixed literal set? For a tag-key
    // left it is the multi-valued sugar for an `||` of equalities (one EXISTS … IN (…) in SQL); for a query-time-known
    // left (literal/claim) it is a constant membership test.
    private sealed class InNode : Node
    {
        private readonly Operand left;
        private readonly IReadOnlyList<string> values;
        private readonly byte[][] valuesUtf8;

        public InNode(Operand left, IReadOnlyList<string> values)
        {
            this.left = left;
            this.values = values;
            var encoded = new byte[values.Count][];
            for (int i = 0; i < values.Count; i++)
            {
                encoded[i] = Encoding.UTF8.GetBytes(values[i]);
            }

            this.valuesUtf8 = encoded;
        }

        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
        {
            if (this.left.IsTagKey)
            {
                ReadOnlySpan<byte> key = this.left.ValueUtf8;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows.Key(i).SequenceEqual(key) && ContainsUtf8(this.valuesUtf8, rows.Value(i)))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (this.left.IsLiteral)
            {
                return ContainsUtf8(this.valuesUtf8, this.left.ValueUtf8);
            }

            // A claim left: any of the principal's values for the claim present in the literal set.
            if (claims.TryGetValues(this.left.ValueUtf8, out byte[][] claimValues))
            {
                foreach (byte[] claimValue in claimValues)
                {
                    if (ContainsUtf8(this.valuesUtf8, claimValue))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
        {
            if (this.left.IsTagKey)
            {
                string keyPlaceholder = emitter.Parameter(this.left.Value);
                var valuePlaceholders = new List<string>(this.values.Count);
                foreach (string value in this.values)
                {
                    valuePlaceholders.Add(emitter.Parameter(value));
                }

                return emitter.ExistsTagValueIn(keyPlaceholder, valuePlaceholders);
            }

            // A query-time-known left: a constant membership test.
            foreach (string resolved in this.left.ResolveKnown(claims))
            {
                foreach (string value in this.values)
                {
                    if (string.Equals(resolved, value, StringComparison.Ordinal))
                    {
                        return emitter.TrueLiteral;
                    }
                }
            }

            return emitter.FalseLiteral;
        }
    }

    // Ordered comparison: `dimension <= bound` (and `<`/`>`/`>=`) over a configured ascending label ordering. The
    // dimension's labels are ranked by their position in the ordering (captured at compile); the row is admitted only
    // when the dimension is present and EVERY row value for it satisfies the bound (conservative multi-value), with an
    // unranked row value / unordered dimension / unranked bound denying (fail-closed). In SQL this is exactly "the
    // dimension is present AND every value lies in the admissible label set" (the labels whose rank meets the bound).
    private sealed class OrderedComparisonNode : Node
    {
        private readonly string dimension;
        private readonly byte[] dimensionUtf8;
        private readonly RankComparison comparison;
        private readonly Operand bound;
        private readonly IReadOnlyList<string> ascending;
        private readonly byte[][] ascendingUtf8;

        public OrderedComparisonNode(string dimension, RankComparison comparison, Operand bound, IReadOnlyList<string> ascending)
        {
            this.dimension = dimension;
            this.dimensionUtf8 = Encoding.UTF8.GetBytes(dimension);
            this.comparison = comparison;
            this.bound = bound;
            this.ascending = ascending;
            var encoded = new byte[ascending.Count][];
            for (int i = 0; i < ascending.Count; i++)
            {
                encoded[i] = Encoding.UTF8.GetBytes(ascending[i]);
            }

            this.ascendingUtf8 = encoded;
        }

        private bool IsUpperBound => this.comparison is RankComparison.LessThan or RankComparison.LessThanOrEqual;

        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
        {
            // An unordered dimension, or a bound that resolves to no ranked value, denies.
            if (this.ascendingUtf8.Length == 0 || !this.TryResolveBound(in claims, out int boundRank))
            {
                return false;
            }

            int rowMin = int.MaxValue;
            int rowMax = int.MinValue;
            bool present = false;
            ReadOnlySpan<byte> key = this.dimensionUtf8;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows.Key(i).SequenceEqual(key))
                {
                    continue;
                }

                int rank = this.RankOf(rows.Value(i));
                if (rank < 0)
                {
                    // An unranked value the principal cannot reason about → deny (fail-closed).
                    return false;
                }

                present = true;
                if (rank < rowMin)
                {
                    rowMin = rank;
                }

                if (rank > rowMax)
                {
                    rowMax = rank;
                }
            }

            if (!present)
            {
                // The dimension is absent: nothing to compare → deny (an unclassified row is never admitted by reach).
                return false;
            }

            return this.comparison switch
            {
                RankComparison.LessThanOrEqual => rowMax <= boundRank,
                RankComparison.LessThan => rowMax < boundRank,
                RankComparison.GreaterThanOrEqual => rowMin >= boundRank,
                RankComparison.GreaterThan => rowMin > boundRank,
                _ => false,
            };
        }

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
        {
            if (this.ascendingUtf8.Length == 0 || !this.TryResolveBound(claims, out int boundRank))
            {
                return emitter.FalseLiteral;
            }

            // The admissible label set is the contiguous slice of the ordering whose rank meets the bound.
            int lo;
            int hi;
            switch (this.comparison)
            {
                case RankComparison.LessThanOrEqual: lo = 0; hi = boundRank; break;
                case RankComparison.LessThan: lo = 0; hi = boundRank - 1; break;
                case RankComparison.GreaterThanOrEqual: lo = boundRank; hi = this.ascending.Count - 1; break;
                case RankComparison.GreaterThan: lo = boundRank + 1; hi = this.ascending.Count - 1; break;
                default: return emitter.FalseLiteral;
            }

            lo = Math.Max(lo, 0);
            hi = Math.Min(hi, this.ascending.Count - 1);
            if (lo > hi)
            {
                // The admissible set is empty → no row value can satisfy the bound.
                return emitter.FalseLiteral;
            }

            string keyPlaceholder = emitter.Parameter(this.dimension);
            var valuePlaceholders = new List<string>(hi - lo + 1);
            for (int i = lo; i <= hi; i++)
            {
                valuePlaceholders.Add(emitter.Parameter(this.ascending[i]));
            }

            return emitter.ExistsTagAllValuesIn(keyPlaceholder, valuePlaceholders);
        }

        private int RankOf(ReadOnlySpan<byte> value)
        {
            for (int i = 0; i < this.ascendingUtf8.Length; i++)
            {
                if (value.SequenceEqual(this.ascendingUtf8[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        private int RankOf(string value)
        {
            for (int i = 0; i < this.ascending.Count; i++)
            {
                if (string.Equals(value, this.ascending[i], StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        // The bound's rank: the principal's most permissive value (the highest rank for an upper bound, the lowest for a
        // lower bound). The bytes-to-bytes path (literal compared once, claims resolved per request).
        private bool TryResolveBound(in Utf8ClaimSet claims, out int boundRank)
        {
            bool upper = this.IsUpperBound;
            int acc = upper ? int.MinValue : int.MaxValue;
            bool any = false;
            if (this.bound.IsLiteral)
            {
                int rank = this.RankOf(this.bound.ValueUtf8);
                if (rank >= 0)
                {
                    any = true;
                    acc = rank;
                }
            }
            else if (claims.TryGetValues(this.bound.ValueUtf8, out byte[][] boundValues))
            {
                foreach (byte[] boundValue in boundValues)
                {
                    int rank = this.RankOf(boundValue);
                    if (rank < 0)
                    {
                        continue;
                    }

                    any = true;
                    acc = upper ? Math.Max(acc, rank) : Math.Min(acc, rank);
                }
            }

            boundRank = acc;
            return any;
        }

        // The SQL path's bound resolution (the principal's claims as the query-time string dictionary).
        private bool TryResolveBound(IReadOnlyDictionary<string, IReadOnlyList<string>> claims, out int boundRank)
        {
            bool upper = this.IsUpperBound;
            int acc = upper ? int.MinValue : int.MaxValue;
            bool any = false;
            foreach (string boundValue in this.bound.ResolveKnown(claims))
            {
                int rank = this.RankOf(boundValue);
                if (rank < 0)
                {
                    continue;
                }

                any = true;
                acc = upper ? Math.Max(acc, rank) : Math.Min(acc, rank);
            }

            boundRank = acc;
            return any;
        }
    }

    // A key-agnostic predicate over the whole claim set vs. the row's tags (design §14.2 bootstrap archetypes).
    // "Covered" means a row tag (k, v) has v among the principal's claim values for key k.
    private sealed class ClaimsCoverageNode(ClaimsQuantifier quantifier) : Node
    {
        public override bool Evaluate(RowTags rows, in Utf8ClaimSet claims)
        {
            if (quantifier == ClaimsQuantifier.Superset)
            {
                // Every row tag must be covered (vacuously true on an untagged row; the filter denies that case).
                for (int i = 0; i < rows.Count; i++)
                {
                    if (!claims.Covers(rows.Key(i), rows.Value(i)))
                    {
                        return false;
                    }
                }

                return true;
            }

            // Intersects: at least one row tag must be covered.
            for (int i = 0; i < rows.Count; i++)
            {
                if (claims.Covers(rows.Key(i), rows.Value(i)))
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToSql(ISecurityRuleSqlEmitter emitter, IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
        {
            if (quantifier == ClaimsQuantifier.Superset)
            {
                var claimEntries = new List<(string KeyPlaceholder, IReadOnlyList<string> ValuePlaceholders)>();
                foreach (KeyValuePair<string, IReadOnlyList<string>> claim in claims)
                {
                    if (claim.Value.Count == 0)
                    {
                        continue;
                    }

                    string keyPlaceholder = emitter.Parameter(claim.Key);
                    var valuePlaceholders = new List<string>(claim.Value.Count);
                    foreach (string value in claim.Value)
                    {
                        valuePlaceholders.Add(emitter.Parameter(value));
                    }

                    claimEntries.Add((keyPlaceholder, valuePlaceholders));
                }

                return emitter.ExistsAllTagsCovered(claimEntries);
            }

            // Intersects: OR over each claim key of "the row has a tag (key, v) with v in the claim's values".
            string? predicate = null;
            foreach (KeyValuePair<string, IReadOnlyList<string>> claim in claims)
            {
                if (claim.Value.Count == 0)
                {
                    continue;
                }

                string keyPlaceholder = emitter.Parameter(claim.Key);
                var valuePlaceholders = new List<string>(claim.Value.Count);
                foreach (string value in claim.Value)
                {
                    valuePlaceholders.Add(emitter.Parameter(value));
                }

                string term = emitter.ExistsTagValueIn(keyPlaceholder, valuePlaceholders);
                predicate = predicate is null ? term : emitter.OrElse(predicate, term);
            }

            // No claim can cover any tag → the principal shares nothing.
            return predicate ?? emitter.FalseLiteral;
        }
    }

    // Constant set intersection over query-time-known string values — only the SQL path's both-constant branch.
    private static bool ConstantIntersects(IReadOnlyList<string> a, IReadOnlyList<string> b)
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

    private readonly struct Operand
    {
        private readonly OperandKind kind;
        private readonly string value;
        private readonly byte[] valueUtf8;

        public Operand(OperandKind kind, string value)
        {
            this.kind = kind;
            this.value = value;

            // Encode the operand's value to UTF-8 once at compile so every per-row comparison is span-vs-span (a
            // $claims.* predicate carries only a marker, never compared against a row, so its UTF-8 is harmless/unused).
            this.valueUtf8 = Encoding.UTF8.GetBytes(value);
        }

        public bool IsTagKey => this.kind == OperandKind.TagKey;

        public bool IsLiteral => this.kind == OperandKind.Literal;

        public bool IsClaimsPredicate => this.kind == OperandKind.ClaimsPredicate;

        public ClaimsQuantifier ClaimsQuantifier => string.Equals(this.value, "superset", StringComparison.Ordinal) ? ClaimsQuantifier.Superset : ClaimsQuantifier.Intersects;

        public string Value => this.value;

        public ReadOnlySpan<byte> ValueUtf8 => this.valueUtf8;

        // The query-time-known values of a claim or literal operand (the SQL path). Not valid for a tag-key operand.
        public IReadOnlyList<string> ResolveKnown(IReadOnlyDictionary<string, IReadOnlyList<string>> claims)
            => this.kind switch
            {
                OperandKind.Literal => [this.value],
                OperandKind.Claim => claims.TryGetValue(this.value, out IReadOnlyList<string>? values) ? values : [],
                _ => throw new InvalidOperationException("ResolveKnown is not valid for a tag-key operand."),
            };
    }

    private ref struct Parser(string text, SecurityLabelOrderings orderings)
    {
        private const string ClaimPrefix = "$claim.";
        private readonly ReadOnlySpan<char> span = text;
        private readonly SecurityLabelOrderings orderings = orderings;
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

            // A $claims.* predicate is a complete boolean atom, never the LHS of a comparison.
            if (left.IsClaimsPredicate)
            {
                return new ClaimsCoverageNode(left.ClaimsQuantifier);
            }

            // Set membership: `left in ('v1', 'v2', …)`.
            if (this.TryConsumeKeyword("in"))
            {
                return this.ParseInList(left);
            }

            return this.TryParseComparisonOperator() switch
            {
                ComparisonToken.Equal => new ComparisonNode(left, Op.Equal, this.ParseOperand()),
                ComparisonToken.NotEqual => new ComparisonNode(left, Op.NotEqual, this.ParseOperand()),
                ComparisonToken.LessThan => this.BuildOrdered(left, RankComparison.LessThan),
                ComparisonToken.LessThanOrEqual => this.BuildOrdered(left, RankComparison.LessThanOrEqual),
                ComparisonToken.GreaterThan => this.BuildOrdered(left, RankComparison.GreaterThan),
                ComparisonToken.GreaterThanOrEqual => this.BuildOrdered(left, RankComparison.GreaterThanOrEqual),
                _ => new TruthyNode(left),
            };
        }

        // Builds an ordered comparison, validating operand shape and baking in the dimension's configured ordering (an
        // unordered dimension yields an always-deny node — fail-closed — rather than a parse error, so a rule stays
        // valid grammar until the deployment configures the ordering).
        private Node BuildOrdered(Operand left, RankComparison comparison)
        {
            if (!left.IsTagKey)
            {
                throw new FormatException("The left side of an ordered comparison (<, <=, >, >=) must be a security-tag dimension.");
            }

            Operand right = this.ParseOperand();
            if (right.IsTagKey || right.IsClaimsPredicate)
            {
                throw new FormatException("The right side of an ordered comparison (<, <=, >, >=) must be a literal or $claim value.");
            }

            IReadOnlyList<string> ascending = this.orderings.TryGetOrdering(left.Value, out IReadOnlyList<string> labels) ? labels : [];
            return new OrderedComparisonNode(left.Value, comparison, right, ascending);
        }

        private Node ParseInList(Operand left)
        {
            this.SkipWhitespace();
            if (!this.TryConsume("("))
            {
                throw new FormatException("Expected '(' after 'in' in security rule.");
            }

            var values = new List<string>();
            while (true)
            {
                this.SkipWhitespace();
                ReadOnlySpan<char> token = this.ReadOperandToken();
                if (token.IsEmpty || (token[0] != '\'' && token[0] != '"'))
                {
                    throw new FormatException($"An 'in' list takes quoted literal values in security rule at position {this.position}.");
                }

                values.Add(Unquote(token));
                this.SkipWhitespace();
                if (this.TryConsume(","))
                {
                    continue;
                }

                if (this.TryConsume(")"))
                {
                    break;
                }

                throw new FormatException($"Expected ',' or ')' in 'in' list in security rule at position {this.position}.");
            }

            return new InNode(left, values);
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

            // $claims.* — a key-agnostic predicate over the whole claim set; reserved, so typos fail closed.
            if (text.StartsWith("$claims", StringComparison.Ordinal))
            {
                return text switch
                {
                    "$claims.superset" => new Operand(OperandKind.ClaimsPredicate, "superset"),
                    "$claims.intersects" => new Operand(OperandKind.ClaimsPredicate, "intersects"),
                    _ => throw new FormatException($"Unknown $claims predicate '{text}' in security rule (expected $claims.superset or $claims.intersects)."),
                };
            }

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

        private ComparisonToken TryParseComparisonOperator()
        {
            this.SkipWhitespace();

            // Two-character operators are tried before their single-character prefixes (<= before <, >= before >).
            if (this.TryConsume("==")) { return ComparisonToken.Equal; }
            if (this.TryConsume("!=")) { return ComparisonToken.NotEqual; }
            if (this.TryConsume("<=")) { return ComparisonToken.LessThanOrEqual; }
            if (this.TryConsume(">=")) { return ComparisonToken.GreaterThanOrEqual; }
            if (this.TryConsume("<")) { return ComparisonToken.LessThan; }
            if (this.TryConsume(">")) { return ComparisonToken.GreaterThan; }
            return ComparisonToken.None;
        }

        // Consumes a bare keyword (e.g. `in`) only when it stands alone — followed by whitespace, '(', or end — so a
        // tag key that merely starts with the keyword (e.g. `internal`) is not mistaken for it.
        private bool TryConsumeKeyword(string keyword)
        {
            this.SkipWhitespace();
            ReadOnlySpan<char> rest = this.span[this.position..];
            if (rest.StartsWith(keyword, StringComparison.Ordinal))
            {
                int after = this.position + keyword.Length;
                if (after >= this.span.Length || char.IsWhiteSpace(this.span[after]) || this.span[after] == '(')
                {
                    this.position = after;
                    return true;
                }
            }

            return false;
        }

        private static bool IsDelimiter(char c)
            => char.IsWhiteSpace(c) || c is '(' or ')' or '=' or '!' or '&' or '|' or '<' or '>';

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