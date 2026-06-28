// <copyright file="NatsJetStreamEnvironmentAdministratorStore.cs" company="Endjin Limited">
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
/// A NATS JetStream-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for a
/// deployment environment — the mutable set of administrator identities entitled to manage that environment and its
/// administration. Each record is stored as its <see cref="EnvironmentAdministrators"/> document in a single KV bucket under a
/// Base64Url-encoded <c>environmentName</c> key; its etag travels inside the document, independent of the KV revision. The
/// record holds deployment-stamped identities only — never secret material. Mirrors
/// <see cref="NatsJetStreamWorkflowAdministratorStore"/> without the reverse administration index.
/// </summary>
/// <remarks>
/// <para>The KV key is <c>env-admin.{base64url(environmentName)}</c>: Base64Url over the UTF-8 of the name yields only the
/// restricted set of characters a NATS KV key permits, so an environment name containing dots, slashes, or other characters
/// round-trips safely, mirroring the source-credential and security-policy stores' key encoding. The environment name is
/// required (non-empty), so its Base64Url encoding is always a non-empty token — a single-token key is therefore valid.</para>
/// <para>Optimistic concurrency is a read-compare-write over the document's own etag, mirroring the relational backends: the
/// <see cref="PutAsync"/> create-or-replace reads the current document (if any) and compares its etag before writing, so a
/// mismatch surfaces as <see cref="EnvironmentAdministrationConflictException"/>.</para>
/// </remarks>
public sealed class NatsJetStreamEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_environment_administrators";
    private const string KeyPrefix = "env-admin.";

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamEnvironmentAdministratorStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the environment-administrator KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the environment-administrator KV bucket over a caller-supplied connection.</summary>
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
    public static async ValueTask<NatsJetStreamEnvironmentAdministratorStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamEnvironmentAdministratorStore(connection, store, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamEnvironmentAdministratorStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamEnvironmentAdministratorStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(Key(environmentName), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? ParsedJsonDocument<EnvironmentAdministrators>.Parse(bytes.AsMemory()) : null;
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

        string key = Key(environmentName);
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();
        byte[] json;

        if (existing is { Value: { } existingBytes })
        {
            // A record exists: parse it ONCE, NON-COPYING over the KV entry's array (the read leaf) — used for both the etag
            // check (None means "I expected no record") and the carried-forward merge. The KV value we write keeps its byte[]
            // shape (the driver leaf).
            using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existingBytes.AsMemory());
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await this.DeleteKeyAsync(Key(environmentName), cancellationToken).ConfigureAwait(false);
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

    // The KV key for a record: the namespace, then Base64Url(environmentName). Base64Url emits only [A-Za-z0-9_-], all
    // valid KV key characters, so an environment name containing dots, slashes, etc. round-trips safely. The name is
    // required (non-empty), so its encoding is always a non-empty token — KV keys cannot have an empty token.
    private static string Key(string environmentName)
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(environmentName)}");

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private async ValueTask DeleteKeyAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await this.store.DeleteAsync(key, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (NatsKVKeyNotFoundException)
        {
        }
        catch (NatsKVKeyDeletedException)
        {
        }
    }

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