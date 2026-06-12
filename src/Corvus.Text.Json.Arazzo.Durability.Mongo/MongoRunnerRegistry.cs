// <copyright file="MongoRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as a document
/// in a dedicated <c>runner_registrations</c> collection, keyed by runner id, holding the canonical JSON document
/// (a BSON binary) alongside a queryable <c>lastSeenAt</c> field (Unix milliseconds) used for pruning.
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the registry is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, string, CancellationToken)"/> or
/// <see cref="ConnectAsync(IMongoClient, string, CancellationToken)"/>. The collection is created lazily on first
/// write, so no provisioning step is required.
/// </remarks>
public sealed class MongoRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly IMongoCollection<BsonDocument> registrations;

    private MongoRunnerRegistry(IMongoClient client, string databaseName, bool ownsClient)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.registrations = database.GetCollection<BsonDocument>("runner_registrations");
    }

    /// <summary>Opens the registry for operation against a database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the client).</returns>
    public static ValueTask<MongoRunnerRegistry> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoRunnerRegistry>(new MongoRunnerRegistry(client, databaseName, ownsClient: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a least-privileged (<c>readWrite</c>)
    /// OIDC/managed-identity credential — so the registry runs under a least-privileged principal.
    /// </remarks>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoRunnerRegistry> ConnectAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoRunnerRegistry>(new MongoRunnerRegistry(client, databaseName, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        var document = new BsonDocument
        {
            ["_id"] = runnerId,
            ["lastSeenAt"] = registration.LastSeenAtValue.ToUnixTimeMilliseconds(),
            ["doc"] = new BsonBinaryData(registration.ToJsonBytes()),
        };

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", runnerId);
        await this.registrations.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", runnerId);
        BsonDocument? existing = await this.registrations.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        RunnerRegistration updated = RunnerRegistration.FromJson(existing["doc"].AsBsonBinaryData.Bytes).WithLastSeenAt(at);
        var document = new BsonDocument
        {
            ["_id"] = runnerId,
            ["lastSeenAt"] = at.ToUnixTimeMilliseconds(),
            ["doc"] = new BsonBinaryData(updated.ToJsonBytes()),
        };
        await this.registrations.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        List<BsonDocument> documents = await this.registrations.Find(Builders<BsonDocument>.Filter.Empty).ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<RunnerRegistration>(documents.Count);
        foreach (BsonDocument document in documents)
        {
            result.Add(RunnerRegistration.FromJson(document["doc"].AsBsonBinaryData.Bytes));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Lt("lastSeenAt", deadBefore.ToUnixTimeMilliseconds());
        DeleteResult result = await this.registrations.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
        return (int)result.DeletedCount;
    }

    /// <summary>Disposes the client if this registry created it (from a connection string).</summary>
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