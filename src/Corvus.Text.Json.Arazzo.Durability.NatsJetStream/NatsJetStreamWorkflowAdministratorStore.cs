// <copyright file="NatsJetStreamWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration record for a
/// base workflow id — the mutable set of administrator identities entitled to publish further versions and to manage
/// administration. Each record is stored as its <see cref="WorkflowAdministrators"/> document in a single KV bucket under a
/// Base64Url-encoded <c>baseWorkflowId</c> key; its etag travels inside the document, independent of the KV revision. The
/// record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// <para>The KV key is <c>wadm.{base64url(baseWorkflowId)}</c>: Base64Url over the UTF-8 of the id yields only the restricted
/// set of characters a NATS KV key permits, so a base workflow id containing dots, slashes, or other characters round-trips
/// safely, mirroring the source-credential and security-policy stores' key encoding.</para>
/// <para>Optimistic concurrency is a read-compare-write over the document's own etag, mirroring the relational backends: the
/// <see cref="PutAsync"/> create-or-replace reads the current document (if any) and compares its etag before writing, so a
/// mismatch surfaces as <see cref="WorkflowAdministrationConflictException"/>.</para>
/// </remarks>
public sealed class NatsJetStreamWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_workflow_administrators";
    private const string KeyPrefix = "wadm.";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamWorkflowAdministratorStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the workflow-administrator KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the workflow-administrator KV bucket over a caller-supplied connection.</summary>
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
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowAdministratorStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamWorkflowAdministratorStore(connection, store, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens the store for operation over a caller-supplied connection (the caller retains ownership).</summary>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamWorkflowAdministratorStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamWorkflowAdministratorStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(baseWorkflowId), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? PersistedJson.ToPooledDocument<WorkflowAdministrators>(bytes) : null;
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

        string key = Key(baseWorkflowId);
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        byte[] json;
        if (existing is { Value: { } current })
        {
            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != WorkflowAdministratorsSerialization.EtagOf(current))
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeUpdated(current, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
        }

        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The KV key for a record: the namespace, then Base64Url(baseWorkflowId). Base64Url emits only [A-Za-z0-9_-], all
    // valid KV key characters, so a base workflow id containing dots, slashes, etc. round-trips safely.
    private static string Key(string baseWorkflowId)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(baseWorkflowId))}");

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
}