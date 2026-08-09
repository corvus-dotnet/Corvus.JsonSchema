// <copyright file="NatsJetStreamObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the
/// control plane has observed, each persisted as its <see cref="ObservedIdentity"/> document under a single KV key per
/// (SubjectKind, SubjectValue). A sighting upserts; the prefix typeahead is a keyset page in
/// <c>(subjectValue, subjectKind)</c> order with the caller's read-reach (§17.1) applied.
/// </summary>
/// <remarks>
/// <para>Each KV key is <c>oid.{base64url(subjectValue)}.{base64url(subjectKind)}</c>: Base64Url over the UTF-8 of each
/// component yields only the restricted set of characters a NATS KV key permits, so a value or kind containing dots,
/// control characters, etc. round-trips safely. <c>subjectValue</c> leads so the keys group by the prefix-searched
/// column, mirroring how the catalog and source-credential stores derive and prefix-scan their keys.</para>
/// <para>The KV store has no server-side ordering or filtering, so — mirroring the catalog store's scan-and-page
/// approach — <see cref="SearchAsync"/> scans the bucket, applies the kind filter, the prefix lower bound, and the
/// exact §17.1 read-reach over each candidate's persisted identity tags, sorts the matches by the composite sort
/// key with <see cref="string.CompareOrdinal(string, string)"/>, keyset-pages here, and detects a next page with one
/// extra match. A reach-scoped search first narrows to the candidates the security-label entries admit (§14.4) —
/// per-label keys in this same bucket under the <c>v.</c>/<c>k.</c> namespaces, maintained by
/// <see cref="SeenAsync"/>'s tag diff and resolved by subject-filtered key listings — so identities outside the
/// caller's reach are never read. An unrestricted (System) reach skips both entirely. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, CancellationToken)"/>.</para>
/// </remarks>
public sealed class NatsJetStreamObservedIdentityStore : IObservedIdentityStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_observed_identities";
    private const string KeyPrefix = "oid.";

    // The collision-probe secondary index (design §16.5.4). The KV bucket has no server-side query, so — mirroring the
    // runner registry's hosting index — the digest membership is itself a set of keys: one member key per (digest,
    // grantee) under DigestPrefix, listed by a subject-wildcard filter (an indexed seek, never a bucket scan), plus a
    // single DigestOfPrefix key per grantee recording its current digest so a re-sighting can retract the old member.
    private const string DigestPrefix = "digx.";
    private const string DigestOfPrefix = "digof.";

    // The composite sort key separator: the null control char cannot appear in a subjectValue/subjectKind token, so it
    // splices the two key parts into one string that orders ordinally exactly as (subjectValue, subjectKind) does.
    private const char SortKeySeparator = '\0';

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly NatsSecurityLabelIndex labelIndex;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamObservedIdentityStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;

        // The label entries live in this same bucket (the v./k. key namespaces are disjoint from oid./digx./digof.),
        // so the index shares the store handle and no extra bucket or privilege is needed.
        this.labelIndex = new NatsSecurityLabelIndex(store);
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the observed-identity KV bucket (requires stream-management rights); run once at deploy time.</summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the observed-identity KV bucket over a caller-supplied connection.</summary>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(Bucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned KV bucket.</summary>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamObservedIdentityStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamObservedIdentityStore(connection, store, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens the store for operation over a caller-supplied connection (the caller retains ownership).</summary>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamObservedIdentityStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamObservedIdentityStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        string kindToken = kind.ToToken();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // The KV key (and the digest-index keys) are built from the value as a string, so the value reifies once here as
        // those storage keys; the document body is serialized bytes-to-bytes from the value/label JSON values at the
        // synchronous serialize calls below.
        string valueKey = (string)value;
        string key = Key(kindToken, valueKey);

        // Read-merge-write: a first sighting inserts; an existing one preserves firstSeen, bumps lastSeen, unions
        // provenance, and refreshes label/identity/completeness (the shared merge). KV Put overwrites either way.
        // The driver mints the existing document array (the KV byte[] value); parse it NON-COPYING (it stays alive through
        // the synchronous merge) — no pooled read buffer, no copy.
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        byte[] json;
        SecurityTagSet previousTags = SecurityTagSet.Empty;
        if (entry is { Value: { } existing })
        {
            using ParsedJsonDocument<ObservedIdentity> current = ParsedJsonDocument<ObservedIdentity>.Parse(existing.AsMemory());
            json = ObservedIdentitySerialization.SerializeUpserted(current.RootElement, kind, value, label, identity, complete, now, provenance);
            previousTags = SecurityTagSet.CopyFrom(current.RootElement.IdentityTags);
        }
        else
        {
            json = ObservedIdentitySerialization.SerializeNew(kind, value, label, identity, complete, now, provenance);
        }

        // §14.4 security-label entries, diffed against the tags the document already read carries — the same
        // retraction discipline as the digest re-index below, in the ordering that keeps an imprecise index safe:
        // entries for the NEW tags land before the document carries them and entries for the removed ones are
        // dropped only after it stops, so an interrupted sighting can only leave a stale entry — which the exact
        // reach evaluation discards — never an identity whose current tags have no entry, which would hide it
        // from the reach it belongs to.
        string rowId = SortKey(valueKey, kindToken);
        HashSet<string> desiredEntries = NatsSecurityLabels.EntryKeysFor(identity, rowId);
        HashSet<string> previousEntries = NatsSecurityLabels.EntryKeysFor(previousTags, rowId);
        foreach (string entryKey in desiredEntries)
        {
            if (!previousEntries.Contains(entryKey))
            {
                await this.store.PutAsync(entryKey, Array.Empty<byte>(), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.ReindexDigestAsync(kindToken, valueKey, identity, cancellationToken).ConfigureAwait(false);

        foreach (string entryKey in previousEntries)
        {
            if (!desiredEntries.Contains(entryKey))
            {
                await this.DeleteAsync(entryKey, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> FindBroadeningOverlapsAsync(ObservedIdentity.GranteeKind kind, JsonString value, SecurityTagSet identity, int limit, CancellationToken cancellationToken)
    {
        int cap = limit > 0 ? limit : 1;

        // The empty identity is a subset of every set but grants no administration scope, so it broadens nothing.
        if (identity.IsEmpty)
        {
            return ObservedIdentityPage.Create(new PooledDocumentList<ObservedIdentity>(0));
        }

        string kindToken = kind.ToToken();
        string valueKey = (string)value;

        // The KV bucket has no queryable identity-tag index (reach is applied in memory), so the strict-superset overlap
        // is a client-side scan: enumerate the grantee keys (unordered), read each document, parse its identity, and keep
        // those whose tag set strictly contains the authored identity (skipping the authored (kind, value)); then order by
        // the contract's (subjectValue, subjectKind) key and take the cap.
        var matches = new List<(string SortKey, byte[] Json)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal) || !TryParseKey(key, out (string SubjectValue, string SubjectKind) parts))
            {
                continue;
            }

            if (string.Equals(parts.SubjectKind, kindToken, StringComparison.Ordinal) && string.Equals(parts.SubjectValue, valueKey, StringComparison.Ordinal))
            {
                continue; // the authored grantee itself
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            bool broadens;
            using (ParsedJsonDocument<ObservedIdentity> candidate = PersistedJson.ToPooledDocument<ObservedIdentity>(bytes))
            {
                SecurityTagSet candidateTags = candidate.RootElement.IdentityTags.IsNotUndefined()
                    ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(candidate.RootElement.IdentityTags).Memory)
                    : SecurityTagSet.Empty;
                broadens = identity.IsSubsetOf(candidateTags) && !candidateTags.IsSubsetOf(identity);
            }

            if (broadens)
            {
                matches.Add((SortKey(parts.SubjectValue, parts.SubjectKind), bytes));
            }
        }

        matches.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));

        var docs = new PooledDocumentList<ObservedIdentity>(Math.Min(cap, matches.Count));
        try
        {
            for (int i = 0; i < matches.Count && i < cap; i++)
            {
                docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(matches[i].Json));
            }

            return ObservedIdentityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // Candidates' subjectValue is recovered as a string from each key, so the prefix reifies once for the in-memory
        // lower-bound StartsWith predicate (undefined prefix matches all).
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

        string? afterSortKey = hasCursor ? SortKey(cursor.SubjectValue, cursor.SubjectKind) : null;

        // The read reach (§17.1) is constant for the call; an unrestricted (System) reach admits everything without
        // touching a single candidate's tags — only a scoped reach pays the per-candidate materialise-and-check cost.
        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        // §14.4: narrow to the candidate identities the label entries admit before reading anything. A null answer
        // means the index could not narrow and the key scan runs as it always did; an empty one means no identity
        // qualifies, so the page is empty without a single document read. The candidate id is the composite sort
        // key, so the kind/prefix/cursor filters apply to a candidate BEFORE its fetch, and a stale entry resolves
        // to a key whose document is gone, which the absent-entry skip discards.
        IReadOnlySet<string>? candidates = readReach is null
            ? null
            : await SecurityLabelQueryResolver.ResolveAsync(
                readReach.ToPredicate(SecurityLabelQueryEmitter.Instance), this.labelIndex, cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return ObservedIdentityPage.Create(new PooledDocumentList<ObservedIdentity>(0));
        }

        // The KV bucket has no server-side ordering or filtering, so — mirroring the catalog store — collect matches,
        // sort by the composite sort key, and keyset-page here. Each match carries its bytes so the page can materialise
        // its pooled documents without a second fetch.
        var matches = new List<(string SortKey, string Value, string Kind, byte[] Json)>();
        await foreach ((string key, (string SubjectValue, string SubjectKind) parts) in this.ScanCandidateKeysAsync(candidates, cancellationToken).ConfigureAwait(false))
        {
            if (kindToken is not null && !string.Equals(parts.SubjectKind, kindToken, StringComparison.Ordinal))
            {
                continue;
            }

            if (prefixStr.Length > 0 && !parts.SubjectValue.StartsWith(prefixStr, StringComparison.Ordinal))
            {
                continue;
            }

            string sortKey = SortKey(parts.SubjectValue, parts.SubjectKind);

            // Seek strictly past the cursor in (subjectValue, subjectKind) order — rows at or before it were returned
            // in an earlier page.
            if (afterSortKey is not null && string.CompareOrdinal(sortKey, afterSortKey) <= 0)
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            // Reach filter (§17.1): the exact evaluation over the candidate's persisted tags — the label entries
            // only propose a superset (§14.4), so this check still decides every row a caller sees. An
            // unrestricted reach skips this entirely (no materialisation), so System search keeps its floor.
            if (readReach is not null)
            {
                bool admitted;
                using (ParsedJsonDocument<ObservedIdentity> candidate = PersistedJson.ToPooledDocument<ObservedIdentity>(bytes))
                {
                    SecurityTagSet identityTags = candidate.RootElement.IdentityTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(candidate.RootElement.IdentityTags).Memory)
                        : SecurityTagSet.Empty;
                    admitted = readReach.IsSatisfiedBy(identityTags);
                }

                if (!admitted)
                {
                    continue; // not reach-visible to this caller (non-disclosing)
                }
            }

            matches.Add((sortKey, parts.SubjectValue, parts.SubjectKind, bytes));
        }

        matches.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));

        string? nextValue = null, nextKind = null;
        if (matches.Count > pageSize)
        {
            // A (pageSize+1)th match exists → there is a next page; the token resumes after the last included row.
            (_, string lastValue, string lastKind, _) = matches[pageSize - 1];
            nextValue = lastValue;
            nextKind = lastKind;
        }

        int resultCount = Math.Min(pageSize, matches.Count);
        var docs = new PooledDocumentList<ObservedIdentity>(resultCount);
        try
        {
            for (int i = 0; i < resultCount; i++)
            {
                docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(matches[i].Json));
            }

            return nextValue is not null ? ObservedIdentityPage.Create(docs, nextValue, nextKind!) : ObservedIdentityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<ObservedIdentity>?> FindIdentityConflictAsync(ObservedIdentity.GranteeKind kind, JsonString value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        // The empty (unscoped) identity never collides; otherwise seek the digest membership index for a grantee whose
        // identity is set-equal (same digest) but whose (kind, value) differs — a non-unique identity the authoring path
        // refuses. This probe runs at FULL reach (a cross-tenant collision must be visible), so — unlike SearchAsync — it
        // applies no read-reach filter. The filtered key listing is an indexed seek over only this digest's members.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        string kindToken = kind.ToToken();

        // The member key's subjectValue is recovered as a string, so the value reifies once for the self-match comparison
        // that excludes the grantee being authored.
        string valueKey = (string)value;
        string memberFilter = DigestMemberPrefix(digest) + ">";
        await foreach (string memberKey in this.store.GetKeysAsync([memberFilter], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!TryParseDigestMemberKey(memberKey, out (string Kind, string Value) member))
            {
                continue;
            }

            if (string.Equals(member.Kind, kindToken, StringComparison.Ordinal) && string.Equals(member.Value, valueKey, StringComparison.Ordinal))
            {
                continue; // the grantee being authored — not a collision with itself
            }

            // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its kind/value/label
            // live in the record, so nothing is reified into a separate POCO here. A tombstoned/raced member key just
            // yields no document — skip it.
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(member.Kind, member.Value), cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } bytes })
            {
                return PersistedJson.ToPooledDocument<ObservedIdentity>(bytes);
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string value) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

    // The composite sort key for keyset paging: subjectValue, the separator, then the subjectKind tie-breaker. The
    // separator cannot appear in either token, so CompareOrdinal over this string is identical to comparing the
    // (subjectValue, subjectKind) tuple lexicographically — the order the contract pages by.
    private static string SortKey(string subjectValue, string subjectKind)
        => string.Concat(subjectValue, SortKeySeparator.ToString(), subjectKind);

    // The KV key for a single identity: the namespace, then Base64Url(subjectValue), Base64Url(subjectKind),
    // dot-separated. Base64Url emits only [A-Za-z0-9_-], all valid KV key characters, so the components — which may
    // contain dots, control characters, etc. — round-trip safely, and subjectValue leads so keys group by the
    // prefix-searched column.
    private static string Key(string subjectKind, string subjectValue)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(subjectValue)}.{Enc(subjectKind)}");

    // Inverts Key: splits oid.{Enc(subjectValue)}.{Enc(subjectKind)} on the dot separators and Base64Url-decodes each
    // part back to its original string, so the ordering tuple is recovered from the key alone.
    private static bool TryParseKey(string key, out (string SubjectValue, string SubjectKind) parts)
    {
        parts = default;
        ReadOnlySpan<char> body = key.AsSpan(KeyPrefix.Length);
        int dot = body.IndexOf('.');
        if (dot < 0)
        {
            return false;
        }

        ReadOnlySpan<char> valuePart = body[..dot];
        ReadOnlySpan<char> kindPart = body[(dot + 1)..];
        if (kindPart.IndexOf('.') >= 0)
        {
            return false;
        }

        try
        {
            parts = (Dec(valuePart.ToString()), Dec(kindPart.ToString()));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // The membership-index key-space for one digest: DigestPrefix, Base64Url(digest), then a dot. Each member appends
    // Base64Url(subjectKind).Base64Url(subjectValue), so the subject-wildcard filter DigestMemberPrefix(d) + ">" lists
    // exactly the grantees holding digest d (an indexed seek), and the trailing '>' spans both encoded member segments.
    private static string DigestMemberPrefix(string digest)
        => string.Create(CultureInfo.InvariantCulture, $"{DigestPrefix}{Enc(digest)}.");

    // A single (digest, grantee) membership key: DigestMemberPrefix(digest) + Base64Url(subjectKind).Base64Url(subjectValue).
    private static string DigestMemberKey(string digest, string subjectKind, string subjectValue)
        => string.Create(CultureInfo.InvariantCulture, $"{DigestMemberPrefix(digest)}{Enc(subjectKind)}.{Enc(subjectValue)}");

    // The per-grantee tracking key holding the grantee's current digest, so a re-sighting that changes the identity knows
    // which old membership key to retract: DigestOfPrefix, Base64Url(subjectKind).Base64Url(subjectValue).
    private static string DigestOfKey(string subjectKind, string subjectValue)
        => string.Create(CultureInfo.InvariantCulture, $"{DigestOfPrefix}{Enc(subjectKind)}.{Enc(subjectValue)}");

    // Inverts DigestMemberKey: recovers (subjectKind, subjectValue) from the last two encoded segments of a membership
    // key (the digest segment in between is not needed — the filter already scoped the listing to one digest).
    private static bool TryParseDigestMemberKey(string key, out (string Kind, string Value) member)
    {
        member = default;
        int lastDot = key.LastIndexOf('.');
        if (lastDot < 0)
        {
            return false;
        }

        int kindDot = key.LastIndexOf('.', lastDot - 1);
        if (kindDot < 0)
        {
            return false;
        }

        try
        {
            string kind = Dec(key[(kindDot + 1)..lastDot]);
            string value = Dec(key[(lastDot + 1)..]);
            member = (kind, value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Maintains the collision-probe index for a (kind, value) on a sighting: retract the previous membership (if the
    // identity changed since the last sighting, so the OLD identity no longer reports a collision) and record the new
    // one. The empty identity has no digest, so it is simply absent from the index (it never collides) — and a former
    // non-empty identity becoming empty still retracts via the tracking key. Best-effort, matching SeenAsync's last-
    // writer-wins document Put.
    private async ValueTask ReindexDigestAsync(string kindToken, string value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        string digestOfKey = DigestOfKey(kindToken, value);
        string? newDigest = SecurityIdentityDigest.Compute(identity);

        NatsKVEntry<byte[]>? tracked = await this.TryGetAsync(digestOfKey, cancellationToken).ConfigureAwait(false);
        string? oldDigest = tracked is { Value: { } trackedBytes } ? Encoding.UTF8.GetString(trackedBytes) : null;
        if (oldDigest is not null && !string.Equals(oldDigest, newDigest, StringComparison.Ordinal))
        {
            await this.DeleteAsync(DigestMemberKey(oldDigest, kindToken, value), cancellationToken).ConfigureAwait(false);
        }

        if (newDigest is null)
        {
            // Empty identity: drop the tracking key (a previous non-empty member was retracted above) and stay unindexed.
            if (oldDigest is not null)
            {
                await this.DeleteAsync(digestOfKey, cancellationToken).ConfigureAwait(false);
            }

            return;
        }

        await this.store.PutAsync(DigestMemberKey(newDigest, kindToken, value), Array.Empty<byte>(), cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.store.PutAsync(digestOfKey, Encoding.UTF8.GetBytes(newDigest), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    // Streams the (key, parsed parts) pairs a search should consider. With no candidate set this is the bucket
    // key scan it always was (skipping the digest/label index namespaces); with one, each candidate sort key is
    // split back into its (value, kind) parts and addressed directly, so identities outside the reach are never
    // listed, never parsed and never read.
    private async IAsyncEnumerable<(string Key, (string SubjectValue, string SubjectKind) Parts)> ScanCandidateKeysAsync(
        IReadOnlySet<string>? candidates,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (candidates is not null)
        {
            foreach (string candidate in candidates)
            {
                int separator = candidate.IndexOf(SortKeySeparator, StringComparison.Ordinal);
                if (separator < 0)
                {
                    continue;
                }

                string subjectValue = candidate[..separator];
                string subjectKind = candidate[(separator + 1)..];
                yield return (Key(subjectKind, subjectValue), (subjectValue, subjectKind));
            }

            yield break;
        }

        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (key.StartsWith(KeyPrefix, StringComparison.Ordinal) && TryParseKey(key, out (string SubjectValue, string SubjectKind) parts))
            {
                yield return (key, parts);
            }
        }
    }

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await this.store.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            return null;
        }
    }

    private async ValueTask DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await this.store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }
}