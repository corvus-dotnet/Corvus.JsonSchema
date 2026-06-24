// <copyright file="SecurityLabelOrderings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The deployment's configured <em>ordered</em> security-tag dimensions (design §14.2): a map from a tag key (e.g.
/// <c>classification</c>) to its labels in <b>ascending</b> order (e.g. <c>["public", "internal", "confidential",
/// "restricted"]</c>). It supplies the rank a <see cref="SecurityRule"/>'s ordered comparison (<c>&lt;</c>,
/// <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>) resolves a label to, captured into the rule once at
/// <see cref="SecurityRule.Compile(string, SecurityLabelOrderings)"/> so neither evaluation nor SQL translation
/// threads the orderings.
/// </summary>
/// <remarks>
/// <para>
/// A dimension with <b>no</b> configured ordering, and any label <b>not present</b> in its ordering, are
/// <b>unranked</b>: an ordered comparison over an unranked dimension, or over a row carrying an unranked value for the
/// dimension, <b>denies</b> (fail-closed) — only equality/set-membership/<c>$claims.*</c> rules apply to an unordered
/// dimension. This is the security-safe default: a misconfigured or absent ordering can never widen reach.
/// </para>
/// <para>
/// Comparison is ordinal (case-sensitive), matching the bytes-to-bytes rule evaluator. The instance is immutable;
/// <see cref="Empty"/> is the no-orderings default (every ordered comparison denies) used by the parameterless
/// <see cref="SecurityRule.Compile(string)"/>.
/// </para>
/// </remarks>
public sealed class SecurityLabelOrderings
{
    /// <summary>The no-orderings default: every ordered comparison (<c>&lt;</c>/<c>&lt;=</c>/<c>&gt;</c>/<c>&gt;=</c>) denies.</summary>
    public static readonly SecurityLabelOrderings Empty = new(new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));

    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> orderings;

    /// <summary>Initializes a new instance of the <see cref="SecurityLabelOrderings"/> class.</summary>
    /// <param name="orderings">Tag key → its labels in ascending order. Copied defensively (ordinal keys).</param>
    public SecurityLabelOrderings(IReadOnlyDictionary<string, IReadOnlyList<string>> orderings)
    {
        ArgumentNullException.ThrowIfNull(orderings);
        var copy = new Dictionary<string, IReadOnlyList<string>>(orderings.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<string>> entry in orderings)
        {
            ArgumentNullException.ThrowIfNull(entry.Value);
            copy[entry.Key] = [.. entry.Value];
        }

        this.orderings = copy;
    }

    /// <summary>Gets the ascending label ordering configured for <paramref name="dimension"/>.</summary>
    /// <param name="dimension">The security-tag key (dimension).</param>
    /// <param name="ascending">The labels in ascending order on success; an empty list otherwise.</param>
    /// <returns><see langword="true"/> if the dimension has a configured ordering.</returns>
    public bool TryGetOrdering(string dimension, out IReadOnlyList<string> ascending)
    {
        if (this.orderings.TryGetValue(dimension, out IReadOnlyList<string>? labels) && labels.Count > 0)
        {
            ascending = labels;
            return true;
        }

        ascending = [];
        return false;
    }
}