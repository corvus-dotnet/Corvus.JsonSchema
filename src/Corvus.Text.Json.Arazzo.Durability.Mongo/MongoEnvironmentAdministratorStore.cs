// <copyright file="MongoEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for a
/// deployment environment — the mutable set of administrator identities entitled to manage administration. Each record is
/// stored as its <see cref="EnvironmentAdministrators"/> document in a binary <c>doc</c> field, keyed directly by
/// EnvironmentName (the Mongo <c>_id</c>); its etag travels in an <c>etag</c> field for the optimistic-concurrency check.
/// The record holds deployment-stamped identities only — never secret material. Mirrors
/// <see cref="MongoWorkflowAdministratorStore"/>, including the reverse administration index that powers
/// <see cref="ListAdministeredAsync"/>.
/// </summary>
/// <remarks>
/// The id is the EnvironmentName verbatim — there is no composite key, tags, or reach, so this seam is a plain CAS
/// key/value persistence. The driver pools connections internally, so the store is naturally concurrent; the
/// <see cref="PutAsync"/> create-or-replace reads the current document and compares its etag before writing, mirroring the
/// other backends.
/// </remarks>
public sealed class MongoEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> administrators;

    private MongoEnvironmentAdministratorStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.administrators = database.GetCollection<BsonDocument>("environment_administrators");
    }

    /// <summary>Provisions the store over a connection string.</summary>
    /// <remarks>
    /// The record is keyed by the unique <c>_id</c> (the EnvironmentName), so no extra index is required and the
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
    public static ValueTask<MongoEnvironmentAdministratorStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoEnvironmentAdministratorStore>(new MongoEnvironmentAdministratorStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoEnvironmentAdministratorStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoEnvironmentAdministratorStore>(new MongoEnvironmentAdministratorStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        byte[]? json = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParsedJsonDocument<EnvironmentAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("An environment administration record requires at least one administrator identity.", nameof(administrators));
        }

        byte[]? existing = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();

        // Mirror the administrator digests top-level (design §7.8) so the reverse index is an indexed multikey query, not a
        // scan of opaque document bytes. These are the exact digests the forward IsAdministeredBy compares. Writing this
        // array alongside the document (one single-document update/insert) retracts the record's previous digests and indexes
        // the current ones atomically — the inbox never observes the index out of step with the record's administrator set.
        var digests = new BsonArray(EnvironmentAdministeredPaging.DistinctDigests(administrators));

        byte[] json;
        if (existing is not null)
        {
            // Parse the existing record ONCE, NON-COPYING over the driver's array (the read leaf) — used for both the etag
            // check and the carried-forward merge. The caller must hold its current etag (None means "I expected no record").
            using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existing.AsMemory());
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            var update = Builders<BsonDocument>.Update
                .Set("etag", etag.Value!)
                .Set("doc", new BsonBinaryData(json))
                .Set("adminDigests", digests);
            await this.administrators.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", environmentName),
                update,
                options: null,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            var document = new BsonDocument
            {
                ["_id"] = environmentName,
                ["etag"] = etag.Value!,
                ["doc"] = new BsonBinaryData(json),
                ["adminDigests"] = digests,
            };
            await this.administrators.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }

        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);

        // The reverse-index keys live in the record's own <c>adminDigests</c> array, so removing the document retracts the
        // environment from every administrator-digest bucket it was in atomically (design §7.8) — the inbox never observes an
        // environment indexed under a digest after its record has been removed. A missing record is a no-op (the delete
        // simply affects no rows), mirroring the retract-before-reindex consistency PutAsync maintains.
        await this.administrators.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", environmentName),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;

        // The keyset cursor (the environment name == _id to page strictly after) reifies once here for the filter leaf,
        // never per row. Mongo's default string sort/compare is binary over UTF-8 (ordinal) — the contract's order.
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.AnyEq("adminDigests", adminDigest);
        if (after is not null)
        {
            filter &= Builders<BsonDocument>.Filter.Gt("_id", after);
        }

        // The (_id) sort + Limit(n+1) bounds the read to one page + 1 (lookahead) — never every administered environment.
        List<BsonDocument> documents = await this.administrators
            .Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .Limit(pageSize + 1)
            .Project(Builders<BsonDocument>.Projection.Include("_id"))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var rows = new List<string>(documents.Count);
        foreach (BsonDocument document in documents)
        {
            rows.Add(document["_id"].AsString);
        }

        return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
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

    private async ValueTask<byte[]?> ReadDocumentAsync(string environmentName, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.administrators
            .Find(Builders<BsonDocument>.Filter.Eq("_id", environmentName))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }
}