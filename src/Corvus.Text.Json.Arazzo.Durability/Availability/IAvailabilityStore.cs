// <copyright file="IAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Availability;

/// <summary>
/// Durable storage for the availability matrix (design §7.8): which workflow versions are made available in which
/// deployment environments. An entry is keyed by (<c>baseWorkflowId</c>, <c>versionNumber</c>, <c>environment</c>); its
/// presence means available. AvailabilityEntry is additive and many-to-many, with no mutable state — an entry is created to
/// make a version available and deleted to withdraw it.
/// </summary>
/// <remarks>
/// <para><strong>Authorization is not the store's concern.</strong> AvailabilityEntry records carry no security tags. Who may
/// make a version available (the target environment's administrators) and whether it is ready (a full source-credential
/// set, §7.7) are enforced at the control-plane surface; the store is a plain keyed persistence seam.</para>
/// <para><strong>Two list axes.</strong> <see cref="ListByVersionAsync"/> returns the environments a version is available
/// in (ordered by environment); <see cref="ListByEnvironmentAsync"/> returns the (workflow, version) pairs available in
/// an environment (ordered by base workflow id then version). Both are keyset-paged with an opaque, backend-scoped token.</para>
/// <para><strong>Ownership.</strong> Read/return methods hand back <strong>pooled documents whose lifetime the caller
/// owns</strong>: dispose the returned <see cref="ParsedJsonDocument{T}"/> / <see cref="AvailabilityPage"/> once read.</para>
/// </remarks>
public interface IAvailabilityStore
{
    /// <summary>Makes a workflow version available in an environment, stamping createdBy/createdAt/etag. Idempotent: if the
    /// version is already available in the environment, the existing entry is returned unchanged.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="actor">The authenticated identity making the version available (for audit).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The entry as a pooled document the caller must dispose, and whether it was newly created (vs. already available).</returns>
    ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken);

    /// <summary>Gets the availability entry for a version in an environment, or <see langword="null"/> if the version is
    /// not available there.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The entry as a pooled document the caller must dispose, or <see langword="null"/>.</returns>
    ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken);

    /// <summary>Withdraws a version's availability in an environment.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if an entry was removed; <see langword="false"/> if the version was not available there.</returns>
    ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken);

    /// <summary>Lists one keyset page of the environments a version is available in, ascending by <c>environment</c>.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="limit">The maximum number of entries to return (the store treats a non-positive value as its default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="AvailabilityPage.NextPageToken"/>, or undefined for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (entries + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken);

    /// <summary>Lists one keyset page of the (workflow, version) pairs available in an environment, ascending by
    /// <c>baseWorkflowId</c> then <c>versionNumber</c>.</summary>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="limit">The maximum number of entries to return (the store treats a non-positive value as its default page size).</param>
    /// <param name="pageToken">The opaque token (its JSON value) from a previous page's <see cref="AvailabilityPage.NextPageToken"/>, or undefined for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (entries + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken);

    /// <summary>Counts the environments a version is available in (the by-version axis), bounded by <paramref name="cap"/>:
    /// the total behind that list's footer, without returning any entry rows.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="cap">The inclusive upper bound on the reported count; once <paramref name="cap"/> matching entries have been seen the count saturates and <c>Capped</c> is <see langword="true"/> (a non-positive value uses the store's default page size as the cap).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded count and whether it was capped (the true total meets or exceeds <paramref name="cap"/>).</returns>
    /// <remarks>The default counts a single bounded page of <paramref name="cap"/> + 1 over
    /// <see cref="ListByVersionAsync"/>; a backend overrides it with a native bounded <c>COUNT</c> so no entry rows are parsed.</remarks>
    async ValueTask<(int Count, bool Capped)> CountByVersionAsync(string baseWorkflowId, int versionNumber, int cap, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int bound = cap > 0 ? cap : AvailabilityPage.DefaultPageSize;
        using AvailabilityPage page = await this.ListByVersionAsync(baseWorkflowId, versionNumber, bound + 1, default, cancellationToken).ConfigureAwait(false);
        int n = page.Entries.Count;
        return n > bound ? (bound, true) : (n, false);
    }

    /// <summary>Counts the (workflow, version) pairs available in an environment (the by-environment axis), bounded by
    /// <paramref name="cap"/>: the total behind that list's footer, without returning any entry rows.</summary>
    /// <param name="environment">The deployment environment.</param>
    /// <param name="cap">The inclusive upper bound on the reported count; once <paramref name="cap"/> matching entries have been seen the count saturates and <c>Capped</c> is <see langword="true"/> (a non-positive value uses the store's default page size as the cap).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bounded count and whether it was capped (the true total meets or exceeds <paramref name="cap"/>).</returns>
    /// <remarks>The default counts a single bounded page of <paramref name="cap"/> + 1 over
    /// <see cref="ListByEnvironmentAsync"/>; a backend overrides it with a native bounded <c>COUNT</c> so no entry rows are parsed.</remarks>
    async ValueTask<(int Count, bool Capped)> CountByEnvironmentAsync(string environment, int cap, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int bound = cap > 0 ? cap : AvailabilityPage.DefaultPageSize;
        using AvailabilityPage page = await this.ListByEnvironmentAsync(environment, bound + 1, default, cancellationToken).ConfigureAwait(false);
        int n = page.Entries.Count;
        return n > bound ? (bound, true) : (n, false);
    }
}