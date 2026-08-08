// <copyright file="ISecurityLabelIndex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A backend's secondary index from security-tag label to row id (design §14.4) — the whole of what a store
/// must supply for <see cref="SecurityLabelQueryResolver"/> to narrow a reach query to a candidate set. A
/// backend implements these two lookups against whatever structure it maintains (a label-partitioned table, a
/// set per label, a key-value bucket per label) and needs no knowledge of the rule grammar.
/// </summary>
/// <remarks>
/// <para>
/// <b>The index may over-report and must not under-report.</b> A returned id whose row does not actually carry
/// the label is harmless: the caller evaluates <see cref="SecurityFilter"/> exactly against the loaded row and
/// discards it. A <em>missing</em> id hides a row the principal is entitled to see. That asymmetry decides the
/// write path: create the index entry no later than the row and remove it no earlier, so a failure between the
/// two writes leaves a stale entry rather than an absent one.
/// </para>
/// <para>
/// Lookups are on the query path, so an implementation should answer them from the index alone and never load
/// the rows themselves. Returning the ids in any order is fine; the caller re-imposes its own ordering.
/// </para>
/// </remarks>
public interface ISecurityLabelIndex
{
    /// <summary>Gets the ids of rows carrying the security tag (<paramref name="key"/>, <paramref name="value"/>).</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="value">The tag value.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The row ids, empty when none carries the label.</returns>
    ValueTask<IReadOnlyCollection<string>> LookupLabelAsync(string key, string value, CancellationToken cancellationToken);

    /// <summary>Gets the ids of rows carrying a security tag with key <paramref name="key"/>, whatever its value.</summary>
    /// <param name="key">The tag key.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The row ids, empty when none carries the key.</returns>
    ValueTask<IReadOnlyCollection<string>> LookupKeyAsync(string key, CancellationToken cancellationToken);
}