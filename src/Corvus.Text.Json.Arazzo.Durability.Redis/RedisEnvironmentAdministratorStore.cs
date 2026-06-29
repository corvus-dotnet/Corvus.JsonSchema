// <copyright file="RedisEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for a
/// deployment environment — the mutable set of administrator identities entitled to manage the environment and its
/// administration. Each record is stored as its <see cref="EnvironmentAdministrators"/> document, keyed solely by
/// EnvironmentName. Mirrors <see cref="RedisWorkflowAdministratorStore"/>, including the reverse administration index.
/// The record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// <para>Each environment name maps to a single Redis hash key <c>arazzo:envadmin:{environmentName}</c> with two fields:
/// the <c>doc</c> field holds the serialized document bytes verbatim, and the <c>etag</c> field holds its current etag so
/// the optimistic-concurrency check is a plain string compare. The document's own embedded etag is authoritative on read;
/// the field is a denormalized copy purely so the conditional write can be done server-side.</para>
/// <para>Redis is not transactional across a read-then-write, so <see cref="PutAsync"/> performs the create-or-replace
/// atomically through a Lua script that compares the stored etag against the caller's expected etag (and absence against
/// <see cref="WorkflowEtag.None"/>) and writes only on a match. The same atomic call rewrites the reverse administration
/// index so the index and the record stay consistent. Targets a single Redis instance (or a primary).</para>
/// </remarks>
public sealed class RedisEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:envadmin:";

    // Hash field holding the EnvironmentAdministrators document bytes verbatim.
    private const string DocField = "doc";

    // Hash field holding the document's current etag, so the conditional write is a server-side string compare.
    private const string EtagField = "etag";

    // The sentinel ARGV value standing in for WorkflowEtag.None ("no record may exist"); it cannot collide with a real
    // etag because real etags are 32-char hex GUIDs.
    private const string NoneSentinel = "";

    // The reverse administration index (design §7.8): one zero-scored sorted set per administrator digest,
    // arazzo:envadminidx:{digest}, whose members are the environment names that digest administers — a ZRANGEBYLEX is then
    // an ordinal keyset over environment names. The per-environment digests set arazzo:envadmindigests:{environmentName}
    // holds an environment's current digests so a PutAsync that changes the administrator set can retract the stale ones
    // before indexing the new ones, and a DeleteAsync can retract them all. The prefixes are distinct from the workflow
    // store's so the two reverse indexes never collide.
    private const string IndexPrefix = "arazzo:envadminidx:";
    private const string DigestsPrefix = "arazzo:envadmindigests:";

    // Create-or-replace the record under an etag check-and-set, AND rewrite its reverse-index entries, atomically.
    // KEYS[1]: the per-environment hash key. KEYS[2]: the per-environment digests set. ARGV[1]: the caller's expected etag
    // (or the None sentinel for "must not exist"). ARGV[2]/ARGV[3]: the new document bytes and new etag. ARGV[4]: the
    // environment name (the index member). ARGV[5]: the per-digest index-set key prefix. ARGV[6..]: the current
    // administrator digests. Returns 1 on success, 0 on an etag mismatch (the caller maps 0 to a conflict).
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

    // Delete the record AND retract its reverse-index entries, atomically. KEYS[1]: the per-environment hash key. KEYS[2]:
    // the per-environment digests set. ARGV[1]: the environment name (the index member). ARGV[2]: the per-digest index-set
    // key prefix. A missing record is a no-op (the per-digests set is empty, so the retract loop does nothing). Always
    // returns 1 — the caller does not inspect the result; delete is unconditional.
    private const string DeleteScript =
        """
        local member = ARGV[1]
        local indexPrefix = ARGV[2]
        local previous = redis.call('SMEMBERS', KEYS[2])
        for i = 1, #previous do
            redis.call('ZREM', indexPrefix .. previous[i], member)
        end
        redis.call('DEL', KEYS[2])
        redis.call('DEL', KEYS[1])
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisEnvironmentAdministratorStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
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

    /// <summary>Opens an environment administrator store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisEnvironmentAdministratorStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisEnvironmentAdministratorStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates an environment administrator store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisEnvironmentAdministratorStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisEnvironmentAdministratorStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.HashGetLeaseAsync(RecordKey(environmentName), DocField).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<EnvironmentAdministrators>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("An environment administration record requires at least one administrator identity.", nameof(administrators));
        }

        cancellationToken.ThrowIfCancellationRequested();
        RedisKey key = RecordKey(environmentName);

        // The etag for this write is generated ONCE up front; it stamps the document body and is the denormalized copy the
        // Lua check-and-set stores in the etag field, so no re-derivation from the serialized bytes is needed.
        WorkflowEtag etag = NewEtag();

        // Read the current document into a pooled lease (no GC read array) so the carried-forward update preserves the
        // immutable creation audit. The Lua script re-checks the etag under the write, so a racing change between this read
        // and the write still yields a conflict.
        using Lease<byte>? lease = await this.database.HashGetLeaseAsync(key, DocField).ConfigureAwait(false);

        // Serialize the new-or-updated record bytes; the returned document is materialized from those exact bytes. The
        // None/mismatch/real-etag-on-missing conflict rules are checked here, mirroring the in-memory contract, and the Lua
        // script re-checks the stored etag under the write for the racing-writer case.
        byte[] json;
        if (lease is { Length: > 0 })
        {
            // A record exists: parse it NON-COPYING over the lease (the lease stays alive through the synchronous merge) for
            // both the etag check and the carried-forward merge. The caller must hold its current etag (None means "I
            // expected no record").
            using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(lease.Memory);
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag.
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        // The reverse-index digests (the exact digests the forward IsAdministeredBy compares) are passed to the script so
        // the document write and the index rewrite are one atomic Lua call.
        IReadOnlyList<string> digests = EnvironmentAdministeredPaging.DistinctDigests(administrators);
        RedisKey[] keys = [key, DigestsKey(environmentName)];
        var argv = new RedisValue[5 + digests.Count];
        argv[0] = expectedEtag.IsNone ? NoneSentinel : expectedEtag.Value!;
        argv[1] = json;
        argv[2] = etag.Value!;
        argv[3] = environmentName;
        argv[4] = IndexPrefix;
        for (int i = 0; i < digests.Count; i++)
        {
            argv[5 + i] = digests[i];
        }

        RedisResult result = await this.database.ScriptEvaluateAsync(PutScript, keys, argv).ConfigureAwait(false);
        if ((long)result == 0)
        {
            throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
        }

        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        cancellationToken.ThrowIfCancellationRequested();

        // Delete the record AND retract its reverse-index entries in one atomic Lua call so the index never outlives the
        // record. A missing record leaves the per-environment digests set empty, so the retract loop runs zero iterations
        // and the deletes are no-ops — preserving the "missing record is a no-op" contract.
        RedisKey[] keys = [RecordKey(environmentName), DigestsKey(environmentName)];
        RedisValue[] argv = [environmentName, IndexPrefix];
        await this.database.ScriptEvaluateAsync(DeleteScript, keys, argv).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;

        // The keyset cursor (the environment name to page strictly after) reifies once here, never per row. The digest's
        // sorted-set members are the administered environment names (score 0), so a ZRANGEBYLEX is an ordinal keyset over
        // environment names — the contract's order; seek strictly past the cursor and take pageSize+1 for the lookahead.
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);
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

        return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
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

    private static RedisKey RecordKey(string environmentName) => Prefix + environmentName;

    private static RedisKey DigestsKey(string environmentName) => DigestsPrefix + environmentName;

    private static RedisKey IndexKey(string adminDigest) => IndexPrefix + adminDigest;
}