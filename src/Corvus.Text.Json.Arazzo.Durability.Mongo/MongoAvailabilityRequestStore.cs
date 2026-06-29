// <copyright file="MongoAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IAvailabilityRequestStore"/> — availability ("promotion") requests (design §7.8): a
/// principal's request to make a workflow version available in an environment, its decision state, and audit metadata. Each
/// request is a document keyed by id holding its <see cref="AvailabilityRequest"/> Corvus.Text.Json schema document as a
/// binary field; the filterable fields (status, target environment, requester) and the creation timestamp are mirrored into
/// BSON fields so the list query can filter and order oldest-first server-side. The etag travels inside the document, so
/// optimistic concurrency is a read-compare-write. Mirrors <see cref="MongoAccessRequestStore"/>, parameterised by
/// environment.
/// </summary>
public sealed class MongoAvailabilityRequestStore : IAvailabilityRequestStore, IAsyncDisposable
{
    // Oldest-first (creation order): by createdAt then id, as the server-side sort.
    private static readonly SortDefinition<BsonDocument> OldestFirst =
        Builders<BsonDocument>.Sort.Ascending("createdAt").Ascending("_id");

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> requests;

    private MongoAvailabilityRequestStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.requests = database.GetCollection<BsonDocument>("availability_requests");
    }

    /// <summary>Opens the store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoAvailabilityRequestStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoAvailabilityRequestStore>(new MongoAvailabilityRequestStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoAvailabilityRequestStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoAvailabilityRequestStore>(new MongoAvailabilityRequestStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "areq-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AvailabilityRequestSerialization.SerializeNew(id, draft, actor, now, etag);
        var document = new BsonDocument
        {
            ["_id"] = id,
            ["environment"] = draft.EnvironmentValue,
            ["createdBy"] = actor,
            ["status"] = AvailabilityRequestStatusNames.Pending,
            ["createdAt"] = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            ["doc"] = new BsonBinaryData(json),
        };
        await this.requests.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AvailabilityRequest>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AvailabilityRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = BuildFilter(query);

        var list = new PooledDocumentList<AvailabilityRequest>();
        List<BsonDocument> documents = await this.requests.Find(filter).Sort(OldestFirst).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(ParsedJsonDocument<AvailabilityRequest>.Parse(document["doc"].AsBsonBinaryData.Bytes.AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityRequestPage> ListAsync(AvailabilityRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AvailabilityRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the Mongo filter needs (the leaf) only here —
        // createdAt as the ISO-8601 "o" form the createdAt field stores (reconstructed from the token's UTC ticks).
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AvailabilityRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (AvailabilityRequestContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
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

        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = BuildFilter(query);

        if (cursorCreatedAt is not null)
        {
            // Keyset seek strictly past (createdAt, id): BSON compares strings by bytes, so the ISO createdAt order is
            // chronological and _id is byte-ordinal — the same total order OldestFirst / the in-memory pager uses.
            filter &= b.Or(
                b.Gt("createdAt", cursorCreatedAt),
                b.And(b.Eq("createdAt", cursorCreatedAt), b.Gt("_id", cursorId)));
        }

        // The (createdAt, _id) sort + Limit(n+1) bounds the read to one page + 1 (lookahead) — never the whole queue.
        var page = new PooledDocumentList<AvailabilityRequest>(pageSize);
        try
        {
            List<BsonDocument> documents = await this.requests
                .Find(filter)
                .Sort(OldestFirst)
                .Limit(pageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasMore = documents.Count > pageSize;
            int take = hasMore ? pageSize : documents.Count;
            for (int i = 0; i < take; i++)
            {
                page.Add(ParsedJsonDocument<AvailabilityRequest>.Parse(documents[i]["doc"].AsBsonBinaryData.Bytes.AsMemory()));
            }

            if (!hasMore)
            {
                return AvailabilityRequestPage.Create(page);
            }

            AvailabilityRequest last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return AvailabilityRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> DecideAsync(string id, AvailabilityRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<AvailabilityRequest> current = ParsedJsonDocument<AvailabilityRequest>.Parse(doc.AsMemory());
        byte[] json = AvailabilityRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("status", AvailabilityRequestStatusNames.ToWire(decision.Status))
            .Set("doc", new BsonBinaryData(json));
        await this.requests.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), update, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AvailabilityRequest>(json);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient && this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Builds the shared list filter (status / environment / requester) plus the approver-inbox filter (the request's
    // environment must be one the caller administers — server-derived strings). The administered set is never empty here
    // (the handler short-circuits a caller who administers nothing to an empty page before the store), but a null/empty set
    // (the non-inbox modes) adds nothing.
    private static FilterDefinition<BsonDocument> BuildFilter(AvailabilityRequestQuery query)
    {
        var b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (query.Status is { } status)
        {
            filter &= b.Eq("status", AvailabilityRequestStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            filter &= b.Eq("environment", environment);
        }

        if (query.CreatedBy is { } createdBy)
        {
            filter &= b.Eq("createdBy", createdBy);
        }

        if (query.AdministeredEnvironments is { Count: > 0 } administered)
        {
            filter &= b.In("environment", administered);
        }

        return filter;
    }

    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.requests.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }
}