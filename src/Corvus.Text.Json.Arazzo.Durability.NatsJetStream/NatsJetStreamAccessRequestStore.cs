// <copyright file="NatsJetStreamAccessRequestStore.cs" company="Endjin Limited">
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
/// A NATS JetStream-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5) persisted in a single KV
/// bucket. Each request is stored as its <see cref="AccessRequest"/> schema document under a namespaced, Base64Url-encoded
/// key. The KV listing is unordered and unindexed, so <see cref="ListAsync"/> materialises every request and filters /
/// orders client-side. The etag travels inside the document (not the KV revision), so optimistic concurrency is a
/// read-compare-write driven by <see cref="AccessRequestSerialization.SerializeDecision"/>, exactly as the
/// SQLite and in-memory backends do.
/// </summary>
public sealed class NatsJetStreamAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_access_requests";
    private const string RequestPrefix = "request.";

    // The KV key listing is unordered; the contract is oldest-first by creation time, with the id as a stable tiebreak.
    private static readonly IComparer<ParsedJsonDocument<AccessRequest>> ByCreatedAtThenId =
        Comparer<ParsedJsonDocument<AccessRequest>>.Create(static (a, b) =>
        {
            int byCreated = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue);
        });

    private readonly NatsConnection? ownedConnection;
    private readonly INatsKVStore store;
    private readonly TimeProvider timeProvider;

    private NatsJetStreamAccessRequestStore(NatsConnection? ownedConnection, INatsKVStore store, TimeProvider timeProvider)
    {
        this.ownedConnection = ownedConnection;
        this.store = store;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the access-request KV bucket (requires stream-management rights); run once at deploy time.</summary>
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

    /// <summary>Provisions the access-request KV bucket over a caller-supplied connection.</summary>
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
    public static async ValueTask<NatsJetStreamAccessRequestStore> ConnectAsync(string url, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        var connection = new NatsConnection(NatsOpts.Default with { Url = url });
        try
        {
            var kv = new NatsKVContext(new NatsJSContext(connection));
            INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
            return new NatsJetStreamAccessRequestStore(connection, store, timeProvider ?? TimeProvider.System);
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
    public static async ValueTask<NatsJetStreamAccessRequestStore> ConnectAsync(INatsConnection connection, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var kv = new NatsKVContext(new NatsJSContext(connection));
        INatsKVStore store = await kv.GetStoreAsync(Bucket, cancellationToken).ConfigureAwait(false);
        return new NatsJetStreamAccessRequestStore(ownedConnection: null, store, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(RequestPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RequestPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        return entry is { Value: { } bytes } ? ParsedJsonDocument<AccessRequest>.Parse(bytes.AsMemory()) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        string? status = query.Status is { } s ? AccessRequestStatusNames.ToWire(s) : null;
        var list = new PooledDocumentList<AccessRequest>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(RequestPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            NatsKVEntry<byte[]>? entry = await this.TryGetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry is not { Value: { } bytes })
            {
                continue;
            }

            ParsedJsonDocument<AccessRequest> document = ParsedJsonDocument<AccessRequest>.Parse(bytes.AsMemory());
            if (Matches(document.RootElement, status, query))
            {
                list.Add(document);
            }
            else
            {
                document.Dispose();
            }
        }

        list.Sort(ByCreatedAtThenId);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RequestPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
        if (entry is not { Value: { } bytes })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(bytes.AsMemory());
        byte[] json = AccessRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        await this.store.PutAsync(RequestPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
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

    private static string Enc(string value) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    // The list filter: each absent criterion matches anything, mirroring the SQLite WHERE clause.
    private static bool Matches(AccessRequest request, string? status, AccessRequestQuery query)
    {
        if (status is not null && !string.Equals(request.StatusValue, status, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.BaseWorkflowId.IsNotUndefined())
        {
            using UnescapedUtf8JsonString filterBaseWorkflowId = query.BaseWorkflowId.GetUtf8String();
            using UnescapedUtf8JsonString rowBaseWorkflowId = request.BaseWorkflowId.GetUtf8String();
            if (!rowBaseWorkflowId.Span.SequenceEqual(filterBaseWorkflowId.Span))
            {
                return false;
            }
        }

        if (query.SubjectClaimType is { } subjectType && !string.Equals(request.SubjectClaimTypeValue, subjectType, StringComparison.Ordinal))
        {
            return false;
        }

        if (query.SubjectClaimValue is { } subjectValue && !string.Equals(request.SubjectClaimValueValue, subjectValue, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
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