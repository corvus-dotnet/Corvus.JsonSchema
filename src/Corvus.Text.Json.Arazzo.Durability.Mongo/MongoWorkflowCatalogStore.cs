// <copyright file="MongoWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Execution;
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
public sealed class MongoWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string ObsoleteStatus = nameof(CatalogStatus.Obsolete);

    // The §14.4 reach translator. It holds no per-query state (a BSON filter carries its operands as values), so one
    // shared instance serves every query without allocating.
    private static readonly MongoSecurityRuleEmitter SecurityEmitter = new("securityTags", "k", "v");

    private readonly IMongoClient client;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;
    private readonly IExecutorPackageSigner? signer;
    private readonly bool ownsClient;
    private readonly IMongoCollection<BsonDocument> versions;

    private MongoWorkflowCatalogStore(IMongoClient client, string databaseName, TimeProvider timeProvider, bool ownsClient, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider, IExecutorPackageSigner? signer)
    {
        this.client = client;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
        this.signer = signer;
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
        await using var store = new MongoWorkflowCatalogStore(client, databaseName, TimeProvider.System, ownsClient: true, metadataProvider: null, executorProvider: null, signer: null);
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
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        IExecutorPackageSigner? signer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoWorkflowCatalogStore>(new MongoWorkflowCatalogStore(client, databaseName, timeProvider ?? TimeProvider.System, ownsClient: true, metadataProvider, executorProvider, signer));
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
        await using var store = new MongoWorkflowCatalogStore(client, databaseName, TimeProvider.System, ownsClient: false, metadataProvider: null, executorProvider: null, signer: null);
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
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        IExecutorPackageSigner? signer = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoWorkflowCatalogStore>(new MongoWorkflowCatalogStore(client, databaseName, timeProvider ?? TimeProvider.System, ownsClient: false, metadataProvider, executorProvider, signer));
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
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

        // The shared field filter (base/status/text/owner/prefix/tags) — identical for both query modes. The keyset
        // cursor is NOT included here: in version mode it filters the composite sortKey, but in distinct mode it filters
        // the collapsed base id after grouping, so each mode applies its own cursor below.
        FilterDefinition<BsonDocument> filter = this.BuildFilter(query);

        if (query.DistinctWorkflows)
        {
            return await this.QueryDistinctWorkflowsAsync(query, filter, limit, cancellationToken).ConfigureAwait(false);
        }

        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;

        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            if (WorkflowContinuationToken.Decode(tokenUtf8.Span) is { } after)
            {
                filter = b.And(filter, b.Gt("sortKey", after));
            }
        }

        // The reach is part of the server-side filter (see BuildFilter), so every version the cursor yields is one the
        // principal may see and the page is bounded the same way with or without a reach filter: limit+1 versions, the
        // extra one being the keyset look-ahead.
        IFindFluent<BsonDocument, BsonDocument> find = this.versions.Find(filter)
            .Sort(Builders<BsonDocument>.Sort.Ascending("sortKey"))
            .Limit(limit + 1);

        // The page is a pooled batch of disposable version documents (the caller disposes the page). Each candidate is
        // parsed once into a pooled, disposable document, and the look-ahead row is disposed immediately rather than
        // kept in the batch.
        var matches = new PooledDocumentList<CatalogVersion>(limit);
        string? nextSortKey = null;
        try
        {
            using IAsyncCursor<BsonDocument> cursor = await find.ToCursorAsync(cancellationToken).ConfigureAwait(false);
            bool full = false;
            while (!full && await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    ParsedJsonDocument<CatalogVersion> candidate = ReadVersion(document);
                    if (matches.Count == limit)
                    {
                        // There is at least one more matching row beyond this page; the last kept row is the cursor.
                        CatalogVersionRef last = matches[matches.Count - 1].Ref;
                        nextSortKey = SortKey(last.BaseWorkflowId, last.VersionNumber);
                        candidate.Dispose();
                        full = true;
                        break;
                    }

                    matches.Add(candidate);
                }
            }
        }
        catch
        {
            matches.Dispose();
            throw;
        }

        return nextSortKey is not null ? CatalogPage.Create(matches, nextSortKey) : CatalogPage.Create(matches);
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(CatalogQuery query, int cap, CancellationToken cancellationToken)
    {
        FilterDefinition<BsonDocument> filter = this.BuildFilter(query);

        // The reach is part of the filter (see BuildFilter), so both counts are native whether or not one is present:
        // a base is counted when ANY version the principal may see matches, which is the grouping the server does.
        if (query.DistinctWorkflows)
        {
            // Count distinct base workflows server-side (group by base, limit to cap+1 groups, count them).
            AggregateCountResult? distinct = await this.versions.Aggregate()
                .Match(filter)
                .Group(new BsonDocument("_id", "$baseWorkflowId"))
                .Limit(cap + 1)
                .Count()
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            long distinctTotal = distinct?.Count ?? 0;
            return distinctTotal > cap ? (cap, true) : ((int)distinctTotal, false);
        }

        long total = await this.versions.CountDocumentsAsync(filter, new CountOptions { Limit = cap + 1 }, cancellationToken).ConfigureAwait(false);
        return total > cap ? (cap, true) : ((int)total, false);
    }

    // The distinct-workflow (collapse-by-base) query: one representative version per base workflow, keyset-paged by
    // base id alone. A base is included if ANY of its versions matches the filter; the representative is the
    // best-MATCHING version — the newest Active, else the newest Obsolete, else the newest (status rank Active=0,
    // Obsolete=1, else=2; tie → higher version number). Base ordering is byte-ordinal: a non-collated Mongo
    // collection compares strings by UTF-8 byte order, which matches the in-memory reference's StringComparer.Ordinal.
    private async ValueTask<CatalogPage> QueryDistinctWorkflowsAsync(CatalogQuery query, FilterDefinition<BsonDocument> filter, int limit, CancellationToken cancellationToken)
    {
        // Decode the keyset cursor (the last returned base id); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        // Compute the representative-precedence status rank (Active=0, Obsolete=1, else=2) so the sort below can order
        // matching versions by (base id asc, status rank asc, version number desc) — the winning version per base is
        // then the first document within each base-id group.
        var addRankStage = new BsonDocument("$addFields", new BsonDocument("statusRank", new BsonDocument("$switch", new BsonDocument
        {
            ["branches"] = new BsonArray
            {
                new BsonDocument { ["case"] = new BsonDocument("$eq", new BsonArray { "$status", "Active" }), ["then"] = 0 },
                new BsonDocument { ["case"] = new BsonDocument("$eq", new BsonArray { "$status", ObsoleteStatus }), ["then"] = 1 },
            },
            ["default"] = 2,
        })));

        var precedenceSortStage = new BsonDocument("$sort", new BsonDocument
        {
            ["baseWorkflowId"] = 1,
            ["statusRank"] = 1,
            ["versionNumber"] = -1,
        });

        // Build the pipeline via the fluent aggregate API so the shared FilterDefinition renders the $match stage
        // (no hand-rolled Render); subsequent raw-BSON stages are appended verbatim.
        IAggregateFluent<BsonDocument> ranked = this.versions.Aggregate()
            .Match(filter)
            .AppendStage<BsonDocument>(addRankStage)
            .AppendStage<BsonDocument>(precedenceSortStage);

        // Collapse server-side. $group by base id takes the $first document (the representative, given the precedence
        // sort), then $match on base id > cursor, re-sort by base id, and $limit (limit + 1) for the lookahead. The
        // representative document is projected back out of the group's $first.
        //
        // The reach is already in the $match that opened the pipeline, so only versions the principal may see reach
        // the $group and the representative is the best-precedence version AMONG THOSE — which is what a collapse
        // performed after reach gives. Grouping before applying reach would be the wrong answer, and is why this
        // could not be a server-side collapse until the reach became a predicate.
        var groupStage = new BsonDocument("$group", new BsonDocument
        {
            ["_id"] = "$baseWorkflowId",
            ["rep"] = new BsonDocument("$first", "$$ROOT"),
        });
        var cursorMatchStage = new BsonDocument("$match", after is null ? [] : new BsonDocument("_id", new BsonDocument("$gt", after)));
        var baseSortStage = new BsonDocument("$sort", new BsonDocument("_id", 1));
        var limitStage = new BsonDocument("$limit", limit + 1);
        var projectStage = new BsonDocument("$replaceRoot", new BsonDocument("newRoot", "$rep"));

        IAggregateFluent<BsonDocument> collapsed = ranked
            .AppendStage<BsonDocument>(groupStage)
            .AppendStage<BsonDocument>(cursorMatchStage)
            .AppendStage<BsonDocument>(baseSortStage)
            .AppendStage<BsonDocument>(limitStage)
            .AppendStage<BsonDocument>(projectStage);

        var matches = new PooledDocumentList<CatalogVersion>(limit);
        string? nextBaseId = null;
        try
        {
            using IAsyncCursor<BsonDocument> cursor = await collapsed.ToCursorAsync(cancellationToken).ConfigureAwait(false);
            bool full = false;
            while (!full && await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in cursor.Current)
                {
                    if (matches.Count == limit)
                    {
                        // There is at least one more matching base workflow beyond this page; the cursor is the last
                        // kept row's base id alone (the page is one row per base).
                        nextBaseId = matches[matches.Count - 1].Ref.BaseWorkflowId;
                        full = true;
                        break;
                    }

                    matches.Add(ReadVersion(document));
                }
            }
        }
        catch
        {
            matches.Dispose();
            throw;
        }

        return nextBaseId is not null ? CatalogPage.Create(matches, nextBaseId) : CatalogPage.Create(matches);
    }

    private FilterDefinition<BsonDocument> BuildFilter(CatalogQuery query)
    {
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

        if (!query.Tags.IsEmpty)
        {
            filter = b.And(filter, b.All("tags", query.Tags.ToList())); // $all = contains every queried tag
        }

        if (query.Security is { } security)
        {
            // Row-security reach (§14.2) pushed down as an indexed predicate over the mirrored securityTags array
            // (§14.4): the server returns only versions the principal may see, so no other tenant's catalog entry is
            // read, paged over, counted or considered as a distinct-mode representative.
            filter = b.And(filter, security.ToPredicate(SecurityEmitter));
        }

        return filter;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        BsonDocument? document = await this.LoadAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return null;
        }

        CatalogStatus status;
        CatalogOwner owner;
        TagSet tags;
        string? obsoletedBy;
        DateTimeOffset? obsoletedAt;

        // The current row is read into a pooled, disposable document only to source the unchanged fields; its field
        // accessors return OWNED COPIES, so the values are safe after the document is disposed.
        using (ParsedJsonDocument<CatalogVersion> currentDoc = ReadVersion(document))
        {
            CatalogVersion current = currentDoc.RootElement;
            CatalogStatus currentStatus = current.StatusValue;
            status = patch.Status ?? currentStatus;
            bool newlyObsolete = status == CatalogStatus.Obsolete && currentStatus != CatalogStatus.Obsolete;
            bool reactivated = status == CatalogStatus.Active && currentStatus == CatalogStatus.Obsolete;

            owner = patch.Owner ?? current.OwnerValue;
            tags = patch.Tags ?? current.TagsValue;
            obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedByOrNull;
            obsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAtValue;
        }

        var setFields = new BsonDocument
        {
            ["status"] = status.ToString(),
            ["tags"] = new BsonArray(tags.ToList()),
            ["ownerName"] = owner.Name,
            ["ownerEmail"] = owner.Email,
            ["ownerTeam"] = (BsonValue?)owner.Team ?? BsonNull.Value,
            ["ownerUrl"] = (BsonValue?)owner.Url ?? BsonNull.Value,
            ["lastUpdatedBy"] = patch.UpdatedBy,
            ["lastUpdatedAt"] = now.ToUnixTimeMilliseconds(),
            ["obsoletedBy"] = (BsonValue?)obsoletedBy ?? BsonNull.Value,
            ["obsoletedAt"] = obsoletedAt is { } oa ? oa.ToUnixTimeMilliseconds() : BsonNull.Value,
        };

        // Re-tag (§14.2): replace the mirrored securityTags array (which reach-filtering reads) with the effective set;
        // absent → the array is left unchanged.
        if (patch.SecurityTags is { } newSecurityTags)
        {
            setFields["securityTags"] = MongoSecurityTags.ToBson(newSecurityTags);
        }

        var update = new BsonDocument("$set", setFields);

        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", Id(baseWorkflowId, versionNumber));
        await this.versions.UpdateOneAsync(filter, update, options: null, cancellationToken).ConfigureAwait(false);

        BsonDocument? updated = await this.LoadAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return updated is null ? null : ReadVersion(updated);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> UpdatePackageAsync(string baseWorkflowId, int versionNumber, ReadOnlyMemory<byte> updatedPackage, CancellationToken cancellationToken)
    {
        // Project only the stored content hash (an existing metadata field) — never the multi-MB package binary — to verify
        // the update against, rather than reading and re-canonicalizing the current package.
        FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Eq("_id", Id(baseWorkflowId, versionNumber));
        BsonDocument? document = await this.versions.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include("hash"))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return false;
        }

        // The update may change only metadata (the native binaries and their attestations, ADR 0055), so a differing
        // content hash is refused so the version's identity never drifts.
        CatalogPackage.EnsureContentHash(document["hash"].AsString, updatedPackage);

        // Replace only the package binary; the projected metadata fields are untouched. Version mutations are
        // control-plane-serialized, so a straightforward $set of the one field is sufficient.
        byte[] packageBytes = MemoryMarshal.TryGetArray(updatedPackage, out ArraySegment<byte> segment)
            && segment.Offset == 0 && segment.Array is { } array && array.Length == segment.Count
            ? array
            : updatedPackage.ToArray();
        var update = new BsonDocument("$set", new BsonDocument("package", new BsonBinaryData(packageBytes)));
        UpdateResult result = await this.versions.UpdateOneAsync(filter, update, options: null, cancellationToken).ConfigureAwait(false);
        return result.MatchedCount > 0;
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

    private static ParsedJsonDocument<CatalogVersion> ReadVersion(BsonDocument document)
        => CatalogVersion.Create(
            baseWorkflowId: document["baseWorkflowId"].AsString,
            versionNumber: (int)document["versionNumber"].AsInt32,
            workflowId: document["workflowId"].AsString,
            title: document["title"].AsString,
            description: document["description"].IsBsonNull ? null : document["description"].AsString,
            status: Enum.Parse<CatalogStatus>(document["status"].AsString),
            tags: ReadTags(document),
            owner: new CatalogOwner(
                document["ownerName"].AsString,
                document["ownerEmail"].AsString,
                document["ownerTeam"].IsBsonNull ? null : document["ownerTeam"].AsString,
                document["ownerUrl"].IsBsonNull ? null : document["ownerUrl"].AsString),
            sources: ReadSources(document),
            hash: document["hash"].AsString,
            createdBy: document["createdBy"].AsString,
            createdAt: DateTimeOffset.FromUnixTimeMilliseconds(document["createdAt"].AsInt64),
            lastUpdatedBy: document["lastUpdatedBy"].IsBsonNull ? null : document["lastUpdatedBy"].AsString,
            lastUpdatedAt: document["lastUpdatedAt"].IsBsonNull ? null : DateTimeOffset.FromUnixTimeMilliseconds(document["lastUpdatedAt"].AsInt64),
            obsoletedBy: document["obsoletedBy"].IsBsonNull ? null : document["obsoletedBy"].AsString,
            obsoletedAt: document["obsoletedAt"].IsBsonNull ? null : DateTimeOffset.FromUnixTimeMilliseconds(document["obsoletedAt"].AsInt64),
            runnable: document.GetValue("runnable", false).AsBoolean,
            executorBuildError: document.GetValue("executorBuildError", BsonNull.Value).IsBsonNull ? null : document["executorBuildError"].AsString,
            securityTags: ReadSecurityTags(document));

    private static TagSet ReadTags(BsonDocument document)
        => MongoTags.Read(document);

    private static SecurityTagSet ReadSecurityTags(BsonDocument document)
        => MongoSecurityTags.Read(document);

    private static SourceSet ReadSources(BsonDocument document)
    {
        if (!document.TryGetValue("sources", out BsonValue value) || value.IsBsonNull)
        {
            return default;
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

        return SourceSet.FromSources(sources);
    }

    private async ValueTask<ParsedJsonDocument<CatalogVersion>> AddCoreAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        // Assign the next version number atomically: read the current max for the base id, project + insert under a
        // unique _id ({base}:{version}). A concurrent add racing on the same base id collides on the duplicate _id,
        // so we re-read the max and retry — the same duplicate-key concurrency strategy the run store uses for its
        // conditional inserts (no multi-document transaction needed, which keeps this working on a standalone server).
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int versionNumber = await this.MaxVersionAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false) + 1;
            CatalogPackageProjection projection = await CatalogPackage.ProjectAsync(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider, this.signer, cancellationToken).ConfigureAwait(false);
            SourceSet sources = SourceSet.FromSources(projection.Sources);

            // The projection is the sole owner of its freshly-built canonical-package array, so take it directly rather
            // than copying — PackPooled returns an exact-sized array, so the ReadOnlyMemory wraps it whole.
            byte[] packageBytes = MemoryMarshal.TryGetArray(projection.CanonicalPackage, out ArraySegment<byte> segment)
                && segment.Offset == 0 && segment.Array is { } array && array.Length == segment.Count
                ? array
                : projection.CanonicalPackage.ToArray();

            // Bind the BSON fields directly from the projected/governance source values (no round-trip through the
            // CatalogVersion document); the pooled document is built once, for the return value.
            BsonDocument document = BuildDocument(
                baseWorkflowId: baseWorkflowId,
                versionNumber: versionNumber,
                workflowId: projection.WorkflowId,
                title: projection.Title,
                description: projection.Description,
                status: CatalogStatus.Active,
                tags: tags,
                owner: metadata.Owner,
                sources: sources,
                securityTags: securityTags,
                hash: projection.Hash,
                runnable: projection.HasExecutor,
                executorBuildError: projection.ExecutorBuildError,
                createdBy: metadata.CreatedBy,
                createdAt: now,
                package: packageBytes);
            try
            {
                await this.versions.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
                return CatalogVersion.Create(
                    baseWorkflowId: baseWorkflowId,
                    versionNumber: versionNumber,
                    workflowId: projection.WorkflowId,
                    title: projection.Title,
                    description: projection.Description,
                    status: CatalogStatus.Active,
                    tags: tags,
                    owner: metadata.Owner,
                    sources: sources,
                    hash: projection.Hash,
                    createdBy: metadata.CreatedBy,
                    createdAt: now,
                    runnable: projection.HasExecutor,
                    executorBuildError: projection.ExecutorBuildError,
                    securityTags: securityTags);
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

    private static BsonDocument BuildDocument(
        string baseWorkflowId,
        int versionNumber,
        string workflowId,
        string title,
        string? description,
        CatalogStatus status,
        TagSet tags,
        CatalogOwner owner,
        SourceSet sources,
        SecurityTagSet securityTags,
        string hash,
        bool runnable,
        string? executorBuildError,
        string createdBy,
        DateTimeOffset createdAt,
        byte[] package)
    {
        var sourcesBson = new BsonArray(sources.ToList().Select(s => new BsonDocument
        {
            ["name"] = s.Name,
            ["type"] = (BsonValue?)s.Type ?? BsonNull.Value,
        }));

        return new BsonDocument
        {
            ["_id"] = Id(baseWorkflowId, versionNumber),
            ["sortKey"] = SortKey(baseWorkflowId, versionNumber),
            ["baseWorkflowId"] = baseWorkflowId,
            ["versionNumber"] = versionNumber,
            ["workflowId"] = workflowId,
            ["workflowIdLower"] = workflowId.ToLowerInvariant(),
            ["title"] = title,
            ["description"] = (BsonValue?)description ?? BsonNull.Value,
            ["status"] = status.ToString(),
            ["tags"] = new BsonArray(tags.ToList()),
            ["securityTags"] = MongoSecurityTags.ToBson(securityTags),
            ["ownerName"] = owner.Name,
            ["ownerEmail"] = owner.Email,
            ["ownerTeam"] = (BsonValue?)owner.Team ?? BsonNull.Value,
            ["ownerUrl"] = (BsonValue?)owner.Url ?? BsonNull.Value,
            ["sources"] = sourcesBson,
            ["hash"] = hash,
            ["runnable"] = runnable,
            ["executorBuildError"] = (BsonValue?)executorBuildError ?? BsonNull.Value,
            ["package"] = new BsonBinaryData(package),
            ["createdBy"] = createdBy,
            ["createdAt"] = createdAt.ToUnixTimeMilliseconds(),
            ["lastUpdatedBy"] = BsonNull.Value,
            ["lastUpdatedAt"] = BsonNull.Value,
            ["obsoletedBy"] = BsonNull.Value,
            ["obsoletedAt"] = BsonNull.Value,
        };
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var byBaseVersion = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("baseWorkflowId").Descending("versionNumber"));
        var bySortKey = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("sortKey"));
        var byStatus = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("status"));
        var byWorkflowIdLower = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("workflowIdLower"));

        // The §14.4 reach index: compound multikey over the mirrored tag array's key and value. Both keys are inside
        // the SAME array, so this indexes one array rather than parallel ones, and it gives an $elemMatch on
        // (k, v in …) tight bounds — the difference between a reach-filtered search being indexed and being a scan.
        var bySecurityTags = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("securityTags.k").Ascending("securityTags.v"));
        await this.versions.Indexes.CreateManyAsync([byBaseVersion, bySortKey, byStatus, byWorkflowIdLower, bySecurityTags], cancellationToken).ConfigureAwait(false);
    }
}