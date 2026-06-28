// <copyright file="MongoEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted as documents. Each
/// environment is stored as its <see cref="Environment"/> document in a binary <c>doc</c> field, keyed by a composite
/// <c>_id</c> (<c>{ n: name, t: tags-discriminator }</c>) so reach-isolated environments that share a name coexist while
/// an exact duplicate is rejected by the unique <c>_id</c>; the <c>name</c> and tag discriminator are mirrored as
/// queryable scalar fields, and the etag travels in a queryable field as well as inside the document.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the small candidate set for a name, since a deployment keeps those reach-disjoint. The driver pools connections
/// internally, so the store is naturally concurrent.
/// </remarks>
public sealed class MongoEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> environments;

    private MongoEnvironmentStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.environments = database.GetCollection<BsonDocument>("environments");
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
        await using var store = new MongoEnvironmentStore(client, databaseName, ownsClient: true, TimeProvider.System);
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
        await using var store = new MongoEnvironmentStore(client, databaseName, ownsClient: false, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoEnvironmentStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoEnvironmentStore>(new MongoEnvironmentStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoEnvironmentStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoEnvironmentStore>(new MongoEnvironmentStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        var document = new BsonDocument
        {
            ["_id"] = Key(draft.NameValue, tags),
            ["name"] = draft.NameValue,
            ["tags"] = tags,
            ["etag"] = etag.Value!,
            ["doc"] = new BsonBinaryData(json),
        };
        try
        {
            await this.environments.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException($"An environment named '{draft.NameValue}' with those security tags already exists.");
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = EnvironmentContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Keyset seek past the cursor in composite _id (n, t) order — an indexed range scan over the unique _id, not a
        // collection load. The standard 2-field keyset predicate ("strictly after" the cursor) plus a matching ascending
        // sort makes _id both the seek key and the stable total order, so the page boundary is the row key we hand back.
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (hasCursor)
        {
            filter = b.Or(
                b.Gt("_id.n", cursor.Name),
                b.And(b.Eq("_id.n", cursor.Name), b.Gt("_id.t", cursor.TieBreaker)));
        }

        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("_id.n").Ascending("_id.t");

        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            // Reach (§14.2) is a per-row predicate applied in memory as we stream; the cursor is consumed only until the
            // page fills, so we read ≈ pageSize / selectivity rows rather than the whole collection. A FURTHER visible row
            // beyond the page is the signal to emit a continuation token — the row key of the last *included* environment.
            using IAsyncCursor<BsonDocument> mongoCursor = await this.environments.Find(filter).Sort(sort).ToCursorAsync(cancellationToken).ConfigureAwait(false);
            string lastName = string.Empty, lastTags = string.Empty;
            bool stop = false;
            while (!stop && await mongoCursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in mongoCursor.Current)
                {
                    byte[] json = document["doc"].AsBsonBinaryData.Bytes;
                    ParsedJsonDocument<Environment> cand = PersistedJson.ToPooledDocument<Environment>(json);
                    bool kept = false;
                    try
                    {
                        SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                            ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                            : SecurityTagSet.Empty;
                        if (!context.Admits(AccessVerb.Read, tags))
                        {
                            continue;
                        }

                        if (docs.Count == pageSize)
                        {
                            hasMore = true;
                            stop = true;
                            break;
                        }

                        BsonDocument id = document["_id"].AsBsonDocument;
                        docs.Add(cand);
                        kept = true;
                        lastName = id["n"].AsString;
                        lastTags = id["t"].AsString;
                    }
                    finally
                    {
                        if (!kept)
                        {
                            cand.Dispose();
                        }
                    }
                }
            }

            return hasMore
                ? EnvironmentPage.Create(docs, lastName, lastTags)
                : EnvironmentPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var update = Builders<BsonDocument>.Update
            .Set("etag", EnvironmentSerialization.EtagOf(json).Value!)
            .Set("doc", new BsonBinaryData(json));
        await this.environments.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Key(name, tags!)),
            update,
            options: null,
            cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        await this.environments.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Key(name, tags!)),
            cancellationToken).ConfigureAwait(false);
        return true;
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

    // The composite document id: (name, tag-discriminator). The discriminator can carry arbitrary tag text (and the
    // canonical separator control char), so it is held as a structured subdocument rather than concatenated into a
    // string id — the unique _id then rejects an exact duplicate environment on insert.
    private static BsonDocument Key(string name, string tags)
        => new()
        {
            ["n"] = name,
            ["t"] = tags,
        };

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the row key). An environment outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        List<BsonDocument> documents = await this.environments
            .Find(Builders<BsonDocument>.Filter.Eq("name", name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            byte[] json = document["doc"].AsBsonBinaryData.Bytes;
            using ParsedJsonDocument<Environment> candidate = PersistedJson.ToPooledDocument<Environment>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, document["tags"].AsString);
            }
        }

        return (null, null);
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var byName = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("name"));
        await this.environments.Indexes.CreateOneAsync(byName, options: null, cancellationToken).ConfigureAwait(false);
    }
}