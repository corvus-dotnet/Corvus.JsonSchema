// <copyright file="IObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Durable storage for observed identities (design §16.5.4): a projection of the distinct grantees the control plane has
/// seen — via an access request, a catalogued version's author, an administrator grant, or an entitlement grant — each
/// resolved to its exact deployment-stamped <c>sys:</c> identity. It backs the <em>store-indexed</em>, prefix-searchable
/// typeahead the grantee picker offers, so an operator names a real identity instead of hand-assembling a
/// <c>{dimension, value}</c> tuple that could silently match no one or over-grant.
/// </summary>
/// <remarks>
/// <para><strong>Trust boundary.</strong> This store holds only <c>sys:</c> identity tags (authorization metadata),
/// never secrets, so it persists as plain JSON like every other entity. It is a write-cheap projection, not a system of
/// record: a sighting upserts; nothing here grants anything by itself.</para>
/// <para><strong>Indexed, store-pushed-down (§16.5.4).</strong> <see cref="SearchAsync"/> is a single indexed query — the
/// backend prefix-matches <c>subjectValue</c> and keyset-pages in <c>(subjectValue, subjectKind)</c> order; it never
/// scans the table or unions across other stores. <strong>It is reach-filtered</strong> (design §17.1): the caller's
/// <see cref="AccessContext"/> read-reach gates which identities are visible, exactly like every other store — a caller
/// discovers an identity only when their read-reach admits its domain tags, so a per-workflow administrator cannot
/// enumerate other tenants' identities (the cross-tenant disclosure the §16.5.4 review found).</para>
/// <para>Read methods hand back <strong>pooled documents whose lifetime the caller owns</strong>: dispose the returned
/// <see cref="ObservedIdentityPage"/> once read (clone any value that must outlive the dispose).</para>
/// </remarks>
public interface IObservedIdentityStore
{
    /// <summary>
    /// Records a sighting of <paramref name="identity"/> for the grantee (<paramref name="kind"/>, <paramref name="value"/>),
    /// idempotently: a new (kind, value) is inserted with first/last-seen now; an existing one bumps last-seen, refreshes the
    /// label and identity tags, and unions <paramref name="provenance"/> into its sighting sources. Cheap and best-effort —
    /// a sighting never fails a foreground operation.
    /// </summary>
    /// <param name="kind">The grantee kind.</param>
    /// <param name="value">The grantee value (a subject id, tenant name, role name, or workflow id) — the prefix-searched key part.</param>
    /// <param name="label">An optional human-friendly display label, or <see langword="null"/>.</param>
    /// <param name="identity">The grantee's exact <c>sys:</c> identity (empty for the unscoped/system identity).</param>
    /// <param name="complete">Whether <paramref name="identity"/> is the principal's whole stamped identity (so an exact-equality grant matches), or a best-effort partial mapping (§17.2).</param>
    /// <param name="provenance">Where this identity was seen (e.g. <c>accessRequest</c>, <c>catalogVersion</c>, <c>administrator</c>, <c>grant</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the sighting is recorded.</returns>
    ValueTask SeenAsync(GranteeKind kind, string value, string? label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken);

    /// <summary>
    /// Lists one keyset page of observed identities the caller's read reach admits whose <c>subjectValue</c> starts with
    /// <paramref name="prefix"/> (optionally restricted to <paramref name="kind"/>), ascending by
    /// <c>(subjectValue, subjectKind)</c> — an indexed query pushed down to the store. An identity is admitted iff
    /// <paramref name="context"/> admits its identity tags for <see cref="AccessVerb.Read"/>, so discovery is scoped to
    /// the caller's existing visibility (§17.1). Seeks strictly past <paramref name="pageToken"/> and returns at most
    /// <paramref name="limit"/> identities, emitting an <see cref="ObservedIdentityPage.NextPageToken"/> when more remain.
    /// </summary>
    /// <param name="context">The caller's row-access grant (use <see cref="AccessContext.System"/> for full reach).</param>
    /// <param name="kind">Restrict to one grantee kind, or <see langword="null"/> for all kinds.</param>
    /// <param name="prefix">The case-sensitive <c>subjectValue</c> prefix to match (empty matches all).</param>
    /// <param name="limit">The maximum number of identities to return in the page (a non-positive value is treated as 1).</param>
    /// <param name="pageToken">An opaque token from a previous page's <see cref="ObservedIdentityPage.NextPageToken"/>, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page (matched identities + an optional next-page token), as a disposable batch the caller must dispose.</returns>
    /// <exception cref="FormatException"><paramref name="pageToken"/> is not a valid continuation token.</exception>
    ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, GranteeKind? kind, string prefix, int limit, string? pageToken, CancellationToken cancellationToken);

    /// <summary>
    /// Probes for an identity collision (design §16.5.4): returns an existing grantee whose <c>sys:</c> identity is
    /// set-equal to <paramref name="identity"/> but whose (kind, value) differs from (<paramref name="kind"/>,
    /// <paramref name="value"/>), or <see langword="null"/> if no other grantee holds that identity. The grant-authoring
    /// path calls this to refuse an <em>ambiguous</em> grant — a directory mapper minting a non-unique identity for two
    /// distinct principals. Matched by the identity digest (an indexed seek; see <see cref="SecurityIdentityDigest"/>),
    /// so it never scans.
    /// </summary>
    /// <remarks>
    /// Runs at <strong>full reach</strong>: a collision is unsafe regardless of who can see the conflicting party, so the
    /// probe is deliberately <em>not</em> reach-filtered (unlike <see cref="SearchAsync"/>) — otherwise a cross-tenant
    /// collision, the most dangerous case, would be invisible. The caller decides what (if anything) to disclose; a
    /// generic refusal that does not echo the conflicting party is the safe response. The empty identity (the unscoped
    /// sentinel) never collides and always returns <see langword="null"/>.
    /// </remarks>
    /// <param name="kind">The grantee kind being authored (excluded from the match together with <paramref name="value"/>).</param>
    /// <param name="value">The grantee value being authored (excluded from the match together with <paramref name="kind"/>).</param>
    /// <param name="identity">The resolved <c>sys:</c> identity to check for uniqueness.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The conflicting grantee, or <see langword="null"/> when the identity is unique to (<paramref name="kind"/>, <paramref name="value"/>).</returns>
    ValueTask<ObservedIdentityConflict?> FindIdentityConflictAsync(GranteeKind kind, string value, SecurityTagSet identity, CancellationToken cancellationToken);
}