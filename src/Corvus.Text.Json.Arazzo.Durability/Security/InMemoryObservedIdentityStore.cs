// <copyright file="InMemoryObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The reference in-memory <see cref="IObservedIdentityStore"/> — the conformance baseline and a ready store for
/// single-node / local-development hosts. Each identity is held as its UTF-8 JSON document (the "push JSON to the store"
/// shape every backend persists), keyed by (subjectKind, subjectValue); searches hand back a pooled
/// <see cref="ObservedIdentityPage"/> the caller disposes.
/// </summary>
/// <remarks>
/// <para><strong>Allocation ledger.</strong> <see cref="SeenAsync"/>: one owned <c>byte[]</c> (the persisted document,
/// produced through a pooled scratch buffer — <c>byte[]</c> only at the leaf); on update it also realizes the existing
/// provenance into a small <see cref="List{T}"/> to union (occasional write path, not warm-hot). <see cref="SearchAsync"/>:
/// a <strong>bounded top-(pageSize+1) selection</strong> — O(pageSize) result allocation regardless of corpus size,
/// mirroring a backend's <c>ORDER BY … LIMIT</c>: a capped buffer holding <em>references</em> into the dictionary (never
/// copies of the key strings or document bytes), the pooled result documents the caller owns (the page itself), and — when
/// more remain — one continuation-token string. Each matching candidate is materialised once (a transient pooled document,
/// immediately disposed) to apply the §17.1 reach predicate — the same per-row reach cost the credential store pays; a
/// backend pushes both the prefix/keyset and the reach predicate to its index. It never sorts the whole matching set.</para>
/// </remarks>
public sealed class InMemoryObservedIdentityStore : IObservedIdentityStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<(string Kind, string Value), byte[]> identities = new();

    // The collision-probe index (design §16.5.4): identity digest → the keys that hold it, plus each key's current digest
    // (so a re-sighting that changes the identity retracts the old digest). The in-memory analogue of a backend's indexed
    // digest column — FindIdentityConflictAsync is an O(1) lookup, never a scan.
    private readonly Dictionary<string, HashSet<(string Kind, string Value)>> byDigest = new();
    private readonly Dictionary<(string Kind, string Value), string> digests = new();
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="InMemoryObservedIdentityStore"/> class.</summary>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    public InMemoryObservedIdentityStore(TimeProvider? timeProvider = null)
        => this.timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc/>
    public ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);

        // The dictionary key is a string (the in-memory storage leaf), so the kind reifies once here to its interned token.
        string kindToken = kind.ToToken();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // The dictionary is string-keyed (the in-memory storage leaf — the only place that needs a concrete form), so the
        // value reifies once here as the key; the document body is serialized bytes-to-bytes from the value/label JSON values.
        string valueKey = (string)value;

        lock (this.gate)
        {
            (string, string) key = (kindToken, valueKey);

            // Upsert (preserve firstSeen, bump lastSeen, union provenance, refresh label/identity/completeness) vs first
            // sighting — the merge is the shared serialization every backend uses, so the semantics are identical.
            byte[] json = this.identities.TryGetValue(key, out byte[]? existing)
                ? ObservedIdentitySerialization.SerializeUpserted(existing, kind, value, label, identity, complete, now, provenance)
                : ObservedIdentitySerialization.SerializeNew(kind, value, label, identity, complete, now, provenance);

            this.identities[key] = json;
            this.ReindexDigest(key, identity);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<ObservedIdentity>?> FindIdentityConflictAsync(ObservedIdentity.GranteeKind kind, JsonString value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        // The empty (unscoped) identity never collides; otherwise look the digest up in the index (O(1), no scan) and
        // return the first holder that is a DIFFERENT grantee — a non-unique identity the authoring path must refuse.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return new ValueTask<ParsedJsonDocument<ObservedIdentity>?>((ParsedJsonDocument<ObservedIdentity>?)null);
        }

        // The dictionary key is a string (the storage leaf), so the authored value reifies once here for the self-compare.
        (string Kind, string Value) self = (kind.ToToken(), (string)value);
        lock (this.gate)
        {
            if (this.byDigest.TryGetValue(digest, out HashSet<(string Kind, string Value)>? holders))
            {
                foreach ((string Kind, string Value) holder in holders)
                {
                    if (holder == self)
                    {
                        continue;
                    }

                    // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its
                    // kind/value/label live in the record, so nothing is reified into a separate POCO here.
                    if (this.identities.TryGetValue(holder, out byte[]? doc))
                    {
                        return new ValueTask<ParsedJsonDocument<ObservedIdentity>?>(PersistedJson.ToPooledDocument<ObservedIdentity>(doc));
                    }
                }
            }
        }

        return new ValueTask<ParsedJsonDocument<ObservedIdentity>?>((ParsedJsonDocument<ObservedIdentity>?)null);
    }

    // Retract this key's previous digest (if the identity changed on a re-sighting) and index its new one. The empty
    // identity has no digest and is simply absent from the index (it never collides).
    private void ReindexDigest((string Kind, string Value) key, SecurityTagSet identity)
    {
        if (this.digests.Remove(key, out string? oldDigest) && this.byDigest.TryGetValue(oldDigest, out HashSet<(string Kind, string Value)>? oldHolders))
        {
            oldHolders.Remove(key);
            if (oldHolders.Count == 0)
            {
                this.byDigest.Remove(oldDigest);
            }
        }

        if (SecurityIdentityDigest.Compute(identity) is { } digest)
        {
            this.digests[key] = digest;
            if (!this.byDigest.TryGetValue(digest, out HashSet<(string Kind, string Value)>? holders))
            {
                holders = [];
                this.byDigest[digest] = holders;
            }

            holders.Add(key);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;

        // Undefined kind means "all kinds"; a defined kind reifies once to its interned token for the ordinal key match.
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // The dictionary keys are strings (the in-memory storage leaf), so the prefix reifies once for the ordinal
        // StartsWith (undefined prefix matches all) — a backend pushes the prefix predicate to its index instead.
        string prefixStr = prefix.IsNotUndefined() ? (string)prefix : string.Empty;

        // The opaque page token arrives as its JSON value; decode the (subjectValue, subjectKind) cursor straight from the
        // request UTF-8 (no managed token string). Init to empties so the unused-when-!hasCursor tuple is never null.
        (string SubjectValue, string SubjectKind) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = ObservedIdentityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // The keyset order every backend pages by is (subjectValue, subjectKind). Rather than materialise + sort the whole
        // matching set, keep only the pageSize+1 SMALLEST past-cursor matches in a capped, insertion-sorted buffer — the
        // in-memory analogue of ORDER BY (subjectValue, subjectKind) LIMIT pageSize+1. The +1 row detects "more remain"
        // (and seeds the next-page token); the buffer holds references into the dictionary, never copies.
        int cap = pageSize + 1;
        var top = new List<(string Value, string Kind, byte[] Json)>(cap);

        // The read reach is constant for the call; an unrestricted (System) reach admits everything without touching a
        // single candidate's tags — only a scoped reach pays the per-candidate materialise-and-check cost (§17.1).
        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        lock (this.gate)
        {
            foreach (KeyValuePair<(string Kind, string Value), byte[]> entry in this.identities)
            {
                if (kindToken is not null && !string.Equals(entry.Key.Kind, kindToken, StringComparison.Ordinal))
                {
                    continue;
                }

                if (prefixStr.Length > 0 && !entry.Key.Value.StartsWith(prefixStr, StringComparison.Ordinal))
                {
                    continue;
                }

                if (hasCursor && CompareKey(entry.Key.Value, entry.Key.Kind, cursor.SubjectValue, cursor.SubjectKind) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                // Reach filter (§17.1): a scoped caller discovers an identity only when their read-reach admits its tags —
                // materialise the candidate to read its identity tags, the same per-row reach idiom the credential store
                // uses. An unrestricted reach skips this entirely (no materialisation), so System search keeps its floor.
                if (readReach is not null)
                {
                    bool admitted;
                    using (ParsedJsonDocument<ObservedIdentity> candidate = PersistedJson.ToPooledDocument<ObservedIdentity>(entry.Value))
                    {
                        admitted = readReach.IsSatisfiedBy(candidate.RootElement.IdentityTagsValue);
                    }

                    if (!admitted)
                    {
                        continue; // not reach-visible to this caller (non-disclosing)
                    }
                }

                // Offer to the bounded top-K: insert if there is room, else only if it is smaller than the current largest.
                if (top.Count < cap)
                {
                    InsertSorted(top, entry.Key.Value, entry.Key.Kind, entry.Value);
                }
                else if (CompareKey(entry.Key.Value, entry.Key.Kind, top[cap - 1].Value, top[cap - 1].Kind) < 0)
                {
                    top.RemoveAt(cap - 1);
                    InsertSorted(top, entry.Key.Value, entry.Key.Kind, entry.Value);
                }
            }

            int resultCount = Math.Min(pageSize, top.Count);
            var docs = new PooledDocumentList<ObservedIdentity>(resultCount);
            try
            {
                for (int i = 0; i < resultCount; i++)
                {
                    docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(top[i].Json));
                }

                // A (pageSize+1)th smallest match exists → there is a next page; the token resumes after the last included
                // row, encoded straight into the page's pooled buffer (no token string).
                ObservedIdentityPage page = top.Count > pageSize
                    ? ObservedIdentityPage.Create(docs, top[pageSize - 1].Value, top[pageSize - 1].Kind)
                    : ObservedIdentityPage.Create(docs);

                return new ValueTask<ObservedIdentityPage>(page);
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
    }

    // The stable total order every backend pages by: subjectValue, then the subjectKind tie-breaker.
    private static int CompareKey(string v1, string k1, string v2, string k2)
    {
        int c = string.CompareOrdinal(v1, v2);
        return c != 0 ? c : string.CompareOrdinal(k1, k2);
    }

    // Inserts (value, kind, json) into the capped buffer at its sorted position (linear from the end — the buffer is
    // pageSize+1 small, and it stays within capacity so no backing array is reallocated).
    private static void InsertSorted(List<(string Value, string Kind, byte[] Json)> buffer, string value, string kind, byte[] json)
    {
        int i = buffer.Count;
        while (i > 0 && CompareKey(value, kind, buffer[i - 1].Value, buffer[i - 1].Kind) < 0)
        {
            i--;
        }

        buffer.Insert(i, (value, kind, json));
    }
}