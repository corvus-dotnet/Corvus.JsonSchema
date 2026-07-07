// <copyright file="RedisDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. Each capture is held in a
/// single hash <c>arazzo:draftrun:{runId}</c> with a <c>record</c> field carrying the audited
/// <see cref="DraftRun"/> record verbatim as its JSON document and a <c>package</c> field carrying the packed
/// document + sources as an opaque blob — the same hash-with-doc+package framing
/// <see cref="RedisWorkflowCatalogStore"/> uses for a catalog version.
/// </summary>
/// <remarks>
/// Create instances with <see cref="ConnectAsync(string, CancellationToken)"/> (which owns and disposes the
/// connection) or <see cref="Connect(IConnectionMultiplexer)"/> (which shares a caller-owned connection).
/// </remarks>
public sealed class RedisDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:draftrun:";
    private const string RecordField = "record";
    private const string PackageField = "package";

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly bool ownsConnection;

    private RedisDraftRunStore(IConnectionMultiplexer connection, bool ownsConnection)
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

    /// <summary>Opens a draft-run store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisDraftRunStore> ConnectAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisDraftRunStore(connection, ownsConnection: true);
    }

    /// <summary>Creates a draft-run store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <returns>The store.</returns>
    public static RedisDraftRunStore Connect(IConnectionMultiplexer connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisDraftRunStore(connection, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The record is serialized verbatim; the (potentially large, ~KB) package is passed straight from its
        // memory as the field's RedisValue (StackExchange.Redis binds ReadOnlyMemory<byte> directly) — no GC copy
        // of the whole package. HSET overwrites both fields, so a re-put replaces the capture.
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
        await this.database.HashSetAsync(
            CaptureKey(id.Value),
            [
                new HashEntry(RecordField, recordUtf8),
                new HashEntry(PackageField, package),
            ]).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.HashGetAsync(CaptureKey(id.Value), RecordField).ConfigureAwait(false);
        return value.IsNull ? null : PersistedJson.ToPooledDocument<DraftRun>((byte[])value!);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RedisValue value = await this.database.HashGetAsync(CaptureKey(id.Value), PackageField).ConfigureAwait(false);
        if (value.IsNull)
        {
            return null;
        }

        byte[] bytes = (byte[])value!;
        return bytes is null ? null : (ReadOnlyMemory<byte>?)bytes;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await this.database.KeyDeleteAsync(CaptureKey(id.Value)).ConfigureAwait(false);
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

    private static RedisKey CaptureKey(string runId)
        => Prefix + runId;
}