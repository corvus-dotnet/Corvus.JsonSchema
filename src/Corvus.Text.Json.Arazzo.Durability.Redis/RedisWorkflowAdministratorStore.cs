// <copyright file="RedisWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration record for a base
/// workflow id — the mutable set of administrator identities entitled to publish further versions and to manage
/// administration. Each record is stored as its <see cref="WorkflowAdministrators"/> document, keyed solely by
/// BaseWorkflowId; there are no tag/reach/index sets (it is simpler than the source-credential store, which fans a
/// binding across several index sets). The record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// <para>Each base id maps to a single Redis hash key <c>arazzo:wadmin:{baseWorkflowId}</c> with two fields: the
/// <c>doc</c> field holds the serialized document bytes verbatim, and the <c>etag</c> field holds its current etag so the
/// optimistic-concurrency check is a plain string compare. The document's own embedded etag is authoritative on read;
/// the field is a denormalized copy purely so the conditional write can be done server-side.</para>
/// <para>Redis is not transactional across a read-then-write, so <see cref="PutAsync"/> performs the create-or-replace
/// atomically through a Lua script — mirroring the source-credential store's atomic-Lua approach — that compares the
/// stored etag against the caller's expected etag (and absence against <see cref="WorkflowEtag.None"/>) and writes only
/// on a match. Targets a single Redis instance (or a primary).</para>
/// </remarks>
public sealed class RedisWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:wadmin:";

    // Hash field holding the WorkflowAdministrators document bytes verbatim.
    private const string DocField = "doc";

    // Hash field holding the document's current etag, so the conditional write is a server-side string compare.
    private const string EtagField = "etag";

    // The sentinel ARGV value standing in for WorkflowEtag.None ("no record may exist"); it cannot collide with a real
    // etag because real etags are 32-char hex GUIDs.
    private const string NoneSentinel = "";

    // The reverse administration index (design §15.4): one zero-scored sorted set per administrator digest,
    // arazzo:wadminidx:{digest}, whose members are the base ids that digest administers — a ZRANGEBYLEX is then an ordinal
    // keyset over base ids. The per-base digests set arazzo:wadmindigests:{baseWorkflowId} holds a base id's current
    // digests so a PutAsync that changes the administrator set can retract the stale ones before indexing the new ones.
    private const string IndexPrefix = "arazzo:wadminidx:";
    private const string DigestsPrefix = "arazzo:wadmindigests:";

    // Create-or-replace the record under an etag check-and-set, AND rewrite its reverse-index entries, atomically.
    // KEYS[1]: the per-base-id hash key. KEYS[2]: the per-base digests set. ARGV[1]: the caller's expected etag (or the
    // None sentinel for "must not exist"). ARGV[2]/ARGV[3]: the new document bytes and new etag. ARGV[4]: the base id (the
    // index member). ARGV[5]: the per-digest index-set key prefix. ARGV[6..]: the current administrator digests. Returns 1
    // on success, 0 on an etag mismatch (the caller maps 0 to a conflict).
    private const string PutScript =
        """
        local expected = ARGV[1]
        local exists = redis.call('EXISTS', KEYS[1])
        if expected == '' then
            if exists == 1 then return 0 end
        else
            if exists == 0 then return 0 end
            if redis.call('HGET', KEYS[1], 'etag') ~= expected then return 0 end
        end
        redis.call('HSET', KEYS[1], 'doc', ARGV[2], 'etag', ARGV[3])
        local member = ARGV[4]
        local indexPrefix = ARGV[5]
        local previous = redis.call('SMEMBERS', KEYS[2])
        for i = 1, #previous do
            redis.call('ZREM', indexPrefix .. previous[i], member)
        end
        redis.call('DEL', KEYS[2])
        for i = 6, #ARGV do
            redis.call('ZADD', indexPrefix .. ARGV[i], 0, member)
            redis.call('SADD', KEYS[2], ARGV[i])
        end
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisWorkflowAdministratorStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
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

    /// <summary>Opens a workflow administrator store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisWorkflowAdministratorStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisWorkflowAdministratorStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a workflow administrator store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisWorkflowAdministratorStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisWorkflowAdministratorStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.HashGetLeaseAsync(RecordKey(baseWorkflowId), DocField).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<WorkflowAdministrators>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        cancellationToken.ThrowIfCancellationRequested();
        RedisKey key = RecordKey(baseWorkflowId);

        // The etag for this write is generated ONCE up front; it stamps the document body and is the denormalized copy the
        // Lua check-and-set stores in the etag field, so no re-derivation from the serialized bytes is needed.
        WorkflowEtag etag = NewEtag();

        // Read the current document into a pooled lease (no GC read array) so the carried-forward update preserves the
        // immutable creation audit. The Lua script re-checks the etag under the write, so a racing change between this read
        // and the write still yields a conflict.
        using Lease<byte>? lease = await this.database.HashGetLeaseAsync(key, DocField).ConfigureAwait(false);

        // Serialize into the pooled buffer the returned document owns and bind its exact bytes as the new-document ARGV (a
        // ReadOnlyMemory<byte> carries the precise length, so there is no GC document array and no second copy); the document
        // is returned on success, disposed on a write failure.
        ParsedJsonDocument<WorkflowAdministrators> doc;
        if (lease is { Length: > 0 })
        {
            // Parse the existing record NON-COPYING over the lease (the lease stays alive through the synchronous merge).
            using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(lease.Memory);
            doc = WorkflowAdministratorsSerialization.SerializeUpdatedDoc(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            doc = WorkflowAdministratorsSerialization.SerializeNewDoc(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;

            // The reverse-index digests (the exact digests the forward IsAdministeredBy compares) are passed to the script
            // so the document write and the index rewrite are one atomic Lua call.
            IReadOnlyList<string> digests = WorkflowAdministeredPaging.DistinctDigests(administrators);
            RedisKey[] keys = [key, DigestsKey(baseWorkflowId)];
            var argv = new RedisValue[5 + digests.Count];
            argv[0] = expectedEtag.IsNone ? NoneSentinel : expectedEtag.Value!;
            argv[1] = utf8;
            argv[2] = etag.Value!;
            argv[3] = baseWorkflowId;
            argv[4] = IndexPrefix;
            for (int i = 0; i < digests.Count; i++)
            {
                argv[5 + i] = digests[i];
            }

            RedisResult result = await this.database.ScriptEvaluateAsync(PutScript, keys, argv).ConfigureAwait(false);
            if ((long)result == 0)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : WorkflowAdministeredPage.DefaultPageSize;

        // The keyset cursor (the base id to page strictly after) reifies once here, never per row. The digest's sorted-set
        // members are the administered base ids (score 0), so a ZRANGEBYLEX is an ordinal keyset over base ids — the
        // contract's order; seek strictly past the cursor and take pageSize+1 for the lookahead.
        string? after = WorkflowAdministeredContinuationToken.DecodeCursorToString(pageToken);
        RedisValue[] members = await this.database.SortedSetRangeByValueAsync(
            IndexKey(adminDigest),
            min: after is null ? default : after,
            max: default,
            exclude: after is null ? Exclude.None : Exclude.Start,
            order: Order.Ascending,
            skip: 0,
            take: pageSize + 1).ConfigureAwait(false);

        var rows = new List<string>(members.Length);
        foreach (RedisValue member in members)
        {
            rows.Add((string)member!);
        }

        return WorkflowAdministeredPaging.ToPage(rows, pageSize);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static RedisKey RecordKey(string baseWorkflowId) => Prefix + baseWorkflowId;

    private static RedisKey DigestsKey(string baseWorkflowId) => DigestsPrefix + baseWorkflowId;

    private static RedisKey IndexKey(string adminDigest) => IndexPrefix + adminDigest;
}