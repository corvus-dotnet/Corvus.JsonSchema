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

    // The reverse administration index (design §15.4): one marker key per (administrator digest, base id) of the form
    // "aidx.{digest}.{Base64Url(baseWorkflowId)}". KV listing is unordered, so a digest's administered base ids are
    // recovered by a server-side key filter ("aidx.{digest}.>"), decoded, and ordered client-side; the marker is
    // maintained on every PutAsync (the administrator set can change → stale digests' markers are dropped, new ones added).
    private const string IndexPrefix = "aidx.";

    // A one-byte placeholder value for a marker key — only its existence matters; the base id lives in the key itself.
    private static readonly byte[] IndexMarker = "1"u8.ToArray();

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
        return entry is { Value: { } bytes } ? ParsedJsonDocument<WorkflowAdministrators>.Parse(bytes.AsMemory()) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
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
        WorkflowEtag etag = NewEtag();
        byte[] json;

        // The base id's previous administrator digests (so stale reverse-index markers can be retracted): read from the
        // record on disk, since KV has no atomic server-side script holding them. Empty for a fresh record.
        IReadOnlyList<string> oldDigests;
        if (existing is { Value: { } existingBytes })
        {
            // A record exists: parse it ONCE, NON-COPYING over the KV entry's array (the read leaf) — used for both the etag
            // check (None means "I expected no record") and the carried-forward merge. The KV value we write keeps its byte[]
            // shape (the driver leaf); the returned document parses those same bytes non-copying.
            using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existingBytes.AsMemory());
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            oldDigests = WorkflowAdministeredPaging.DistinctDigests(current.RootElement);
            json = WorkflowAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            oldDigests = [];
            json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        await this.store.PutAsync(key, json, cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.ReindexAsync(baseWorkflowId, oldDigests, WorkflowAdministeredPaging.DistinctDigests(administrators), cancellationToken).ConfigureAwait(false);
        return ParsedJsonDocument<WorkflowAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : WorkflowAdministeredPage.DefaultPageSize;
        string? after = WorkflowAdministeredContinuationToken.DecodeCursorToString(pageToken);

        // Recover the digest's administered base ids via a server-side key filter (aidx.{digest}.>), decode each from its
        // marker key, and order them client-side (KV listing is unordered) — string.CompareOrdinal is the contract's order.
        string prefix = string.Concat(IndexPrefix, adminDigest, ".");
        var ids = new List<string>();
        await foreach (string markerKey in this.store.GetKeysAsync([prefix + ">"], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (markerKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                ids.Add(Dec(markerKey[prefix.Length..]));
            }
        }

        ids.Sort(StringComparer.Ordinal);

        var rows = new List<string>(Math.Min(pageSize + 1, ids.Count));
        foreach (string id in ids)
        {
            if (after is not null && string.CompareOrdinal(id, after) <= 0)
            {
                continue; // at or before the cursor — already returned on an earlier page
            }

            rows.Add(id);
            if (rows.Count > pageSize)
            {
                break;
            }
        }

        return WorkflowAdministeredPaging.ToPage(rows, pageSize);
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
        => string.Create(CultureInfo.InvariantCulture, $"{KeyPrefix}{Enc(baseWorkflowId)}");

    // The reverse-index marker key for (digest, base id): "aidx.{digest}.{Base64Url(baseWorkflowId)}". The digest is hex
    // (KV-key-safe, no '.'), so the single '.' before the encoded base id is an unambiguous segment boundary.
    private static string IndexKey(string adminDigest, string baseWorkflowId)
        => string.Concat(IndexPrefix, adminDigest, ".", Enc(baseWorkflowId));

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    private static string Dec(string segment) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segment));

    // Reconciles a base id's reverse-index markers (§15.4): drop the markers for digests it no longer administers, then
    // (idempotently) write a marker for each current digest. Not atomic with the document write (KV has no multi-key
    // transaction), but a stale marker only ever over-reports until the next write, and the maintenance is bounded by the
    // small administrator set.
    private async ValueTask ReindexAsync(string baseWorkflowId, IReadOnlyList<string> oldDigests, IReadOnlyList<string> newDigests, CancellationToken cancellationToken)
    {
        foreach (string digest in oldDigests)
        {
            if (!newDigests.Contains(digest))
            {
                await this.DeleteKeyAsync(IndexKey(digest, baseWorkflowId), cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (string digest in newDigests)
        {
            await this.store.PutAsync(IndexKey(digest, baseWorkflowId), IndexMarker, cancellationToken: cancellationToken).ConfigureAwait(false);
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