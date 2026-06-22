// <copyright file="MongoAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5): a principal's request for
/// elevated capability on a workflow, its decision state, and audit metadata. Each request is a document keyed by id
/// holding its <see cref="AccessRequest"/> Corvus.Text.Json schema document as a binary field; the filterable fields
/// (status, target workflow, subject) and the creation timestamp are mirrored into BSON fields so the list query can
/// filter and order oldest-first server-side. The etag travels inside the document, so optimistic concurrency is a
/// read-compare-write.
/// </summary>
public sealed class MongoAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    // Oldest-first (creation order): by createdAt then id, as the server-side sort.
    private static readonly SortDefinition<BsonDocument> OldestFirst =
        Builders<BsonDocument>.Sort.Ascending("createdAt").Ascending("_id");

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> requests;

    private MongoAccessRequestStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.requests = database.GetCollection<BsonDocument>("access_requests");
    }

    /// <summary>Opens the store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoAccessRequestStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoAccessRequestStore>(new MongoAccessRequestStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoAccessRequestStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoAccessRequestStore>(new MongoAccessRequestStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, now, etag);
        var document = new BsonDocument
        {
            ["_id"] = id,
            ["baseWorkflowId"] = draft.BaseWorkflowIdValue,
            ["subjectClaimType"] = draft.SubjectClaimTypeValue,
            ["subjectClaimValue"] = draft.SubjectClaimValueValue,
            ["status"] = AccessRequestStatusNames.Pending,
            ["createdAt"] = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            ["doc"] = new BsonBinaryData(json),
        };
        await this.requests.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Empty;
        if (query.Status is { } status)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("status", AccessRequestStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("baseWorkflowId", baseWorkflowId);
        }

        if (query.SubjectClaimType is { } subjectType)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("subjectClaimType", subjectType);
        }

        if (query.SubjectClaimValue is { } subjectValue)
        {
            filter &= Builders<BsonDocument>.Filter.Eq("subjectClaimValue", subjectValue);
        }

        var list = new PooledDocumentList<AccessRequest>();
        List<BsonDocument> documents = await this.requests.Find(filter).Sort(OldestFirst).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            list.Add(ParsedJsonDocument<AccessRequest>.Parse(document["doc"].AsBsonBinaryData.Bytes.AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
        byte[] json = AccessRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("status", AccessRequestStatusNames.ToWire(decision.Status))
            .Set("doc", new BsonBinaryData(json));
        await this.requests.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), update, options: null, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
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

    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.requests.Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }
}