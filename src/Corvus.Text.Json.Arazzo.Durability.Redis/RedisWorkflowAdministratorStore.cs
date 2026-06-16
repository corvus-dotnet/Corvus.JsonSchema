// <copyright file="RedisWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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

    // Create-or-replace the record under an etag check-and-set, atomically. KEYS[1]: the per-base-id hash key.
    // ARGV[1]: the caller's expected etag (or the None sentinel for "must not exist"). ARGV[2]/ARGV[3]: the new
    // document bytes and new etag. Returns 1 on success, 0 on an etag mismatch (the caller maps 0 to a conflict).
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
        RedisValue value = await this.database.HashGetAsync(RecordKey(baseWorkflowId), DocField).ConfigureAwait(false);
        return value.IsNullOrEmpty ? null : PersistedJson.ToPooledDocument<WorkflowAdministrators>((byte[])value!);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
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

        // Read the current document so the carried-forward update preserves the immutable creation audit. The Lua script
        // re-checks the etag under the write, so a racing change between this read and the write still yields a conflict.
        RedisValue existingValue = await this.database.HashGetAsync(key, DocField).ConfigureAwait(false);
        byte[] json = existingValue.IsNullOrEmpty
            ? WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag())
            : WorkflowAdministratorsSerialization.SerializeUpdated((byte[])existingValue!, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());

        WorkflowEtag newEtag = WorkflowAdministratorsSerialization.EtagOf(json);
        RedisKey[] keys = [key];
        RedisValue[] argv = [expectedEtag.IsNone ? NoneSentinel : expectedEtag.Value!, json, newEtag.Value!];

        RedisResult result = await this.database.ScriptEvaluateAsync(PutScript, keys, argv).ConfigureAwait(false);
        if ((long)result == 0)
        {
            throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
        }

        return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
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
}