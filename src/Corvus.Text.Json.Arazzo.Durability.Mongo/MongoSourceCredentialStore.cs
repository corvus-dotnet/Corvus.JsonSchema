// <copyright file="MongoSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings — references and
/// non-sensitive metadata only, never secret material — persisted as documents. Each binding is stored as its
/// <see cref="SourceCredentialBinding"/> document in a binary <c>doc</c> field, keyed by a composite <c>_id</c>
/// (<c>{ s: sourceName, e: environment, t: tags-discriminator }</c>) so tenant-/workflow-scoped bindings for the same
/// source/environment coexist while an exact duplicate is rejected by the unique <c>_id</c>; the <c>sourceName</c>,
/// <c>environment</c>, and tag discriminator are mirrored as queryable scalar fields, and the etag travels in a
/// queryable field as well as inside the document.
/// </summary>
/// <remarks>
/// Management point reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage
/// path by label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a
/// deployment keeps those reach-disjoint; list/count push the management reach into the query over the
/// multikey-indexed <c>securityTags</c> mirror (the same predicate, applied by the server), so out-of-reach rows never
/// leave the store. The driver pools connections internally, so the store is naturally concurrent.
/// </remarks>
public sealed class MongoSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    // The §14.2 reach predicate translated to a Mongo filter over the securityTags mirror ({ k, v } array elements of
    // the MANAGEMENT tags only — reach is a management concern, never the usage tags). The emitter is immutable, so
    // one instance serves every query.
    private static readonly MongoSecurityRuleEmitter SecurityEmitter = new("securityTags", "k", "v");

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> credentials;

    private MongoSourceCredentialStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.credentials = database.GetCollection<BsonDocument>("source_credentials");
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
        await using var store = new MongoSourceCredentialStore(client, databaseName, ownsClient: true, TimeProvider.System);
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
        await using var store = new MongoSourceCredentialStore(client, databaseName, ownsClient: false, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoSourceCredentialStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoSourceCredentialStore>(new MongoSourceCredentialStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoSourceCredentialStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoSourceCredentialStore>(new MongoSourceCredentialStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceCredentialSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue);
        var document = new BsonDocument
        {
            ["_id"] = Key(draft.SourceNameValue, draft.EnvironmentValue, tags),
            ["sourceName"] = draft.SourceNameValue,
            ["environment"] = draft.EnvironmentValue,
            ["tags"] = tags,
            ["etag"] = etag.Value!,

            // The queryable mirror of the MANAGEMENT tags the §14.2 list/count reach predicate evaluates server-side
            // (never the usage tags — reach is a management concern); a re-tagging update re-sets it in step (see
            // UpdateAsync).
            ["securityTags"] = MongoSecurityTags.ToBson(draft.ManagementTagsValue),
            ["doc"] = new BsonBinaryData(json),
        };
        try
        {
            await this.credentials.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            ThrowHelper.ThrowSourceCredentialAlreadyExists(draft.SourceNameValue, draft.EnvironmentValue);
        }

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string SourceName, string Environment, string TieBreaker) cursor = (string.Empty, string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceCredentialContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // Keyset seek past the cursor in composite _id (s, e, t) order — an indexed range scan over the unique _id, not a
        // collection load. The standard 3-field keyset predicate ("strictly after" the cursor) plus a matching ascending
        // sort makes _id both the seek key and the stable total order, so the page boundary is the row key we hand back.
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (hasCursor)
        {
            filter = b.Or(
                b.Gt("_id.s", cursor.SourceName),
                b.And(b.Eq("_id.s", cursor.SourceName), b.Gt("_id.e", cursor.Environment)),
                b.And(b.Eq("_id.s", cursor.SourceName), b.Eq("_id.e", cursor.Environment), b.Gt("_id.t", cursor.TieBreaker)));
        }

        // The §14.2 read reach pushed into the query over the securityTags mirror (the same predicate context.Admits
        // evaluates, but applied by the server against the multikey index), so out-of-reach rows never leave the
        // store and the server-side limit is a true page bound.
        if (context.Reach(AccessVerb.Read) is { } reach)
        {
            filter = b.And(filter, reach.ToPredicate(SecurityEmitter));
        }

        SortDefinition<BsonDocument> sort = Builders<BsonDocument>.Sort.Ascending("_id.s").Ascending("_id.e").Ascending("_id.t");

        var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
        bool hasMore = false;
        try
        {
            // Every row the cursor yields is already admitted, so the loop is a pure page fill; the (pageSize + 1)th
            // row is the lookahead that signals a continuation token — the row key of the last *included* binding.
            using IAsyncCursor<BsonDocument> mongoCursor = await this.credentials.Find(filter).Sort(sort).Limit(pageSize + 1).ToCursorAsync(cancellationToken).ConfigureAwait(false);
            string lastSource = string.Empty, lastEnv = string.Empty, lastTags = string.Empty;
            bool stop = false;
            while (!stop && await mongoCursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in mongoCursor.Current)
                {
                    byte[] json = document["doc"].AsBsonBinaryData.Bytes;
                    ParsedJsonDocument<SourceCredentialBinding> cand = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
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
                        lastSource = id["s"].AsString;
                        lastEnv = id["e"].AsString;
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
                ? SourceCredentialPage.Create(docs, lastSource, lastEnv, lastTags)
                : SourceCredentialPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        UpdateDefinition<BsonDocument> update = Builders<BsonDocument>.Update
            .Set("etag", SourceCredentialSerialization.EtagOf(json).Value!)
            .Set("doc", new BsonBinaryData(json));

        // A draft that supplies management tags re-tags the binding's reach scope (a store-level replace; an omitted
        // set is carried forward), so the securityTags mirror the list/count reach predicate reads is re-set in the
        // same write — left stale, the listing keeps deciding by the old tags and drifts from this store's own get.
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            update = update.Set("securityTags", MongoSecurityTags.ToBson(draft.ManagementTagsValue));
        }

        await this.credentials.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Key(sourceName, environment, tags!)),
            update,
            options: null,
            cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
        }

        await this.credentials.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Key(sourceName, environment, tags!)),
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
        long total = await this.credentials.CountDocumentsAsync(
            filter,
            new CountOptions { Limit = cap + 1 },
            cancellationToken).ConfigureAwait(false);
        return total > cap ? (cap, true) : ((int)total, false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        List<BsonDocument> documents = await this.credentials
            .Find(b.And(b.Eq("sourceName", sourceName), b.Eq("environment", environment)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(document["doc"].AsBsonBinaryData.Bytes);
            if (candidate.RootElement.IsUsableBy(runTags))
            {
                return candidate;
            }

            candidate.Dispose();
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        List<BsonDocument> documents = await this.credentials
            .Find(Builders<BsonDocument>.Filter.Eq("sourceName", sourceName))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        bool any = false;
        foreach (BsonDocument document in documents)
        {
            any = true;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(document["doc"].AsBsonBinaryData.Bytes);
            if (candidate.RootElement.IsUsableBy(tags))
            {
                return CredentialSourceAccess.Granted;
            }
        }

        return any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured;
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

    // The composite document id: (sourceName, environment, tag-discriminator). The discriminator can carry arbitrary
    // tag text (and the canonical separator control char), so it is held as a structured subdocument rather than
    // concatenated into a string id — the unique _id then rejects an exact duplicate binding on insert.
    private static BsonDocument Key(string sourceName, string environment, string tags)
        => new()
        {
            ["s"] = sourceName,
            ["e"] = environment,
            ["t"] = tags,
        };

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its
    // bytes and its tag discriminator (the row key). A binding outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        List<BsonDocument> documents = await this.credentials
            .Find(b.And(b.Eq("sourceName", sourceName), b.Eq("environment", environment)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (BsonDocument document in documents)
        {
            byte[] json = document["doc"].AsBsonBinaryData.Bytes;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, document["tags"].AsString);
            }
        }

        return (null, null);
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var bySource = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("sourceName"));
        var bySourceEnvironment = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("sourceName").Ascending("environment"));

        // Multikey index over the securityTags mirror so the pushed-down §14.2 reach predicate is an index lookup, not
        // a collection scan.
        var bySecurityTags = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("securityTags.k").Ascending("securityTags.v"));
        await this.credentials.Indexes.CreateManyAsync([bySource, bySourceEnvironment, bySecurityTags], cancellationToken).ConfigureAwait(false);
    }
}