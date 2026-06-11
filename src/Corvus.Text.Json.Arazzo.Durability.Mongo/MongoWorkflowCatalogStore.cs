// <copyright file="MongoWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IWorkflowCatalogStore"/>. Each version is a document in a dedicated
/// <c>catalogVersions</c> collection (separate from the run store's collections) holding the projected,
/// searchable metadata alongside the canonical package bytes (a BSON binary). Versions are keyed by the composite
/// <c>{baseWorkflowId}:{versionNumber}</c> document id, and a precomputed <c>sortKey</c>
/// (<c>{baseWorkflowId}{versionNumber:D10}</c>) makes (base id, version) keyset paging a single indexed range scan.
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, string, CancellationToken)"/>.
/// </remarks>
public sealed class MongoWorkflowCatalogStore : IWorkflowCatalogStore, IAsyncDisposable
{
    private const string ObsoleteStatus = nameof(CatalogStatus.Obsolete);

    private readonly IMongoClient client;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;
    private readonly IMongoCollection<BsonDocument> versions;

    private MongoWorkflowCatalogStore(IMongoClient client, string databaseName, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.versions = database.GetCollection<BsonDocument>("catalogVersions");
    }

    /// <summary>
    /// Provisions the store's indexes. Creating indexes requires the <c>createIndex</c> privilege, so run this
    /// once at deploy/migration time, separately from the least-privileged user used to
    /// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/> the store for operation. (The
    /// collection itself is created lazily on first write, so the operational user needs only <c>readWrite</c>.)
    /// </summary>
    /// <param name="connectionString">A MongoDB connection string for a user permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var client = new MongoClient(connectionString);
        await using var store = new MongoWorkflowCatalogStore(client, databaseName, TimeProvider.System, ownsClient: true);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <remarks>
    /// This creates no indexes, so it is safe to use a least-privileged operational user granted only
    /// <c>readWrite</c> on the database. Call <see cref="PrepareAsync(string, string, CancellationToken)"/> once
    /// beforehand to create the indexes.
    /// </remarks>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoWorkflowCatalogStore>(new MongoWorkflowCatalogStore(client, databaseName, timeProvider ?? TimeProvider.System, ownsClient: true));
    }

    /// <summary>Provisions the store's indexes over a caller-supplied client.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example one whose <c>MongoClientSettings</c> use an
    /// OIDC/managed-identity or AWS-IAM credential — so provisioning runs under a deliberate credential rather
    /// than one embedded in a connection string. The caller retains ownership of the client.
    /// </remarks>
    /// <param name="client">A configured MongoDB client permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await using var store = new MongoWorkflowCatalogStore(client, databaseName, TimeProvider.System, ownsClient: false);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a least-privileged (<c>readWrite</c>)
    /// OIDC/managed-identity credential — so the store runs under a least-privileged principal. This creates
    /// no indexes; call <see cref="PrepareAsync(IMongoClient, string, CancellationToken)"/> once beforehand.
    /// </remarks>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoWorkflowCatalogStore> ConnectAsync(
        IMongoClient client,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoWorkflowCatalogStore>(new MongoWorkflowCatalogStore(client, databaseName, timeProvider ?? TimeProvider.System, ownsClient: false));
    }

    /// <inheritdoc/>
    public ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8.ToArray(), metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.LoadAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return document is null ? null : ReadVersion(document);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        byte[]? package = await this.LoadPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return package is null ? null : (ReadOnlyMemory<byte>?)package;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        byte[]? package = await this.LoadPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return package is null ? null : CatalogPackage.GetDocument(package, documentName);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        int limit = query.Limit <= 0 ? 100 : query.Limit;

        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;

        if (query.BaseWorkflowId is { } baseId)
        {
            filter = b.And(filter, b.Eq("baseWorkflowId", baseId));
        }

        if (query.Status is { } status)
        {
            filter = b.And(filter, b.Eq("status", status.ToString()));
        }

        if (query.Text is { Length: > 0 } text)
        {
            string pattern = EscapeRegex(text);
            filter = b.And(filter, b.Or(
                b.Regex("title", new BsonRegularExpression(pattern, "i")),
                b.Regex("description", new BsonRegularExpression(pattern, "i"))));
        }

        if (query.Owner is { Length: > 0 } owner)
        {
            string pattern = EscapeRegex(owner);
            filter = b.And(filter, b.Or(
                b.Regex("ownerName", new BsonRegularExpression(pattern, "i")),
                b.Regex("ownerEmail", new BsonRegularExpression(pattern, "i"))));
        }

        if (query.WorkflowIdPrefix is { Length: > 0 } workflowIdPrefix)
        {
            // Anchored (^) prefix regex on the lowercased field, CASE-SENSITIVE (no "i") so it uses the
            // workflowIdLower index — a case-insensitive regex cannot. The lowercased field + lowercased
            // prefix give case-insensitive behaviour while staying index-backed.
            filter = b.And(filter, b.Regex("workflowIdLower", new BsonRegularExpression("^" + EscapeRegex(workflowIdPrefix.ToLowerInvariant()))));
        }

        if (query.Tags is { Count: > 0 } tags)
        {
            filter = b.And(filter, b.All("tags", tags)); // $all = contains every queried tag
        }

        if (WorkflowContinuationToken.Decode(query.ContinuationToken) is { } after)
        {
            filter = b.And(filter, b.Gt("sortKey", after));
        }

        List<BsonDocument> documents = await this.versions.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("sortKey"))
            .Limit(limit + 1)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var matches = new List<CatalogVersion>(documents.Count);
        foreach (BsonDocument document in documents)
        {
            matches.Add(ReadVersion(document));
        }

        string? continuation = null;
        if (matches.Count > limit)
        {
            matches.RemoveAt(matches.Count - 1);
            CatalogVersion last = matches[^1];
            continuation = WorkflowContinuationToken.Encode(SortKey(last.BaseWorkflowId, last.VersionNumber));
        }

        return new CatalogPage(matches, continuation);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        BsonDocument? document = await this.LoadAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        CatalogVersion current = ReadVersion(document);
        CatalogStatus status = patch.Status ?? current.Status;
        bool newlyObsolete = status == CatalogStatus.Obsolete && current.Status != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && current.Status == CatalogStatus.Obsolete;

        CatalogOwner owner = patch.Owner ?? current.Owner;
        IReadOnlyList<string> tags = patch.Tags is { } t ? [.. t] : current.Tags;
        string? obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedBy;
        DateTimeOffset? obsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAt;

        var update = new BsonDocument("$set", new BsonDocument
        {
            ["status"] = status.ToString(),
            ["tags"] = new BsonArray(tags),
            ["ownerName"] = owner.Name,
            ["ownerEmail"] = owner.Email,
            ["ownerTeam"] = (BsonValue?)owner.Team ?? BsonNull.Value,
            ["ownerUrl"] = (BsonValue?)owner.Url ?? BsonNull.Value,
            ["lastUpdatedBy"] = patch.UpdatedBy,
            ["lastUpdatedAt"] = now.ToUnixTimeMilliseconds(),
            ["obsoletedBy"] = (BsonValue?)obsoletedBy ?? BsonNull.Value,
            ["obsoletedAt"] = obsoletedAt is { } oa ? oa.ToUnixTimeMilliseconds() : BsonNull.Value,
        });

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", Id(baseWorkflowId, versionNumber));
        await this.versions.UpdateOneAsync(filter, update, options: null, cancellationToken).ConfigureAwait(false);

        return current with
        {
            Owner = owner,
            Tags = tags,
            Status = status,
            LastUpdatedBy = patch.UpdatedBy,
            LastUpdatedAt = now,
            ObsoletedBy = obsoletedBy,
            ObsoletedAt = obsoletedAt,
        };
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", Id(baseWorkflowId, versionNumber));
        DeleteResult result = await this.versions.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("status", ObsoleteStatus);
        List<BsonDocument> documents = await this.versions.Find(filter).ToListAsync(cancellationToken).ConfigureAwait(false);
        var refs = new List<CatalogVersionRef>(documents.Count);
        foreach (BsonDocument document in documents)
        {
            refs.Add(new CatalogVersionRef(
                document["baseWorkflowId"].AsString,
                (int)document["versionNumber"].AsInt32,
                document["workflowId"].AsString));
        }

        return refs;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            return;
        }

        IEnumerable<string> ids = versions.Select(v => Id(v.BaseWorkflowId, v.VersionNumber));
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.In("_id", ids);
        await this.versions.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
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

    private static string Id(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}:{versionNumber}");

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    private static string EscapeRegex(string value) => System.Text.RegularExpressions.Regex.Escape(value);

    private static CatalogVersion ReadVersion(BsonDocument document)
        => new(
            BaseWorkflowId: document["baseWorkflowId"].AsString,
            VersionNumber: (int)document["versionNumber"].AsInt32,
            WorkflowId: document["workflowId"].AsString,
            Title: document["title"].AsString,
            Description: document["description"].IsBsonNull ? null : document["description"].AsString,
            Status: Enum.Parse<CatalogStatus>(document["status"].AsString),
            Tags: ReadTags(document),
            Owner: new CatalogOwner(
                document["ownerName"].AsString,
                document["ownerEmail"].AsString,
                document["ownerTeam"].IsBsonNull ? null : document["ownerTeam"].AsString,
                document["ownerUrl"].IsBsonNull ? null : document["ownerUrl"].AsString),
            Sources: ReadSources(document),
            Hash: document["hash"].AsString,
            CreatedBy: document["createdBy"].AsString,
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(document["createdAt"].AsInt64),
            LastUpdatedBy: document["lastUpdatedBy"].IsBsonNull ? null : document["lastUpdatedBy"].AsString,
            LastUpdatedAt: document["lastUpdatedAt"].IsBsonNull ? null : DateTimeOffset.FromUnixTimeMilliseconds(document["lastUpdatedAt"].AsInt64),
            ObsoletedBy: document["obsoletedBy"].IsBsonNull ? null : document["obsoletedBy"].AsString,
            ObsoletedAt: document["obsoletedAt"].IsBsonNull ? null : DateTimeOffset.FromUnixTimeMilliseconds(document["obsoletedAt"].AsInt64));

    private static IReadOnlyList<string> ReadTags(BsonDocument document)
    {
        if (!document.TryGetValue("tags", out BsonValue value) || value.IsBsonNull)
        {
            return [];
        }

        BsonArray array = value.AsBsonArray;
        var tags = new List<string>(array.Count);
        foreach (BsonValue tag in array)
        {
            tags.Add(tag.AsString);
        }

        return tags;
    }

    private static IReadOnlyList<CatalogSourceRef> ReadSources(BsonDocument document)
    {
        if (!document.TryGetValue("sources", out BsonValue value) || value.IsBsonNull)
        {
            return [];
        }

        BsonArray array = value.AsBsonArray;
        var sources = new List<CatalogSourceRef>(array.Count);
        foreach (BsonValue entry in array)
        {
            BsonDocument source = entry.AsBsonDocument;
            string name = source["name"].AsString;
            string? type = source.TryGetValue("type", out BsonValue t) && !t.IsBsonNull ? t.AsString : null;
            sources.Add(new CatalogSourceRef(name, type));
        }

        return sources;
    }

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        IReadOnlyList<string> tags = metadata.Tags is { Count: > 0 } t ? [.. t] : [];

        // Assign the next version number atomically: read the current max for the base id, project + insert under a
        // unique _id ({base}:{version}). A concurrent add racing on the same base id collides on the duplicate _id,
        // so we re-read the max and retry — the same duplicate-key concurrency strategy the run store uses for its
        // conditional inserts (no multi-document transaction needed, which keeps this working on a standalone server).
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int versionNumber = await this.MaxVersionAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber);
            var version = new CatalogVersion(
                BaseWorkflowId: baseWorkflowId,
                VersionNumber: versionNumber,
                WorkflowId: projection.WorkflowId,
                Title: projection.Title,
                Description: projection.Description,
                Status: CatalogStatus.Active,
                Tags: tags,
                Owner: metadata.Owner,
                Sources: projection.Sources,
                Hash: projection.Hash,
                CreatedBy: metadata.CreatedBy,
                CreatedAt: now);

            BsonDocument document = BuildDocument(version, projection.CanonicalPackage.ToArray());
            try
            {
                await this.versions.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
                return version;
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                // A concurrent add took this version number; recompute the max and try the next one.
            }
        }
    }

    private async ValueTask<int> MaxVersionAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("baseWorkflowId", baseWorkflowId);
        BsonDocument? top = await this.versions.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Descending("versionNumber"))
            .Limit(1)
            .Project(Builders<BsonDocument>.Projection.Include("versionNumber"))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return top is null ? 0 : (int)top["versionNumber"].AsInt32;
    }

    private async ValueTask<BsonDocument?> LoadAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", Id(baseWorkflowId, versionNumber));
        return await this.versions.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<byte[]?> LoadPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", Id(baseWorkflowId, versionNumber));
        BsonDocument? document = await this.versions.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("package"))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["package"].AsBsonBinaryData.Bytes;
    }

    private static BsonDocument BuildDocument(CatalogVersion version, byte[] package)
    {
        var sources = new BsonArray(version.Sources.Select(s => new BsonDocument
        {
            ["name"] = s.Name,
            ["type"] = (BsonValue?)s.Type ?? BsonNull.Value,
        }));

        return new BsonDocument
        {
            ["_id"] = Id(version.BaseWorkflowId, version.VersionNumber),
            ["sortKey"] = SortKey(version.BaseWorkflowId, version.VersionNumber),
            ["baseWorkflowId"] = version.BaseWorkflowId,
            ["versionNumber"] = version.VersionNumber,
            ["workflowId"] = version.WorkflowId,
            ["workflowIdLower"] = version.WorkflowId.ToLowerInvariant(),
            ["title"] = version.Title,
            ["description"] = (BsonValue?)version.Description ?? BsonNull.Value,
            ["status"] = version.Status.ToString(),
            ["tags"] = new BsonArray(version.Tags),
            ["ownerName"] = version.Owner.Name,
            ["ownerEmail"] = version.Owner.Email,
            ["ownerTeam"] = (BsonValue?)version.Owner.Team ?? BsonNull.Value,
            ["ownerUrl"] = (BsonValue?)version.Owner.Url ?? BsonNull.Value,
            ["sources"] = sources,
            ["hash"] = version.Hash,
            ["package"] = new BsonBinaryData(package),
            ["createdBy"] = version.CreatedBy,
            ["createdAt"] = version.CreatedAt.ToUnixTimeMilliseconds(),
            ["lastUpdatedBy"] = (BsonValue?)version.LastUpdatedBy ?? BsonNull.Value,
            ["lastUpdatedAt"] = version.LastUpdatedAt is { } lua ? lua.ToUnixTimeMilliseconds() : BsonNull.Value,
            ["obsoletedBy"] = (BsonValue?)version.ObsoletedBy ?? BsonNull.Value,
            ["obsoletedAt"] = version.ObsoletedAt is { } oa ? oa.ToUnixTimeMilliseconds() : BsonNull.Value,
        };
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var byBaseVersion = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("baseWorkflowId").Descending("versionNumber"));
        var bySortKey = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("sortKey"));
        var byStatus = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status"));
        var byWorkflowIdLower = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("workflowIdLower"));
        await this.versions.Indexes.CreateManyAsync([byBaseVersion, bySortKey, byStatus, byWorkflowIdLower], cancellationToken).ConfigureAwait(false);
    }
}