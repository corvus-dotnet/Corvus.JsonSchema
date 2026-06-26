// <copyright file="NatsJetStreamAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
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
/// key. The KV listing is unordered, so the unpaged <see cref="ListAsync(AccessRequestQuery, CancellationToken)"/>
/// materialises every request and filters / orders client-side; the paged seam adds a keyset index of marker keys for
/// oldest-first paging. The etag travels inside the document (not the KV revision), so optimistic concurrency is a
/// read-compare-write driven by <see cref="AccessRequestSerialization.SerializeDecision"/>, exactly as the
/// SQLite and in-memory backends do.
/// </summary>
public sealed class NatsJetStreamAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private const string Bucket = "arazzo_access_requests";
    private const string RequestPrefix = "request.";

    // A keyset index for native oldest-first paging: one marker key per request of the form
    // "idx.{Base64Url(createdAtIso)}.{Base64Url(id)}". KV listing is unordered, so the order is materialised client-side
    // by decoding (createdAt, id) from each index key — but enumerating these is cheap (no documents), and only the page's
    // documents are then fetched. The "idx." prefix never collides with the "request." record keys.
    private const string IndexPrefix = "idx.";

    private static readonly byte[] IndexMarker = "1"u8.ToArray();

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
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, now, etag);
        await this.store.PutAsync(RequestPrefix + Enc(id), json, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Maintain the keyset index (createdAt, id). A decision never changes createdAt/id, and there is no delete, so this
        // is the only write the index needs.
        await this.store.PutAsync(IndexKey(now, id), IndexMarker, cancellationToken: cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<AccessRequestPage> ListAsync(AccessRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AccessRequestPage.DefaultPageSize;
        string? wireStatus = query.Status is { } s ? AccessRequestStatusNames.ToWire(s) : null;

        // Decode the keyset cursor; createdAt + id reify to strings (the leaf) only here — createdAt as the ISO-8601 "o"
        // form (reconstructed from the token's UTC ticks). Undefined token = first page.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AccessRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (AccessRequestContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorCreatedAt = new DateTime(cursorTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    cursorId = Encoding.UTF8.GetString(cursorIdUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // Enumerate the index keys (cheap, no documents) and recover (createdAt, id) from each; KV listing is unordered, so
        // the (createdAt, id) order is materialised client-side. Then only the page's documents are fetched + filtered.
        var keys = new List<(string CreatedAt, string Id)>();
        await foreach (string key in this.store.GetKeysAsync(cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (!key.StartsWith(IndexPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string rest = key[IndexPrefix.Length..];
            int dot = rest.IndexOf('.');
            if (dot < 0)
            {
                continue;
            }

            keys.Add((Dec(rest[..dot]), Dec(rest[(dot + 1)..])));
        }

        keys.Sort(static (a, b) =>
        {
            int byCreated = string.CompareOrdinal(a.CreatedAt, b.CreatedAt);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.Id, b.Id);
        });

        var page = new PooledDocumentList<AccessRequest>(pageSize);
        try
        {
            bool hasMore = false;
            foreach ((string createdAt, string id) in keys)
            {
                if (cursorCreatedAt is not null)
                {
                    int byCreated = string.CompareOrdinal(createdAt, cursorCreatedAt);
                    bool after = byCreated > 0 || (byCreated == 0 && string.CompareOrdinal(id, cursorId) > 0);
                    if (!after)
                    {
                        continue; // at or before the cursor — already returned in an earlier page
                    }
                }

                NatsKVEntry<byte[]>? entry = await this.TryGetAsync(RequestPrefix + Enc(id), cancellationToken).ConfigureAwait(false);
                if (entry is not { Value: { } bytes })
                {
                    continue; // indexed but record gone — skip
                }

                ParsedJsonDocument<AccessRequest> document = ParsedJsonDocument<AccessRequest>.Parse(bytes.AsMemory());
                if (!Matches(document.RootElement, wireStatus, query))
                {
                    document.Dispose();
                    continue;
                }

                if (page.Count == pageSize)
                {
                    hasMore = true; // one matching row beyond the page → a next page exists
                    document.Dispose();
                    break;
                }

                page.Add(document);
            }

            if (!hasMore)
            {
                return AccessRequestPage.Create(page);
            }

            AccessRequest last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return AccessRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
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

    // The inverse of Enc: recovers a key segment's original text. Only ever applied to segments this store wrote.
    private static string Dec(string segment) => Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segment));

    // The keyset index key for a request: "idx.{Base64Url(createdAtIso)}.{Base64Url(id)}", with createdAt as the
    // fixed-width ISO-8601 "o" UTC form. Both parts are Base64Url so they are KV-key-safe single dot-free segments; the
    // (createdAt, id) order is recovered by decoding them (KV listing is unordered, so ordering is done client-side).
    private static string IndexKey(DateTimeOffset createdAt, string id)
        => string.Concat(IndexPrefix, Enc(createdAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)), ".", Enc(id));

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