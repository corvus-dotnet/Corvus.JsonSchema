// <copyright file="MongoDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.InteropServices;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug (<c>$draft</c>) run's
/// latest assembled metadata trace. Each run's trace is stored as a document in a dedicated
/// <c>draft_run_traces</c> collection keyed by run id (<c>_id</c>), holding the trace as an opaque BSON binary —
/// exactly the package-blob idiom <see cref="MongoDraftRunStore"/> persists beside its record.
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, string, CancellationToken)"/> or
/// <see cref="ConnectAsync(IMongoClient, string, CancellationToken)"/>. The collection is created lazily on first
/// write, so no provisioning step is required. The put binds the trace into the BSON binary without an
/// unconditional copy — reusing its backing array when the memory wraps a whole array, copying only for a slice —
/// matching <see cref="MongoDraftRunStore"/>'s package bind.
/// </remarks>
public sealed class MongoDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly IMongoCollection<BsonDocument> traces;

    private MongoDraftRunTraceStore(IMongoClient client, string databaseName, bool ownsClient)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.traces = database.GetCollection<BsonDocument>("draft_run_traces");
    }

    /// <summary>Opens the draft-run-trace store for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoDraftRunTraceStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoDraftRunTraceStore>(new MongoDraftRunTraceStore(client, databaseName, ownsClient: true));
    }

    /// <summary>Opens the draft-run-trace store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a least-privileged (<c>readWrite</c>)
    /// OIDC/managed-identity credential — so the store runs under a least-privileged principal.
    /// </remarks>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoDraftRunTraceStore> ConnectAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoDraftRunTraceStore>(new MongoDraftRunTraceStore(client, databaseName, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        // Bind the trace into the BSON binary without an unconditional copy: reuse its backing array when the memory
        // wraps a whole array (its common shape), falling back to a copy only for a slice — the package-blob idiom
        // MongoDraftRunStore uses. BsonBinaryData wraps the array; it is not copied again.
        byte[] traceBytes = MemoryMarshal.TryGetArray(traceUtf8, out ArraySegment<byte> segment)
            && segment.Offset == 0 && segment.Array is { } array && array.Length == segment.Count
            ? array
            : traceUtf8.ToArray();

        var document = new BsonDocument
        {
            ["_id"] = id.Value,
            ["trace"] = new BsonBinaryData(traceBytes),
        };

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        await this.traces.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        BsonDocument? document = await this.traces.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("trace"))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null ? null : (ReadOnlyMemory<byte>?)document["trace"].AsBsonBinaryData.Bytes;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", id.Value);
        DeleteResult result = await this.traces.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
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