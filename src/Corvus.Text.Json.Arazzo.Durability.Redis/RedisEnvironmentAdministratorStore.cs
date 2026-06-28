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
/// EnvironmentName; there are no tag/reach/index sets. Mirrors <see cref="RedisWorkflowAdministratorStore"/> without the
/// reverse administration index. The record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// <para>Each environment name maps to a single Redis hash key <c>arazzo:envadmin:{environmentName}</c> with two fields:
/// the <c>doc</c> field holds the serialized document bytes verbatim, and the <c>etag</c> field holds its current etag so
/// the optimistic-concurrency check is a plain string compare. The document's own embedded etag is authoritative on read;
/// the field is a denormalized copy purely so the conditional write can be done server-side.</para>
/// <para>Redis is not transactional across a read-then-write, so <see cref="PutAsync"/> performs the create-or-replace
/// atomically through a Lua script that compares the stored etag against the caller's expected etag (and absence against
/// <see cref="WorkflowEtag.None"/>) and writes only on a match. The store touches only the per-record key — there are no
/// index sets to maintain. Targets a single Redis instance (or a primary).</para>
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

    // Create-or-replace the record under an etag check-and-set, atomically. KEYS[1]: the per-environment hash key.
    // ARGV[1]: the caller's expected etag (or the None sentinel for "must not exist"). ARGV[2]/ARGV[3]: the new document
    // bytes and new etag. Returns 1 on success, 0 on an etag mismatch (the caller maps 0 to a conflict). There is no
    // reverse index, so no index sets are touched.
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

        RedisValue[] argv =
        [
            expectedEtag.IsNone ? NoneSentinel : expectedEtag.Value!,
            json,
            etag.Value!,
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(PutScript, [key], argv).ConfigureAwait(false);
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
        await this.database.KeyDeleteAsync(RecordKey(environmentName)).ConfigureAwait(false);
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
}