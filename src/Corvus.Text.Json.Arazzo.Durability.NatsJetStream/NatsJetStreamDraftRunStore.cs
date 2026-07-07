// <copyright file="NatsJetStreamDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. Each capture
/// occupies two KV keys in one bucket, both scoped by the Base64Url-encoded run id (run ids may contain KV-illegal
/// characters): a <c>record.{enc}</c> key holding the audited <see cref="DraftRun"/> record verbatim as its JSON
/// document (the runner registry's per-key doc idiom) and a <c>package.{enc}</c> key holding the packed document +
/// sources as an opaque blob.
/// </summary>
/// <remarks>
/// <para>
/// The catalog store envelopes its small metadata header and its package into a single KV value (it scans headers
/// for listing); the draft-run capture has no listing and reads record and package independently, so they are held
/// under separate keys. That split lets the (potentially large) package be written straight from its memory with
/// the raw serializer — never copied into a combined envelope buffer, honouring the campaign's package-bind bar.
/// </para>
/// <para>
/// Create instances with <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </para>
/// </remarks>
public sealed class NatsJetStreamDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private const string CaptureBucket = "arazzo_draftruns";
    private const string RecordPrefix = "record.";
    private const string PackagePrefix = "package.";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore captures;

    private NatsJetStreamDraftRunStore(NatsConnection? ownedConnection, INatsKVStore captures)
    {
        this.ownedConnection = ownedConnection;
        this.captures = captures;
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
        await kv.CreateStoreAsync(new NatsKVConfig(CaptureBucket), cancellationToken).ConfigureAwait(false);
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
        await kv.CreateStoreAsync(new NatsKVConfig(CaptureBucket), cancellationToken).ConfigureAwait(false);
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
    public static async ValueTask<NatsJetStreamDraftRunStore> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore captures = await kv.GetStoreAsync(CaptureBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamDraftRunStore(connection, captures);
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
    public static async ValueTask<NatsJetStreamDraftRunStore> ConnectAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore captures = await kv.GetStoreAsync(CaptureBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamDraftRunStore(ownedConnection: null, captures);
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        string enc = Enc(id.Value);
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));

        // The record is a bare byte[] value (the runner registry's per-key doc idiom). The (potentially large, ~KB)
        // package is written straight from its memory with the raw serializer, which streams the span into the
        // publish buffer — never copied into a managed array (unlike the catalog's header+package envelope, which
        // must concatenate). This is the campaign's no-copy package-bind on a network driver.
        await this.captures.PutAsync(RecordPrefix + enc, recordUtf8, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.captures.PutAsync(PackagePrefix + enc, package, serializer: NatsRawSerializer<ReadOnlyMemory<byte>>.Default, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RecordPrefix + Enc(id.Value), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } value } ? PersistedJson.ToPooledDocument<DraftRun>(value) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(PackagePrefix + Enc(id.Value), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } value } ? (ReadOnlyMemory<byte>?)value : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        string enc = Enc(id.Value);

        // The record key is the authority for the capture's existence; report whether it was present, then remove
        // both keys (a missing key is a no-op, so a partial capture is cleaned up too).
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(RecordPrefix + enc, cancellationToken).ConfigureAwait(false);
        bool existed = existing is { Value: not null };
        await this.DeleteKeyAsync(RecordPrefix + enc, cancellationToken).ConfigureAwait(false);
        await this.DeleteKeyAsync(PackagePrefix + enc, cancellationToken).ConfigureAwait(false);
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

    // Base64Url of the UTF-8 bytes: KV keys forbid '+' and '/', so the url-safe alphabet keeps the run-id segment a
    // single dot-free token (periods are reserved here as the key's segment separator).
    private static string Enc(string value)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await this.captures.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
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
            await this.captures.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }
}