// <copyright file="RedisDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug (<c>$draft</c>) run's
/// latest assembled metadata trace. Each run's trace is held in a single string key
/// <c>arazzo:draftruntrace:{runId}</c> as an opaque blob — the same opaque-blob framing
/// <see cref="RedisDraftRunStore"/> uses for its capture's <c>package</c> field (there is one trace per run, so a
/// plain string key rather than a hash).
/// </summary>
/// <remarks>
/// Create instances with <see cref="ConnectAsync(string, CancellationToken)"/> (which owns and disposes the
/// connection) or <see cref="Connect(IConnectionMultiplexer)"/> (which shares a caller-owned connection).
/// </remarks>
public sealed class RedisDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:draftruntrace:";

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly bool ownsConnection;

    private RedisDraftRunTraceStore(IConnectionMultiplexer connection, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
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

    /// <summary>Opens a draft-run-trace store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisDraftRunTraceStore> ConnectAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisDraftRunTraceStore(connection, ownsConnection: true);
    }

    /// <summary>Creates a draft-run-trace store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <returns>The store.</returns>
    public static RedisDraftRunTraceStore Connect(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisDraftRunTraceStore(connection, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The trace is passed straight from its memory as the key's RedisValue (StackExchange.Redis binds
        // ReadOnlyMemory<byte> directly) — no GC copy of the trace. SET overwrites, so a re-put replaces the trace
        // (the package-blob idiom RedisDraftRunStore uses for its HSET package field).
        await this.database.StringSetAsync(TraceKey(id.Value), traceUtf8).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.StringGetAsync(TraceKey(id.Value)).ConfigureAwait(false);
        return value.IsNull ? null : (ReadOnlyMemory<byte>?)(byte[])value!;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.database.KeyDeleteAsync(TraceKey(id.Value)).ConfigureAwait(false);
    }

    /// <summary>Disposes the connection if this store created it (from a configuration string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static RedisKey TraceKey(string runId)
        => Prefix + runId;
}