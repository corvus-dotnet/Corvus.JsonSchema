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
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage path by
/// label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a deployment
/// keeps those reach-disjoint. The driver pools connections internally, so the store is naturally concurrent.
/// </remarks>
public sealed class MongoSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
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
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceCredentialSerialization.SerializeNew(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.Discriminator(definition.ManagementTags, definition.UsageTags);
        var document = new BsonDocument
        {
            ["_id"] = Key(definition.SourceName, definition.Environment, tags),
            ["sourceName"] = definition.SourceName,
            ["environment"] = definition.Environment,
            ["tags"] = tags,
            ["etag"] = etag.Value!,
            ["doc"] = new BsonBinaryData(json),
        };
        try
        {
            await this.credentials.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new InvalidOperationException($"A source credential binding for '{definition.SourceName}@{definition.Environment}' with those security tags already exists.");
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
    public async ValueTask<PooledDocumentList<SourceCredentialBinding>> ListAsync(AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var docs = new PooledDocumentList<SourceCredentialBinding>();
        try
        {
            List<BsonDocument> documents = await this.credentials.Find(Builders<BsonDocument>.Filter.Empty)
                .Sort(Builders<BsonDocument>.Sort.Ascending("sourceName").Ascending("environment"))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (BsonDocument document in documents)
            {
                byte[] json = document["doc"].AsBsonBinaryData.Bytes;
                using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                if (context.Admits(AccessVerb.Read, candidate.RootElement.ManagementTagsValue))
                {
                    docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
                }
            }

            return docs;
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var update = Builders<BsonDocument>.Update
            .Set("etag", SourceCredentialSerialization.EtagOf(json).Value!)
            .Set("doc", new BsonBinaryData(json));
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
        await this.credentials.Indexes.CreateManyAsync([bySource, bySourceEnvironment], cancellationToken).ConfigureAwait(false);
    }
}