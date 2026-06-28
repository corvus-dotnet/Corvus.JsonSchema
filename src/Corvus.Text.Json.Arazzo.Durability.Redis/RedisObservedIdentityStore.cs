// <copyright file="RedisObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the control plane has
/// observed, each persisted verbatim as its <see cref="ObservedIdentity"/> document under a per-identity key keyed by
/// (subjectKind, subjectValue), with a global sorted set indexing every identity by its <c>sortKey</c>
/// (<c>subjectValue \0 subjectKind</c>) so the prefix typeahead can be ordered and keyset-paged — the same
/// authoritative-store-with-index pattern <see cref="RedisWorkflowCatalogStore"/> uses for catalogued versions.
/// </summary>
/// <remarks>
/// <para>Redis has no server-side ordering or filtering, so the prefix search pages over the lexicographically-ordered
/// index <c>sortKey</c>s and applies the caller's read-reach (§17.1) <strong>in memory</strong> over each candidate's
/// persisted <c>sys:</c> tags (the catalog/source-credential idiom): the index members are the small sort keys (no
/// documents), so they are ordinal-sorted, the cursor is seeked past, and each candidate's document is fetched lazily —
/// only up to what the page needs, never the whole table. An unrestricted (System) reach skips the per-row
/// materialisation entirely. The <c>sortKey</c>'s ascending ordinal order is exactly the contract's
/// <c>(subjectValue, subjectKind)</c> order (the null separator is the lowest byte and cannot appear in a
/// subjectValue/subjectKind token), and the prefix is an in-memory case-sensitive ordinal <c>StartsWith</c>.</para>
/// <para>Targets a single Redis instance (or a primary): a sighting touches the per-identity key and the shared index
/// set, which is not Redis-Cluster slot-safe. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> (or
/// <see cref="Connect(IConnectionMultiplexer, TimeProvider?)"/>).</para>
/// </remarks>
public sealed class RedisObservedIdentityStore : IObservedIdentityStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:obid:";

    // Per-identity value key: holds the ObservedIdentity document bytes verbatim, keyed by (subjectKind, subjectValue).
    private const string IdentityPrefix = Prefix + "id:";

    // Global index set: members are the identities' sortKeys, so SearchAsync can order and keyset-page every identity.
    private const string IndexKey = Prefix + "index";

    // Collision-probe index (design §16.5.4). Per-digest SET key prefix: a SET per identity digest whose members are the
    // sortKeys (the same member encoding the ordering index uses) of every grantee holding that set-equal identity, so
    // FindIdentityConflictAsync is an indexed SMEMBERS lookup, never a scan.
    private const string DigestSetPrefix = Prefix + "bydigest:";

    // Per-record current-digest string key prefix: holds the digest a (kind, value) is currently indexed under, so a
    // re-sighting that CHANGES the identity can retract the sortKey from the OLD digest's SET before adding the new one.
    private const string DigestOfPrefix = Prefix + "digestof:";

    // The null control char cannot appear in a subjectValue/subjectKind token and is the lowest byte, so it joins the
    // two key parts into a sortKey whose ascending ordinal order is exactly the contract's (subjectValue, subjectKind).
    private const char Separator = (char)0;

    // Store the identity document, add its sortKey to the ordering index, and maintain the collision-probe digest index,
    // atomically. KEYS: identity key, index zset, the digestOf string for this (kind, value). ARGV: document bytes,
    // sortKey, the new digest ('' for the empty identity), and the digest-SET key prefix. The sortKey is deterministic
    // from (kind, value), so a re-sighting re-adds the same ordering member (a no-op). For the digest index it first
    // reads the record's previously-indexed digest and, when the identity changed, SREMs the sortKey from the OLD
    // digest's SET (the required re-sighting retraction); it then SADDs the sortKey to the new digest's SET and records
    // the new digest — or, for the empty identity (no digest), simply clears the record's digestOf so it is not indexed.
    private const string StoreScript =
        """
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('ZADD', KEYS[2], 0, ARGV[2])
        local oldDigest = redis.call('GET', KEYS[3])
        if oldDigest and oldDigest ~= ARGV[3] then
            redis.call('SREM', ARGV[4] .. oldDigest, ARGV[2])
        end
        if ARGV[3] == '' then
            redis.call('DEL', KEYS[3])
        else
            redis.call('SADD', ARGV[4] .. ARGV[3], ARGV[2])
            redis.call('SET', KEYS[3], ARGV[3])
        end
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisObservedIdentityStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the store can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens an observed-identity store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisObservedIdentityStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisObservedIdentityStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates an observed-identity store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisObservedIdentityStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisObservedIdentityStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        cancellationToken.ThrowIfCancellationRequested();
        string kindToken = kind.ToToken();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // The value is the storage-key leaf (the identity key, sortKey, and digestOf string), so it reifies once here as
        // the key; the document body is serialized bytes-to-bytes from the value/label JSON values at the synchronous
        // serialize calls below. The label never becomes a managed string.
        string valueKey = (string)value;

        // Read-merge-write keyed by (kind, value): a first sighting inserts; an existing one preserves firstSeen, bumps
        // lastSeen, unions provenance, and refreshes label/identity/completeness (the shared merge every backend uses). The
        // existing document is read into a pooled lease and parsed NON-COPYING over it (the lease stays alive through the
        // synchronous merge) — no GC read array.
        using Lease<byte>? existingLease = await this.database.StringGetLeaseAsync(IdentityKey(kindToken, valueKey)).ConfigureAwait(false);
        using ParsedJsonDocument<ObservedIdentity>? existing = existingLease is { Length: > 0 }
            ? ParsedJsonDocument<ObservedIdentity>.Parse(existingLease.Memory)
            : null;

        // Serialize the document into a pooled buffer and pass it as the script value via ReadOnlyMemory (RedisValue carries
        // the exact length) — no GC document array. The buffer is returned once the script call completes.
        using PooledUtf8 doc = existing is null
            ? ObservedIdentitySerialization.SerializeNewPooled(kind, value, label, identity, complete, now, provenance)
            : ObservedIdentitySerialization.SerializeUpsertedPooled(existing.RootElement, kind, value, label, identity, complete, now, provenance);

        // Store the document, ensure its sortKey is in the ordering index, and maintain the collision-probe digest index,
        // atomically. The key and sortKey are deterministic from (kind, value), so a re-sighting overwrites the document
        // and ordering member in place; the digest index, however, must retract the OLD digest's member when a re-sighting
        // changed the identity — the script does that from the per-record digestOf string. The empty identity has no
        // digest ('' below) and is not indexed (it never collides).
        string digest = SecurityIdentityDigest.Compute(identity) ?? string.Empty;
        RedisKey[] keys = [IdentityKey(kindToken, valueKey), IndexKey, DigestOfKey(kindToken, valueKey)];
        RedisValue[] argv = [doc.Memory, SortKey(valueKey, kindToken), digest, DigestSetPrefix];
        await this.database.ScriptEvaluateAsync(StoreScript, keys, argv).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // The prefix is the in-memory StartsWith predicate (and its .Length gate), so it reifies once here (undefined
        // prefix matches all).
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

        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        // Redis has no server-side ordering for a sorted set's members by value, so the keyset scan runs over the index
        // sortKeys in memory: they are the small (value \0 kind) tuples (no documents), so order them ordinally, seek
        // strictly past the cursor, then fetch each candidate's document lazily — only up to what the page needs.
        RedisValue[] sortKeys = await this.database.SortedSetRangeByRankAsync(IndexKey).ConfigureAwait(false);
        Array.Sort(sortKeys, static (a, b) => string.CompareOrdinal((string)a!, (string)b!));
        string? cursorSortKey = hasCursor ? SortKey(cursor.SubjectValue, cursor.SubjectKind) : null;

        var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
        string? nextValue = null, nextKind = null;
        try
        {
            // A FURTHER admitted row beyond the page is the signal to emit a continuation token — the (value, kind) of
            // the last *included* identity, so the next request seeks strictly past it.
            string lastValue = string.Empty, lastKind = string.Empty;
            foreach (RedisValue member in sortKeys)
            {
                string sortKey = (string)member!;
                if (cursorSortKey is not null && string.CompareOrdinal(sortKey, cursorSortKey) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                (string subjectValue, string subjectKind) = SplitSortKey(sortKey);
                if (kindToken is not null && !string.Equals(subjectKind, kindToken, StringComparison.Ordinal))
                {
                    continue;
                }

                if (prefixStr.Length > 0 && !subjectValue.StartsWith(prefixStr, StringComparison.Ordinal))
                {
                    continue;
                }

                cancellationToken.ThrowIfCancellationRequested();

                // Read the candidate into a pooled lease (no GC read array): the reach check parses it NON-COPYING over the
                // lease; a candidate that makes the page is copied into an owned pooled document (the lease returns here).
                using Lease<byte>? lease = await this.database.StringGetLeaseAsync(IdentityKey(subjectKind, subjectValue)).ConfigureAwait(false);
                if (lease is not { Length: > 0 })
                {
                    continue;
                }

                // Reach (§17.1): a scoped caller discovers an identity only when their read-reach admits its tags — parse
                // the candidate over the lease to read its identity tags, the same per-row reach idiom the credential and
                // catalog stores use. An unrestricted (System) reach skips this entirely (no parse).
                if (readReach is not null)
                {
                    using ParsedJsonDocument<ObservedIdentity> candidate = ParsedJsonDocument<ObservedIdentity>.Parse(lease.Memory);
                    SecurityTagSet identityTags = candidate.RootElement.IdentityTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(candidate.RootElement.IdentityTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!readReach.IsSatisfiedBy(identityTags))
                    {
                        continue; // not reach-visible to this caller (non-disclosing)
                    }
                }

                if (docs.Count == pageSize)
                {
                    nextValue = lastValue;
                    nextKind = lastKind;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(lease.Span));
                lastValue = subjectValue;
                lastKind = subjectKind;
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
        cancellationToken.ThrowIfCancellationRequested();

        // The empty (unscoped) identity never collides; otherwise the digest is the SET key whose members are the
        // sortKeys of every grantee holding this set-equal identity — an indexed SMEMBERS lookup, never a scan. Runs at
        // FULL reach (not reach-filtered): a collision is unsafe regardless of who can see the conflicting party.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        // The value is the storage-key leaf used to compute this grantee's own sortKey (excluded from the probe).
        string valueKey = (string)value;
        string selfSortKey = SortKey(valueKey, kind.ToToken());
        foreach (RedisValue member in await this.database.SetMembersAsync(DigestSetKey(digest)).ConfigureAwait(false))
        {
            string sortKey = (string)member!;
            if (string.Equals(sortKey, selfSortKey, StringComparison.Ordinal))
            {
                continue; // the grantee being authored — not a conflict with itself
            }

            (string conflictValue, string conflictKindToken) = SplitSortKey(sortKey);
            using Lease<byte>? document = await this.database.StringGetLeaseAsync(IdentityKey(conflictKindToken, conflictValue)).ConfigureAwait(false);
            if (document is not { Length: > 0 })
            {
                continue; // the document was removed between the index read and the fetch — skip the stale member
            }

            // Hand back the conflicting grantee as its own JSON document (copied off the lease, which returns to the pool
            // here; the caller disposes the document); its kind/value/label live in the record — no separate POCO.
            return PersistedJson.ToPooledDocument<ObservedIdentity>(document.Span);
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    // The per-identity value key: (subjectKind, subjectValue) joined by the null separator (which cannot appear in
    // either token), so the natural (kind, value) keys the upsert.
    private static RedisKey IdentityKey(string kindToken, string value)
        => IdentityPrefix + kindToken + Separator + value;

    // The keyset sort key: subjectValue, the null separator, then subjectKind — so an ascending ordinal sort yields the
    // contract's (subjectValue, subjectKind) order, with the separator (the lowest byte) keeping a value that is a
    // prefix of another value ordered before it.
    private static string SortKey(string value, string kindToken)
        => string.Concat(value, Separator.ToString(), kindToken);

    private static (string SubjectValue, string SubjectKind) SplitSortKey(string sortKey)
    {
        int sep = sortKey.IndexOf(Separator, StringComparison.Ordinal);
        return (sortKey[..sep], sortKey[(sep + 1)..]);
    }

    // The collision-probe digest SET: its members are the sortKeys (the same member encoding the ordering index uses) of
    // every grantee whose identity hashes to this digest, so a conflict probe is one indexed SMEMBERS over this key.
    private static RedisKey DigestSetKey(string digest)
        => DigestSetPrefix + digest;

    // The per-record current-digest string: (subjectKind, subjectValue) joined by the null separator (which cannot
    // appear in either token), holding the digest this record is indexed under so a re-sighting can retract the old one.
    private static RedisKey DigestOfKey(string kindToken, string value)
        => DigestOfPrefix + kindToken + Separator + value;
}