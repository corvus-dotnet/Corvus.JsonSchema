// <copyright file="MongoDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. Each capture is stored as a
/// document in a dedicated <c>draft_run_captures</c> collection keyed by run id (<c>_id</c>), holding the audited
/// <see cref="DraftRun"/> record verbatim as its JSON document (a BSON binary, the runner registry's
/// registration-document idiom) alongside the packed document + sources as an opaque BSON binary (the catalog
/// store's package idiom).
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, string, CancellationToken)"/> or
/// <see cref="ConnectAsync(IMongoClient, string, CancellationToken)"/>. The collection is created lazily on first
/// write, so no provisioning step is required.
/// </remarks>
public sealed class MongoDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly IMongoCollection<BsonDocument> captures;

    private MongoDraftRunStore(IMongoClient client, string databaseName, bool ownsClient)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.captures = database.GetCollection<BsonDocument>("draft_run_captures");
    }

    /// <summary>Opens the draft-run store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoDraftRunStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoDraftRunStore>(new MongoDraftRunStore(client, databaseName, ownsClient: true));
    }

    /// <summary>Opens the draft-run store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a least-privileged (<c>readWrite</c>)
    /// OIDC/managed-identity credential — so the store runs under a least-privileged principal.
    /// </remarks>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoDraftRunStore> ConnectAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoDraftRunStore>(new MongoDraftRunStore(client, databaseName, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));

        // Bind the (potentially large, ~KB) package into the BSON binary without an unconditional copy: reuse its
        // backing array when the memory wraps a whole array (its common shape), falling back to a copy only for a
        // slice — the catalog store's package-blob idiom. BsonBinaryData wraps the array; it is not copied again.
        byte[] packageBytes = MemoryMarshal.TryGetArray(package, out ArraySegment<byte> segment)
            && segment.Offset == 0 && segment.Array is { } array && array.Length == segment.Count
            ? array
            : package.ToArray();

        var document = new BsonDocument
        {
            ["_id"] = id.Value,
            ["record"] = new BsonBinaryData(recordUtf8),
            ["package"] = new BsonBinaryData(packageBytes),
        };

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        await this.captures.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        BsonDocument? document = await this.captures.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("record"))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : PersistedJson.ToPooledDocument<DraftRun>(document["record"].AsBsonBinaryData.Bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        BsonDocument? document = await this.captures.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("package"))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : (ReadOnlyMemory<byte>?)document["package"].AsBsonBinaryData.Bytes;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        DeleteResult result = await this.captures.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <summary>Disposes the client if this store created it (from a connection string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient && this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }
}