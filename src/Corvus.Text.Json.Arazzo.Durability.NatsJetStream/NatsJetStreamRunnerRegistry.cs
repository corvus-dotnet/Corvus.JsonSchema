// <copyright file="NatsJetStreamRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

/// <summary>
/// A NATS JetStream key/value-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored
/// as its JSON document under a single KV key per runner (the Base64Url-encoded runner id, since runner ids may
/// contain KV-illegal characters), exactly as the catalog store
/// (<see cref="NatsJetStreamWorkflowCatalogStore"/>) encodes its keys.
/// </summary>
/// <remarks>
/// List and prune scan the bucket's keys and decode each registration, mirroring the catalog store's
/// scan approach. Create instances with <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning
/// with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class NatsJetStreamRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string RegistryBucket = "arazzo_runners";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore registry;

    private NatsJetStreamRunnerRegistry(NatsConnection? ownedConnection, INatsKVStore registry)
    {
        this.ownedConnection = ownedConnection;
        this.registry = registry;
    }

    /// <summary>
    /// Provisions the registry's key/value bucket. Creating a KV bucket creates a JetStream stream, which
    /// requires stream-management permissions, so run this once at deploy/migration time, separately from the
    /// least-privileged account used to <see cref="ConnectAsync(string, CancellationToken)"/> the registry for
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
        await kv.CreateStoreAsync(new NatsKVConfig(RegistryBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the registry's key/value bucket over a caller-supplied connection.</summary>
    /// <remarks>
    /// Supply a connection the caller configured (for example with a creds file, nkey, or token) so
    /// provisioning runs under a deliberate, stream-management-capable account. The caller retains ownership.
    /// </remarks>
    /// <param name="connection">A NATS connection for an account permitted to manage streams.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the bucket exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await kv.CreateStoreAsync(new NatsKVConfig(RegistryBucket), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation, binding to its already-provisioned key/value bucket.</summary>
    /// <remarks>
    /// This creates no streams/buckets, so it is safe to use a least-privileged account granted only
    /// get/put/delete on the bucket's subjects. Call <see cref="PrepareAsync(string, CancellationToken)"/> once
    /// beforehand — with a stream-management account — to create the bucket.
    /// </remarks>
    /// <param name="url">A NATS server URL (e.g. <c>nats://localhost:4222</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the connection).</returns>
    public static async ValueTask<NatsJetStreamRunnerRegistry> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore registry = await kv.GetStoreAsync(RegistryBucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamRunnerRegistry(connection, registry);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens the registry for operation over a caller-supplied connection (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a connection the caller configured — for example with a least-privileged operational account
    /// (get/put/delete on the bucket's subjects) — so the registry runs under a least-privileged principal. This
    /// creates no buckets; call <see cref="PrepareAsync(INatsConnection, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="connection">A NATS connection.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied connection).</returns>
    public static async ValueTask<NatsJetStreamRunnerRegistry> ConnectAsync(INatsConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore registry = await kv.GetStoreAsync(RegistryBucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamRunnerRegistry(ownedConnection: null, registry);
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        await this.registry.PutAsync(Key(registration.RunnerIdValue), registration.ToJsonBytes(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        string key = Key(runnerId);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } value })
        {
            return false;
        }

        RunnerRegistration updated = RunnerRegistration.FromJson(value).WithLastSeenAt(at);
        await this.registry.PutAsync(key, updated.ToJsonBytes(), cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<RunnerRegistration>();
        await foreach (string key in this.registry.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } value })
            {
                result.Add(RunnerRegistration.FromJson(value));
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        int removed = 0;
        await foreach (string key in this.registry.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is { Value: { } value } && RunnerRegistration.FromJson(value).LastSeenAtValue < deadBefore)
            {
                await this.DeleteAsync(key, cancellationToken).ConfigureAwait(false);
                removed++;
            }
        }

        return removed;
    }

    /// <summary>Disposes the connection if this registry created it (from a URL).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedConnection is not null)
        {
            await this.ownedConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string Key(string runnerId)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(runnerId));

    private async ValueTask<NatsKVEntry<byte[]>?> TryGetAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            return await this.registry.GetEntryAsync<byte[]>(key, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private async ValueTask DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await this.registry.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }
}