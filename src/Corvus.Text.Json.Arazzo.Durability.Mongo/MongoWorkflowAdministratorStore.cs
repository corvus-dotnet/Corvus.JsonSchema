// <copyright file="MongoWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration record for a
/// base workflow id — the mutable set of administrator identities entitled to publish further versions and to manage
/// administration. Each record is stored as its <see cref="WorkflowAdministrators"/> document in a binary <c>doc</c>
/// field, keyed directly by BaseWorkflowId (the Mongo <c>_id</c>); its etag travels in an <c>etag</c> field for the
/// optimistic-concurrency check. The record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// The id is the BaseWorkflowId verbatim — there is no composite key, tags, or reach (unlike the source-credential
/// store), so this seam is a plain CAS key/value persistence. The driver pools connections internally, so the store is
/// naturally concurrent; the <see cref="PutAsync"/> create-or-replace reads the current document and compares its etag
/// before writing, mirroring the other backends.
/// </remarks>
public sealed class MongoWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> administrators;

    private MongoWorkflowAdministratorStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.administrators = database.GetCollection<BsonDocument>("workflowAdministrators");
    }

    /// <summary>Provisions the store over a connection string.</summary>
    /// <remarks>
    /// The record is keyed by the unique <c>_id</c> (the BaseWorkflowId), so no extra index is required and the
    /// collection itself is created lazily on first write — this method exists only to mirror the other backends'
    /// deploy-time provisioning seam, and the operational user used to
    /// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/> the store needs only <c>readWrite</c>.
    /// </remarks>
    /// <param name="connectionString">A MongoDB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once provisioning is done (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return default;
    }

    /// <summary>Provisions the store over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once provisioning is done (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(IMongoClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return default;
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoWorkflowAdministratorStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoWorkflowAdministratorStore>(new MongoWorkflowAdministratorStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoWorkflowAdministratorStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoWorkflowAdministratorStore>(new MongoWorkflowAdministratorStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        byte[]? json = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        byte[] json;
        if (existing is not null)
        {
            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != WorkflowAdministratorsSerialization.EtagOf(existing))
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeUpdated(existing, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
            var update = Builders<BsonDocument>.Update
                .Set("etag", WorkflowAdministratorsSerialization.EtagOf(json).Value!)
                .Set("doc", new BsonBinaryData(json));
            await this.administrators.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", baseWorkflowId),
                update,
                options: null,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
            var document = new BsonDocument
            {
                ["_id"] = baseWorkflowId,
                ["etag"] = WorkflowAdministratorsSerialization.EtagOf(json).Value!,
                ["doc"] = new BsonBinaryData(json),
            };
            await this.administrators.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }

        return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
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

    private async ValueTask<byte[]?> ReadDocumentAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.administrators
            .Find(Builders<BsonDocument>.Filter.Eq("_id", baseWorkflowId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }
}