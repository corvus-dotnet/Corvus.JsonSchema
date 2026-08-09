// <copyright file="SecurityLabelQuery.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A plan for narrowing a candidate row set using a backend's security-label index (design §14.4), for the
/// backends whose query language cannot express a <see cref="SecurityRule"/> directly. It is produced by
/// <see cref="SecurityLabelQueryEmitter"/> and consumed in one of two ways, so a backend never inspects this
/// structure itself: a point-lookup backend supplies the two lookups on <see cref="ISecurityLabelIndex"/> to
/// <see cref="SecurityLabelQueryResolver"/>, and a backend with native set operations (Redis) executes the flat
/// program <see cref="Compile"/> emits.
/// </summary>
/// <remarks>
/// <para>
/// <b>The set this denotes is a sound OVER-approximation.</b> Resolving it yields a superset of the rows the
/// rule admits, never a subset, and the caller then applies <see cref="SecurityFilter.IsSatisfiedBy(in SecurityTagSet)"/>
/// to each candidate to get the exact answer. The index therefore decides how much data crosses the wire, and
/// the evaluator decides what the principal sees, so an imprecise plan costs throughput and can never widen
/// reach.
/// </para>
/// <para>
/// <b>Why there is no complement node.</b> Widening a subtree and then negating it <em>narrows</em> the result,
/// which would silently hide rows a principal is entitled to see. Rather than track which subtrees are exact,
/// every negation resolves to <see cref="Universe"/> — sound unconditionally, and no real loss, because
/// complementing a set of row ids needs the full universe of ids anyway. The property that matters survives:
/// the deployment's mandated wrapper rule (§14.3, e.g. <c>sys:tenant == $claim.tenant</c>) is ANDed into every
/// decision and maps to an exact <see cref="Label"/>, so however far the user rule degrades, the intersection
/// still bounds the query to the principal's own tenant.
/// </para>
/// </remarks>
public sealed class SecurityLabelQuery
{
    private readonly QueryKind kind;
    private readonly string? key;
    private readonly string? value;
    private readonly IReadOnlyList<SecurityLabelQuery>? children;

    private SecurityLabelQuery(QueryKind kind, string? key, string? value, IReadOnlyList<SecurityLabelQuery>? children)
    {
        this.kind = kind;
        this.key = key;
        this.value = value;
        this.children = children;
    }

    /// <summary>The kinds of plan node. Internal: a backend resolves a plan, it does not walk one.</summary>
    internal enum QueryKind
    {
        /// <summary>No narrowing is possible; the caller must consider every row it would otherwise consider.</summary>
        Universe,

        /// <summary>No row qualifies, so the caller need not query at all.</summary>
        Empty,

        /// <summary>Rows carrying the security tag (<see cref="Key"/>, <see cref="Value"/>).</summary>
        Label,

        /// <summary>Rows carrying the security-tag key <see cref="Key"/> with any value.</summary>
        AnyValueOfKey,

        /// <summary>The union of the child sets.</summary>
        Union,

        /// <summary>The intersection of the child sets.</summary>
        Intersect,
    }

    /// <summary>Gets the plan denoting every row, i.e. "the index cannot narrow this".</summary>
    public static SecurityLabelQuery Universe { get; } = new(QueryKind.Universe, null, null, null);

    /// <summary>Gets the plan denoting no row.</summary>
    public static SecurityLabelQuery Empty { get; } = new(QueryKind.Empty, null, null, null);

    internal QueryKind Kind => this.kind;

    internal string Key => this.key!;

    internal string Value => this.value!;

    internal IReadOnlyList<SecurityLabelQuery> Children => this.children!;

    /// <summary>Builds the plan for rows carrying a specific security tag.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The plan.</returns>
    public static SecurityLabelQuery Label(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        return new SecurityLabelQuery(QueryKind.Label, key, value, null);
    }

    /// <summary>Builds the plan for rows carrying a security tag with the given key, whatever its value.</summary>
    /// <param name="key">The tag key.</param>
    /// <returns>The plan.</returns>
    public static SecurityLabelQuery AnyValueOfKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new SecurityLabelQuery(QueryKind.AnyValueOfKey, key, null, null);
    }

    /// <summary>
    /// Builds the union of two plans, collapsing the identities. <see cref="Universe"/> absorbs (a union that
    /// includes everything is everything) and <see cref="Empty"/> is dropped.
    /// </summary>
    /// <param name="left">The left plan.</param>
    /// <param name="right">The right plan.</param>
    /// <returns>The plan.</returns>
    public static SecurityLabelQuery Union(SecurityLabelQuery left, SecurityLabelQuery right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.kind == QueryKind.Universe || right.kind == QueryKind.Universe)
        {
            return Universe;
        }

        if (left.kind == QueryKind.Empty)
        {
            return right;
        }

        if (right.kind == QueryKind.Empty)
        {
            return left;
        }

        return new SecurityLabelQuery(QueryKind.Union, null, null, Combine(QueryKind.Union, left, right));
    }

    /// <summary>
    /// Builds the intersection of two plans, collapsing the identities. <see cref="Empty"/> absorbs and
    /// <see cref="Universe"/> is dropped — the case that carries the design's weight, because it is what lets an
    /// un-narrowable user rule intersect with an exact wrapper-rule label and still bound the query.
    /// </summary>
    /// <param name="left">The left plan.</param>
    /// <param name="right">The right plan.</param>
    /// <returns>The plan.</returns>
    public static SecurityLabelQuery Intersect(SecurityLabelQuery left, SecurityLabelQuery right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.kind == QueryKind.Empty || right.kind == QueryKind.Empty)
        {
            return Empty;
        }

        if (left.kind == QueryKind.Universe)
        {
            return right;
        }

        if (right.kind == QueryKind.Universe)
        {
            return left;
        }

        return new SecurityLabelQuery(QueryKind.Intersect, null, null, Combine(QueryKind.Intersect, left, right));
    }

    /// <summary>
    /// Compiles the plan to a flat postfix program for a backend that executes the set algebra natively
    /// (server-side set operations) rather than supplying point lookups to <see cref="SecurityLabelQueryResolver"/>.
    /// </summary>
    /// <remarks>
    /// The program mirrors the resolver's contract: <see langword="null"/> means the plan is
    /// <see cref="Universe"/> — the index cannot narrow, and the caller should run its unnarrowed query — and an
    /// empty program means the plan is <see cref="Empty"/> — no row qualifies, and the caller should answer
    /// without querying. A non-empty program contains only pushes and set operations, because
    /// <see cref="Union(SecurityLabelQuery, SecurityLabelQuery)"/> and
    /// <see cref="Intersect(SecurityLabelQuery, SecurityLabelQuery)"/> collapse the identities at construction,
    /// so <see cref="Universe"/> and <see cref="Empty"/> can only ever be the root. Executing it over a stack —
    /// pushes look up a label set, operations pop <see cref="SecurityLabelInstruction.Count"/> sets and push the
    /// combined set — leaves the candidate set as the single remaining entry.
    /// </remarks>
    /// <returns>The program, or <see langword="null"/> for "no narrowing possible".</returns>
    public IReadOnlyList<SecurityLabelInstruction>? Compile()
    {
        if (this.kind == QueryKind.Universe)
        {
            return null;
        }

        if (this.kind == QueryKind.Empty)
        {
            return [];
        }

        var program = new List<SecurityLabelInstruction>();
        Emit(this, program);
        return program;

        static void Emit(SecurityLabelQuery node, List<SecurityLabelInstruction> program)
        {
            switch (node.kind)
            {
                case QueryKind.Label:
                    program.Add(new(SecurityLabelInstructionKind.PushLabel, node.key, node.value, 0));
                    break;

                case QueryKind.AnyValueOfKey:
                    program.Add(new(SecurityLabelInstructionKind.PushKey, node.key, null, 0));
                    break;

                case QueryKind.Union:
                case QueryKind.Intersect:
                    foreach (SecurityLabelQuery child in node.Children)
                    {
                        Emit(child, program);
                    }

                    program.Add(new(
                        node.kind == QueryKind.Union ? SecurityLabelInstructionKind.Union : SecurityLabelInstructionKind.Intersect,
                        null,
                        null,
                        node.Children.Count));
                    break;

                default:
                    // The factories collapse Universe and Empty at construction, so neither can sit below the root.
                    throw new System.Diagnostics.UnreachableException();
            }
        }
    }

    // Flattens same-kind children so a long chain of binary ANDs resolves as one n-ary intersection rather than
    // as nested pairs, which matters because each level would otherwise materialise an intermediate id set.
    private static List<SecurityLabelQuery> Combine(QueryKind kind, SecurityLabelQuery left, SecurityLabelQuery right)
    {
        var combined = new List<SecurityLabelQuery>(2);
        Append(combined, kind, left);
        Append(combined, kind, right);
        return combined;

        static void Append(List<SecurityLabelQuery> into, QueryKind kind, SecurityLabelQuery node)
        {
            if (node.kind == kind)
            {
                into.AddRange(node.Children);
            }
            else
            {
                into.Add(node);
            }
        }
    }
}