// <copyright file="MongoAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow versions are
/// available in which environments) persisted as documents. Each entry is stored as its <see cref="AvailabilityEntry"/>
/// document in a binary <c>doc</c> field, keyed by a deterministic composite <c>_id</c>
/// (<c>baseWorkflowId + " " + versionNumber + " " + environment</c>) for point operations and an idempotent insert; the
/// three key parts are mirrored as queryable/sortable scalar fields so the two list axes are indexed range scans.
/// AvailabilityEntry carries no security tags and has no mutable state — an entry is created (idempotently) to make a
/// version available and deleted to withdraw it; authorization and readiness are the control-plane surface's concern.
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the store is naturally concurrent. The by-version axis orders by
/// <c>environment</c>; the by-environment axis orders by (<c>baseWorkflowId</c>, <c>versionNumber</c>) — each a keyset
/// range scan over the mirrored scalar fields, paged with the opaque <see cref="AvailabilityContinuationToken"/>.
/// </remarks>
public sealed class MongoAvailabilityStore : IAvailabilityStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> availability;

    private MongoAvailabilityStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.availability = database.GetCollection<BsonDocument>("availability");
    }

    /// <summary>Provisions the store's indexes over a connection string.</summary>
    /// <remarks>
    /// Creating indexes requires the <c>createIndex</c> privilege, so run this once at deploy/migration time, separately
    /// from the least-privileged user used to <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/>
    /// the store for operation. (The collection itself is created lazily on first write, and the composite <c>_id</c>
    /// already enforces uniqueness, so the operational user needs only <c>readWrite</c>.)
    /// </remarks>
    /// <param name="connectionString">A MongoDB connection string for a user permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var client = new MongoClient(connectionString);
        await using var store = new MongoAvailabilityStore(client, databaseName, ownsClient: true, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's indexes over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(IMongoClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await using var store = new MongoAvailabilityStore(client, databaseName, ownsClient: false, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoAvailabilityStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoAvailabilityStore>(new MongoAvailabilityStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoAvailabilityStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoAvailabilityStore>(new MongoAvailabilityStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);

        // Idempotent: if the version is already available in the environment, return the existing entry unchanged.
        byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>(existing), false);
        }

        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
        byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var document = new BsonDocument
        {
            ["_id"] = Key(baseWorkflowId, versionNumber, environment),
            ["baseWorkflowId"] = baseWorkflowId,
            ["versionNumber"] = versionNumber,
            ["environment"] = environment,
            ["doc"] = new BsonBinaryData(json),
        };
        try
        {
            await this.availability.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent make-available won the race; the version is now available, so honour idempotency.
            byte[]? raced = await this.ReadDocumentAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
            if (raced is not null)
            {
                return (PersistedJson.ToPooledDocument<AvailabilityEntry>(raced), false);
            }

            throw;
        }

        return (PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        byte[]? json = await this.ReadDocumentAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<AvailabilityEntry>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        DeleteResult result = await this.availability
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", Key(baseWorkflowId, versionNumber, environment)), cancellationToken)
            .ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Rows for this exact version, ordered by environment (the only varying key part on this axis). The keyset
        // predicate seeks strictly past the cursor's environment; reading one extra row past the page detects "more".
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.And(b.Eq("baseWorkflowId", baseWorkflowId), b.Eq("versionNumber", versionNumber));
        if (hasCursor)
        {
            filter = b.And(filter, b.Gt("environment", cursor.Environment));
        }

        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("environment");

        var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
        try
        {
            List<BsonDocument> rows = await this.availability.Find(filter).Sort(sort).Limit(pageSize + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
            bool hasMore = false;
            string lastEnvironment = string.Empty;
            foreach (BsonDocument document in rows)
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                lastEnvironment = document["environment"].AsString;
                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(document["doc"].AsBsonBinaryData.Bytes));
            }

            return hasMore
                ? AvailabilityPage.Create(docs, baseWorkflowId, versionNumber, lastEnvironment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Rows for this environment, ordered by base workflow id then numeric version number. The two-field keyset
        // predicate ("strictly after" the cursor) is an Or of the compound conditions; versionNumber compares numerically.
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Eq("environment", environment);
        if (hasCursor)
        {
            filter = b.And(
                filter,
                b.Or(
                    b.Gt("baseWorkflowId", cursor.BaseWorkflowId),
                    b.And(b.Eq("baseWorkflowId", cursor.BaseWorkflowId), b.Gt("versionNumber", cursor.VersionNumber))));
        }

        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("baseWorkflowId").Ascending("versionNumber");

        var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
        try
        {
            List<BsonDocument> rows = await this.availability.Find(filter).Sort(sort).Limit(pageSize + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
            bool hasMore = false;
            string lastBaseWorkflowId = string.Empty;
            int lastVersionNumber = 0;
            foreach (BsonDocument document in rows)
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                lastBaseWorkflowId = document["baseWorkflowId"].AsString;
                lastVersionNumber = document["versionNumber"].AsInt32;
                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(document["doc"].AsBsonBinaryData.Bytes));
            }

            return hasMore
                ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, environment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
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

    // The deterministic composite document id: the three key parts joined by a single space. A space cannot collide with
    // a numeric version, so the parts are recoverable as a stable point-op key; the unique _id makes the insert idempotent.
    private static string Key(string baseWorkflowId, int versionNumber, string environment)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId} {versionNumber} {environment}");

    private static bool TryDecodeCursor(JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    private async ValueTask<byte[]?> ReadDocumentAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.availability
            .Find(Builders<BsonDocument>.Filter.Eq("_id", Key(baseWorkflowId, versionNumber, environment)))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        // The by-version axis seeks (baseWorkflowId, versionNumber) then ranges over environment; the by-environment axis
        // seeks environment then ranges over (baseWorkflowId, versionNumber). Each is its own ascending compound index.
        var byVersion = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("baseWorkflowId").Ascending("versionNumber").Ascending("environment"));
        var byEnvironment = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("environment").Ascending("baseWorkflowId").Ascending("versionNumber"));
        await this.availability.Indexes.CreateManyAsync([byVersion, byEnvironment], cancellationToken).ConfigureAwait(false);
    }
}