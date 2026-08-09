// <copyright file="MongoEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
/// Management point reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) in memory over
/// the small candidate set for a name, since a deployment keeps those reach-disjoint; list/count push the reach into
/// the query over the multikey-indexed <c>securityTags</c> mirror (the same predicate, applied by the server), so
/// out-of-reach rows never leave the store. The driver pools connections internally, so the store is naturally
/// concurrent.
/// </remarks>
public sealed class MongoEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    // The §14.2 reach predicate translated to a Mongo filter over the securityTags mirror ({ k, v } array elements).
    // The emitter is immutable, so one instance serves every query.
    private static readonly MongoSecurityRuleEmitter SecurityEmitter = new("securityTags", "k", "v");

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> environments;
    private readonly IMongoCollection<BsonDocument> tenancyLedger;

    private MongoEnvironmentStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.environments = database.GetCollection<BsonDocument>("environments");

        // Its own collection rather than a reserved _id in `environments`: that collection's first list page filters on
        // nothing, so a ledger row would be streamed as a candidate environment and parsed as one.
        this.tenancyLedger = database.GetCollection<BsonDocument>("environmenttenancyledger");
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

            // The queryable mirror of the management tags the §14.2 list/count reach predicate evaluates server-side;
            // a re-tagging update re-sets it in step (see UpdateAsync).
            ["securityTags"] = MongoSecurityTags.ToBson(draft.ManagementTagsValue),
            ["doc"] = new BsonBinaryData(json),
        };
        try
        {
            await this.environments.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            ThrowHelper.ThrowEnvironmentAlreadyExists(draft.NameValue);
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

        // The §14.2 read reach pushed into the query over the securityTags mirror (the same predicate context.Admits
        // evaluates, but applied by the server against the multikey index), so out-of-reach rows never leave the
        // store and the server-side limit is a true page bound.
        if (context.Reach(AccessVerb.Read) is { } reach)
        {
            filter = b.And(filter, reach.ToPredicate(SecurityEmitter));
        }

        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("_id.n").Ascending("_id.t");

        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            // Every row the cursor yields is already admitted, so the loop is a pure page fill; the (pageSize + 1)th
            // row is the lookahead that signals a continuation token — the row key of the last *included* environment.
            using IAsyncCursor<BsonDocument> mongoCursor = await this.environments.Find(filter).Sort(sort).Limit(pageSize + 1).ToCursorAsync(cancellationToken).ConfigureAwait(false);
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
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("etag", EnvironmentSerialization.EtagOf(json).Value!)
            .Set("doc", new BsonBinaryData(json));

        // A draft that supplies management tags re-tags the row's reach scope (a store-level replace; an omitted set
        // is carried forward), so the securityTags mirror the list/count reach predicate reads is re-set in the same
        // write — left stale, the listing keeps deciding by the old tags and drifts from this store's own get.
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            update = update.Set("securityTags", MongoSecurityTags.ToBson(draft.ManagementTagsValue));
        }

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
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Empty;
        if (context.Reach(AccessVerb.Read) is { } reach)
        {
            filter = reach.ToPredicate(SecurityEmitter);
        }

        // A native bounded count: the same reach predicate the list pushes down, with the server told to stop counting
        // one row past the cap — the (cap + 1)th admitted row trips Capped.
        long total = await this.environments.CountDocumentsAsync(
            filter,
            new CountOptions { Limit = cap + 1 },
            cancellationToken).ConfigureAwait(false);
        return total > cap ? (cap, true) : ((int)total, false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<TenancyLedger>?> GetTenancyLedgerAsync(CancellationToken cancellationToken)
    {
        BsonDocument? row = await this.tenancyLedger
            .Find(Builders<BsonDocument>.Filter.Eq("_id", TenancyLedgerId))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : PersistedJson.ToPooledDocument<TenancyLedger>(row["doc"].AsBsonBinaryData.Bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryCommitTenancyLedgerAsync(TenancyLedger current, ReadOnlyMemory<byte> admitting, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = TenancyLedgerSerialization.SerializeCommitted(current, admitting, actor, this.timeProvider.GetUtcNow(), etag);
        var row = new BsonDocument
        {
            { "_id", TenancyLedgerId },
            { "etag", etag.Value! },
            { "doc", new BsonBinaryData(json) },
        };

        // One server-side operation carries the whole predicate, so the compare and the swap cannot be interleaved: the
        // unique _id for the first write, and an etag-matching replace thereafter.
        if (current.IsUndefined())
        {
            try
            {
                await this.tenancyLedger.InsertOneAsync(row, options: null, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }

        ReplaceOneResult replaced = await this.tenancyLedger.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", TenancyLedgerId),
                Builders<BsonDocument>.Filter.Eq("etag", current.EtagValue.Value!)),
            row,
            (ReplaceOptions?)null,
            cancellationToken).ConfigureAwait(false);
        return replaced.ModifiedCount == 1;
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

    // The tenancy ledger's fixed _id: there is exactly one row per deployment, and the unique _id is what makes the
    // first write's compare-and-swap ("no row must exist") a single server-side operation.
    private const string TenancyLedgerId = "tenancy";

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

        // Multikey index over the securityTags mirror so the pushed-down §14.2 reach predicate is an index lookup, not
        // a collection scan.
        var bySecurityTags = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("securityTags.k").Ascending("securityTags.v"));
        await this.environments.Indexes.CreateManyAsync([byName, bySecurityTags], cancellationToken).ConfigureAwait(false);
    }
}