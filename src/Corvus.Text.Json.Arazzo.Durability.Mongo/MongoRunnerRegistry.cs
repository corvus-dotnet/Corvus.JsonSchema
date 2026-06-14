// <copyright file="MongoRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as a document
/// in a dedicated <c>runner_registrations</c> collection, keyed by runner id, holding the canonical JSON document
/// (a BSON binary) alongside a queryable <c>lastSeenAt</c> field (Unix milliseconds) used for pruning and a
/// multikey <c>loadedVersions</c> projection (one <c>{ baseWorkflowId, versionNumber }</c> sub-document per loaded
/// hosted version) used by <see cref="IsVersionHostedAsync"/>.
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

        // Multikey index over the queryable hosting projection, supporting IsVersionHostedAsync.
        var indexKeys = Builders<BsonDocument>.IndexKeys
            .Ascending("loadedVersions.baseWorkflowId")
            .Ascending("loadedVersions.versionNumber");
        this.registrations.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(indexKeys));
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

        byte[] doc = PersistedJson.ToArray(registration, static (Utf8JsonWriter writer, in RunnerRegistration r) => r.WriteTo(writer));

        BsonDocument document = BuildDocument(registration, doc, registration.LastSeenAtValue.ToUnixTimeMilliseconds());

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", runnerId);
        await this.registrations.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);

        // $elemMatch ensures the same array element matches both base id and version.
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.ElemMatch(
            "loadedVersions",
            Builders<BsonDocument>.Filter.Eq("baseWorkflowId", baseWorkflowId) & Builders<BsonDocument>.Filter.Eq("versionNumber", versionNumber));
        return await this.registrations.Find(filter).Limit(1).AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the stored BSON document for a registration: the canonical JSON (clean) plus the queryable
    /// <c>lastSeenAt</c> field and the multikey <c>loadedVersions</c> projection used by
    /// <see cref="IsVersionHostedAsync"/>.
    /// </summary>
    private static BsonDocument BuildDocument(RunnerRegistration registration, byte[] doc, long lastSeenAtUnixMilliseconds)
    {
        var loadedVersions = new BsonArray();
        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            loadedVersions.Add(new BsonDocument
            {
                ["baseWorkflowId"] = baseWorkflowId,
                ["versionNumber"] = versionNumber,
            });
        }

        return new BsonDocument
        {
            ["_id"] = registration.RunnerIdValue,
            ["lastSeenAt"] = lastSeenAtUnixMilliseconds,
            ["doc"] = new BsonBinaryData(doc),
            ["loadedVersions"] = loadedVersions,
        };
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

        byte[] existingDoc = existing["doc"].AsBsonBinaryData.Bytes;
        RunnerRegistration current = RunnerRegistration.FromJson(existingDoc);

        byte[] doc = PersistedJson.ToArray((existingDoc, at), static (Utf8JsonWriter writer, in (byte[] Existing, DateTimeOffset At) ctx) =>
        {
            using ParsedJsonDocument<RunnerRegistration> parsed = ParsedJsonDocument<RunnerRegistration>.Parse(ctx.Existing);
            parsed.RootElement.WriteWithLastSeenAt(writer, ctx.At);
        });

        BsonDocument document = BuildDocument(current, doc, at.ToUnixTimeMilliseconds());
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