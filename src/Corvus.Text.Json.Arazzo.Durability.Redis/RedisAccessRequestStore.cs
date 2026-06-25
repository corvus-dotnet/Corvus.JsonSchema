// <copyright file="RedisAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5) persisted for a distributed host.
/// Each request is stored verbatim as its <see cref="AccessRequest"/> Corvus.Text.Json document under a per-record key,
/// with a single set holding every request id for enumeration. The filterable fields (status, target workflow, subject)
/// and the creation order live inside the document, so <see cref="ListAsync"/> materialises every record and filters /
/// orders client-side, just as the binding store does. The etag travels inside the document, so optimistic concurrency
/// is a read-compare-write.
/// </summary>
public sealed class RedisAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private const string RequestPrefix = "arazzo:accessreq:";
    private const string RequestIndexKey = "arazzo:accessreqs";

    // Singleton comparer (created once) for the client-side snapshot ordering, since the index set is unordered:
    // oldest-first by creation instant then id.
    private static readonly IComparer<ParsedJsonDocument<AccessRequest>> ByCreatedThenId =
        Comparer<ParsedJsonDocument<AccessRequest>>.Create(static (a, b) => a.RootElement.CreatedAtValue != b.RootElement.CreatedAtValue ? a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue) : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue));

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisAccessRequestStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
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

    /// <summary>Opens an access-request store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisAccessRequestStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisAccessRequestStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates an access-request store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisAccessRequestStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisAccessRequestStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the RedisValue (a
        // ReadOnlyMemory<byte> carries the precise length, so there is no GC document array and no second copy). The
        // document is returned on success, disposed on failure.
        ParsedJsonDocument<AccessRequest> doc = AccessRequestSerialization.SerializeNewDoc(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await this.database.StringSetAsync(RequestPrefix + id, utf8).ConfigureAwait(false);
            await this.database.SetAddAsync(RequestIndexKey, id).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        // Read into a pooled lease (no GC read array); the returned document must own its buffer, so copy the lease span
        // into an owned pooled document — the lease returns to the pool here.
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RequestPrefix + id).ConfigureAwait(false);
        return lease is { Length: > 0 } ? PersistedJson.ToPooledDocument<AccessRequest>(lease.Span) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? wireStatus = query.Status is { } status ? AccessRequestStatusNames.ToWire(status) : null;
        var list = new PooledDocumentList<AccessRequest>();
        foreach (RedisValue member in await this.database.SetMembersAsync(RequestIndexKey).ConfigureAwait(false))
        {
            // Read each candidate into a pooled lease (no GC read array); a list document must own its buffer, so copy the
            // lease span into an owned pooled document — the lease returns to the pool here.
            using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RequestPrefix + (string)member!).ConfigureAwait(false);
            if (lease is not { Length: > 0 })
            {
                continue;
            }

            ParsedJsonDocument<AccessRequest> document = PersistedJson.ToPooledDocument<AccessRequest>(lease.Span);
            if (Matches(document.RootElement, query, wireStatus))
            {
                list.Add(document);
            }
            else
            {
                document.Dispose();
            }
        }

        list.Sort(ByCreatedThenId);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();

        // Read the existing document into a pooled lease (no GC read array) and parse it NON-COPYING over the lease (the
        // lease stays alive through the synchronous etag check + merge).
        using Lease<byte>? lease = await this.database.StringGetLeaseAsync(RequestPrefix + id).ConfigureAwait(false);
        if (lease is not { Length: > 0 })
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(lease.Memory);

        // Serialize the merged result into the pooled buffer the returned document owns and bind its exact bytes (no GC
        // array, no second copy); return on success, dispose on a write failure.
        ParsedJsonDocument<AccessRequest> updated = AccessRequestSerialization.SerializeDecisionDoc(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await this.database.StringSetAsync(RequestPrefix + id, utf8).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static bool Matches(in AccessRequest request, AccessRequestQuery query, string? wireStatus)
    {
        if (wireStatus is not null && !string.Equals(request.StatusValue, wireStatus, StringComparison.Ordinal))
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
}