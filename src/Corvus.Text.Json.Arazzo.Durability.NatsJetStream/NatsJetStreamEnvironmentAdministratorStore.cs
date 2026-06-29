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
/// <see cref="NatsJetStreamWorkflowAdministratorStore"/>, including its reverse administration index.
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

    // The reverse administration index (mirrors the workflow store): one marker key per (administrator digest, environment
    // name) of the form "eaidx.{digest}.{Base64Url(environmentName)}". KV listing is unordered, so a digest's administered
    // environment names are recovered by a server-side key filter ("eaidx.{digest}.>"), decoded, and ordered client-side; the
    // marker is maintained on every PutAsync (the administrator set can change → stale digests' markers are dropped, new ones
    // added) and retracted on DeleteAsync (every marker for the deleted environment is removed).
    private const string IndexPrefix = "eaidx.";

    // A one-byte placeholder value for a marker key — only its existence matters; the environment name lives in the key itself.
    private static readonly byte[] IndexMarker = "1"u8.ToArray();

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

        // The environment's previous administrator digests (so stale reverse-index markers can be retracted): read from the
        // record on disk, since KV has no atomic server-side script holding them. Empty for a fresh record.
        IReadOnlyList<string> oldDigests;
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

            oldDigests = EnvironmentAdministeredPaging.DistinctDigests(current.RootElement);
            json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            oldDigests = [];
            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.ReindexAsync(environmentName, oldDigests, EnvironmentAdministeredPaging.DistinctDigests(administrators), cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        string key = Key(environmentName);

        // KV DeleteAsync is idempotent (it never reports an absent key), so "did the record exist?" — and the administrator
        // digests whose reverse-index markers must be retracted — can only come from a read taken before the delete. A
        // missing record stays a no-op: there is nothing to delete and no markers to retract.
        NatsKVEntry<byte[]>? existing = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
        if (existing is not { Value: { } existingBytes })
        {
            return;
        }

        IReadOnlyList<string> digests;
        using (ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existingBytes.AsMemory()))
        {
            digests = EnvironmentAdministeredPaging.DistinctDigests(current.RootElement);
        }

        // Retract this environment's reverse-index markers (so a deleted environment never lingers in any digest's
        // administered list), then delete the record itself. Not atomic with the document delete (KV has no multi-key
        // transaction), but a stale marker only ever over-reports until the next write.
        await this.ReindexAsync(environmentName, digests, [], cancellationToken).ConfigureAwait(false);
        await this.DeleteKeyAsync(key, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        // Recover the digest's administered environment names via a server-side key filter (eaidx.{digest}.>), decode each
        // from its marker key, and order them client-side (KV listing is unordered) — string.CompareOrdinal is the contract's
        // order.
        string prefix = string.Concat(IndexPrefix, adminDigest, ".");
        var names = new List<string>();
        await foreach (string markerKey in this.store.GetKeysAsync([prefix + ">"], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (markerKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                names.Add(Dec(markerKey[prefix.Length..]));
            }
        }

        names.Sort(StringComparer.Ordinal);

        var rows = new List<string>(Math.Min(pageSize + 1, names.Count));
        foreach (string name in names)
        {
            if (after is not null && string.CompareOrdinal(name, after) <= 0)
            {
                continue; // at or before the cursor — already returned on an earlier page
            }

            rows.Add(name);
            if (rows.Count > pageSize)
            {
                break;
            }
        }

        return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
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

    // The reverse-index marker key for (digest, environment name): "eaidx.{digest}.{Base64Url(environmentName)}". The digest
    // is hex (KV-key-safe, no '.'), so the single '.' before the encoded name is an unambiguous segment boundary. Base64Url of
    // a non-empty UTF-8 name is always a non-empty token, so the key never carries an empty token (which KV forbids).
    private static string IndexKey(string adminDigest, string environmentName)
        => string.Concat(IndexPrefix, adminDigest, ".", Enc(environmentName));

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string segment) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segment));

    // Reconciles an environment's reverse-index markers: drop the markers for digests it no longer administers, then
    // (idempotently) write a marker for each current digest. Not atomic with the document write (KV has no multi-key
    // transaction), but a stale marker only ever over-reports until the next write, and the maintenance is bounded by the
    // small administrator set. On delete, called with an empty current-digest set so every marker is retracted.
    private async ValueTask ReindexAsync(string environmentName, IReadOnlyList<string> oldDigests, IReadOnlyList<string> newDigests, CancellationToken cancellationToken)
    {
        foreach (string digest in oldDigests)
        {
            if (!newDigests.Contains(digest))
            {
                await this.DeleteKeyAsync(IndexKey(digest, environmentName), cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (string digest in newDigests)
        {
            await this.store.PutAsync(IndexKey(digest, environmentName), IndexMarker, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

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