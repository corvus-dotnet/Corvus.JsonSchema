// <copyright file="NatsJetStreamDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug
/// (<c>$draft</c>) run's latest assembled metadata trace. Each run's trace occupies a single KV key in the bucket,
/// scoped by the Base64Url-encoded run id (run ids may contain KV-illegal characters), holding the trace as an
/// opaque blob — exactly the package-blob idiom <see cref="NatsJetStreamDraftRunStore"/> persists beside its record.
/// </summary>
/// <remarks>
/// <para>
/// The trace is written straight from its memory with the raw serializer — never copied into a managed array —
/// honouring the campaign's no-copy blob-bind on a network driver, exactly as
/// <see cref="NatsJetStreamDraftRunStore"/> writes its package.
/// </para>
/// <para>
/// Create instances with <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </para>
/// </remarks>
public sealed class NatsJetStreamDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private const string TraceBucket = "arazzo_draftruntraces";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore traces;

    private NatsJetStreamDraftRunTraceStore(NatsConnection? ownedConnection, INatsKVStore traces)
    {
        this.ownedConnection = ownedConnection;
        this.traces = traces;
    }

    /// <summary>
    /// Provisions the store's key/value bucket. Creating a KV bucket creates a JetStream stream, which requires
    /// stream-management permissions, so run this once at deploy/migration time, separately from the
    /// least-privileged account used to <see cref="ConnectAsync(string, CancellationToken)"/> the store for
    /// operation (which needs only get/put/delete on the bucket's subjects).
    /// </summary>
    /// <param name="url">A NATS server URL for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await using var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(TraceBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's key/value bucket over a caller-supplied connection.</summary>
    /// <remarks>
    /// Supply a connection the caller configured (for example with a creds file, nkey, or token) so provisioning
    /// runs under a deliberate, stream-management-capable account. The caller retains ownership.
    /// </remarks>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(TraceBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation, binding to its already-provisioned key/value bucket.</summary>
    /// <remarks>
    /// This creates no streams/buckets, so it is safe to use a least-privileged account granted only get/put/delete
    /// on the bucket's subjects. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand — with a
    /// stream-management account — to create the bucket.
    /// </remarks>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamDraftRunTraceStore> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore traces = await kv.GetStoreAsync(TraceBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamDraftRunTraceStore(connection, traces);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens the store for operation over a caller-supplied connection (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a connection the caller configured — for example with a least-privileged operational account
    /// (get/put/delete on the bucket's subjects) — so the store runs under a least-privileged principal. This
    /// creates no buckets; call <see cref="PrepareAsync(INatsConnection, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamDraftRunTraceStore> ConnectAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore traces = await kv.GetStoreAsync(TraceBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamDraftRunTraceStore(ownedConnection: null, traces);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        // The trace is written straight from its memory with the raw serializer, which streams the span into the
        // publish buffer — never copied into a managed array. This is the campaign's no-copy blob-bind on a network
        // driver (NatsJetStreamDraftRunStore writes its package the same way). PUT overwrites, so a re-put replaces.
        await this.traces.PutAsync(Enc(id.Value), traceUtf8, serializer: NatsRawSerializer<ReadOnlyMemory<byte>>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Enc(id.Value), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } value } ? (ReadOnlyMemory<byte>?)value : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        string key = Enc(id.Value);

        // Report whether the trace was present, then remove it (a missing key is a no-op).
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        bool existed = existing is { Value: not null };
        await this.DeleteKeyAsync(key, cancellationToken).ConfigureAwait(false);
        return existed;
    }

    /// <summary>Disposes the connection if this store created it (from a URL).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Base64Url of the UTF-8 bytes: KV keys forbid '+' and '/', so the url-safe alphabet keeps the run id a single
    // valid KV key token.
    private static string Enc(string value)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await this.traces.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async ValueTask DeleteKeyAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await this.traces.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }
}