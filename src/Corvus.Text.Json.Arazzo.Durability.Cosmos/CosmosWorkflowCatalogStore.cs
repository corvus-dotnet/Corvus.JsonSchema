// <copyright file="CosmosWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IWorkflowCatalogStore"/>. Each version is a document holding the
/// canonical package (as a base64 byte array) plus the projected searchable/governance metadata; versions of a
/// base workflow share a partition (the partition key is the base workflow id) so version assignment and
/// per-workflow reads are single-partition, while catalog search runs as a cross-partition query.
/// </summary>
/// <remarks>
/// The document id is <c>{baseWorkflowId}-v{versionNumber}</c>, so the next version is assigned by querying the
/// current max within the base-id partition and attempting a create-if-not-exists; a concurrent add that already
/// took that number surfaces as a <see cref="HttpStatusCode.Conflict"/>, which is retried against the new max —
/// the same optimistic-concurrency discipline the run store uses for its checkpoint writes. Provision the
/// database and container once with <see cref="PrepareAsync(string, string, CancellationToken)"/>, then open the
/// store with <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/>; the overloads taking
/// a <see cref="CosmosClient"/> let callers configure the client (for example a least-privileged data-plane
/// managed identity) themselves.
/// </remarks>
public sealed class CosmosWorkflowCatalogStore : IWorkflowCatalogStore, IAsyncDisposable
{
    private const string CatalogContainerId = "workflow_catalog";

    private readonly CosmosClient client;
    private readonly Container catalog;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosWorkflowCatalogStore(CosmosClient client, Container catalog, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.catalog = catalog;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
    }

    /// <summary>Provisions the catalog's database and container over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string (typically the account key, which has management-plane rights).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CreateClientOptions());
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the catalog's database and container over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Creating a database/container is a Cosmos <em>management-plane</em> operation — the data-plane RBAC roles
    /// (for example <c>Cosmos DB Built-in Data Contributor</c>) cannot do it. So provisioning needs the account
    /// key or a control-plane role and must be separated from the least-privileged data-plane credential used to
    /// <see cref="ConnectAsync(CosmosClient, string, TimeProvider?, CancellationToken)"/> the store for operation.
    /// Run this once at deploy/migration time.
    /// </remarks>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the catalog store for operation against an already-provisioned database and container.</summary>
    /// <remarks>
    /// This creates no database or container, so it is safe to use a least-privileged data-plane credential.
    /// Call <see cref="PrepareAsync(string, string, CancellationToken)"/> once beforehand to provision.
    /// </remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosWorkflowCatalogStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the catalog store for operation over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a managed identity / <c>TokenCredential</c>
    /// holding only a data-plane role — so the store runs under a least-privileged principal with no account key.
    /// This creates no database or container; call <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="client">A configured Cosmos client; the caller retains ownership and must dispose it.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosWorkflowCatalogStore> ConnectAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosWorkflowCatalogStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    /// <summary>The serializer options the store relies on (camelCase property names, null properties omitted).</summary>
    /// <returns>The Cosmos client options used by the connection-string overloads.</returns>
    public static CosmosClientOptions CreateClientOptions() => new()
    {
        UseSystemTextJsonSerializerWithOptions = SerializerOptions,
    };

    /// <inheritdoc/>
    public ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8.ToArray(), metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        CatalogDocument? document = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return document?.ToVersion();
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        CatalogDocument? document = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        byte[]? bytes = document?.Package;
        return bytes is null ? null : (ReadOnlyMemory<byte>?)bytes;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        CatalogDocument? document = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        byte[]? bytes = document?.Package;
        return bytes is null ? null : CatalogPackage.GetDocument(bytes, documentName);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        int limit = query.Limit <= 0 ? 100 : query.Limit;

        var conditions = new List<string>();
        if (query.BaseWorkflowId is not null)
        {
            conditions.Add("c.baseWorkflowId = @baseWorkflowId");
        }

        if (query.Status is not null)
        {
            conditions.Add("c.status = @status");
        }

        if (query.Text is { Length: > 0 })
        {
            conditions.Add("(CONTAINS(LOWER(c.title), @text) OR (IS_DEFINED(c.description) AND CONTAINS(LOWER(c.description), @text)))");
        }

        if (query.Owner is { Length: > 0 })
        {
            conditions.Add("(CONTAINS(LOWER(c.owner.name), @owner) OR CONTAINS(LOWER(c.owner.email), @owner))");
        }

        var tagParameters = new List<(string Name, string Value)>();
        if (query.Tags is { Count: > 0 } queryTags)
        {
            for (int i = 0; i < queryTags.Count; i++)
            {
                string name = $"@tag{i.ToString(CultureInfo.InvariantCulture)}";
                conditions.Add($"ARRAY_CONTAINS(c.tags, {name})");
                tagParameters.Add((name, queryTags[i]));
            }
        }

        if (after is not null)
        {
            conditions.Add("c.sortKey > @after");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT * FROM c" + where + " ORDER BY c.sortKey");
        if (query.BaseWorkflowId is { } baseId)
        {
            definition = definition.WithParameter("@baseWorkflowId", baseId);
        }

        if (query.Status is { } status)
        {
            definition = definition.WithParameter("@status", status.ToString());
        }

        if (query.Text is { Length: > 0 } text)
        {
            definition = definition.WithParameter("@text", text.ToLowerInvariant());
        }

        if (query.Owner is { Length: > 0 } owner)
        {
            definition = definition.WithParameter("@owner", owner.ToLowerInvariant());
        }

        foreach ((string name, string value) in tagParameters)
        {
            definition = definition.WithParameter(name, value);
        }

        if (after is not null)
        {
            definition = definition.WithParameter("@after", after);
        }

        var versions = new List<CatalogVersion>();
        string? continuation = null;
        using FeedIterator<CatalogDocument> iterator = this.catalog.GetItemQueryIterator<CatalogDocument>(definition);
        while (iterator.HasMoreResults)
        {
            FeedResponse<CatalogDocument> page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (CatalogDocument document in page)
            {
                if (versions.Count == limit)
                {
                    // Fetched one beyond the page — a next page exists.
                    CatalogVersion last = versions[^1];
                    continuation = WorkflowContinuationToken.Encode(SortKey(last.BaseWorkflowId, last.VersionNumber));
                    return new CatalogPage(versions, continuation);
                }

                versions.Add(document.ToVersion());
            }
        }

        return new CatalogPage(versions, continuation);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string id = DocumentId(baseWorkflowId, versionNumber);
        var partition = new PartitionKey(baseWorkflowId);

        CatalogDocument? document;
        string etag;
        try
        {
            ItemResponse<CatalogDocument> read = await this.catalog.ReadItemAsync<CatalogDocument>(id, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
            document = read.Resource;
            etag = read.ETag;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        CatalogVersion current = document.ToVersion();
        CatalogStatus status = patch.Status ?? current.Status;
        bool newlyObsolete = status == CatalogStatus.Obsolete && current.Status != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && current.Status == CatalogStatus.Obsolete;

        CatalogOwner ownerValue = patch.Owner ?? current.Owner;
        IReadOnlyList<string> tags = patch.Tags is { } t ? [.. t] : current.Tags;
        string? obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedBy;
        DateTimeOffset? obsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAt;

        document.Status = status.ToString();
        document.Tags = tags is { Count: > 0 } ? [.. tags] : null;
        document.Owner = OwnerDocument.From(ownerValue);
        document.LastUpdatedBy = patch.UpdatedBy;
        document.LastUpdatedAt = now.ToUnixTimeMilliseconds();
        document.ObsoletedBy = obsoletedBy;
        document.ObsoletedAt = obsoletedAt?.ToUnixTimeMilliseconds();

        var options = new ItemRequestOptions { IfMatchEtag = etag };
        await this.catalog.ReplaceItemAsync(document, id, partition, options, cancellationToken).ConfigureAwait(false);

        return current with
        {
            Owner = ownerValue,
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
        try
        {
            await this.catalog.DeleteItemAsync<CatalogDocument>(
                DocumentId(baseWorkflowId, versionNumber), new PartitionKey(baseWorkflowId), cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT c.baseWorkflowId, c.versionNumber, c.workflowId FROM c WHERE c.status = @status")
            .WithParameter("@status", nameof(CatalogStatus.Obsolete));

        var refs = new List<CatalogVersionRef>();
        using FeedIterator<RefResult> iterator = this.catalog.GetItemQueryIterator<RefResult>(definition);
        while (iterator.HasMoreResults)
        {
            FeedResponse<RefResult> page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (RefResult result in page)
            {
                refs.Add(new CatalogVersionRef(result.BaseWorkflowId, result.VersionNumber, result.WorkflowId));
            }
        }

        return refs;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        foreach (CatalogVersionRef reference in versions)
        {
            await this.DeleteAsync(reference.BaseWorkflowId, reference.VersionNumber, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient)
        {
            this.client.Dispose();
        }

        return default;
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(CatalogContainerId, "/baseWorkflowId"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosWorkflowCatalogStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        // GetDatabase/GetContainer return proxies without network I/O (no creation), so this is a pure
        // data-plane open against the already-provisioned resources.
        Database database = client.GetDatabase(databaseName);
        Container catalog = database.GetContainer(CatalogContainerId);
        return new CosmosWorkflowCatalogStore(client, catalog, timeProvider ?? TimeProvider.System, ownsClient);
    }

    private static string DocumentId(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}-v{versionNumber}");

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        var partition = new PartitionKey(baseWorkflowId);
        IReadOnlyList<string> tags = metadata.Tags is { Count: > 0 } t ? [.. t] : [];

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

            var document = CatalogDocument.From(version, projection.CanonicalPackage.ToArray());

            try
            {
                await this.catalog.CreateItemAsync(document, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
                return version;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // A concurrent add already claimed this version number; recompute the max and retry.
            }
        }
    }

    private async ValueTask<int> MaxVersionAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT VALUE MAX(c.versionNumber) FROM c WHERE c.baseWorkflowId = @baseWorkflowId")
            .WithParameter("@baseWorkflowId", baseWorkflowId);
        using FeedIterator<int?> iterator = this.catalog.GetItemQueryIterator<int?>(
            definition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(baseWorkflowId) });
        while (iterator.HasMoreResults)
        {
            FeedResponse<int?> page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (int? value in page)
            {
                return value ?? 0;
            }
        }

        return 0;
    }

    private async ValueTask<CatalogDocument?> ReadOneAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<CatalogDocument> response = await this.catalog.ReadItemAsync<CatalogDocument>(
                DocumentId(baseWorkflowId, versionNumber), new PartitionKey(baseWorkflowId), cancellationToken: cancellationToken).ConfigureAwait(false);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private sealed class CatalogDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("baseWorkflowId")]
        public string BaseWorkflowId { get; set; } = string.Empty;

        [JsonPropertyName("versionNumber")]
        public int VersionNumber { get; set; }

        [JsonPropertyName("sortKey")]
        public string SortKey { get; set; } = string.Empty;

        [JsonPropertyName("workflowId")]
        public string WorkflowId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("owner")]
        public OwnerDocument Owner { get; set; } = new();

        [JsonPropertyName("sources")]
        public List<SourceDocument>? Sources { get; set; }

        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;

        [JsonPropertyName("package")]
        public byte[] Package { get; set; } = [];

        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("lastUpdatedBy")]
        public string? LastUpdatedBy { get; set; }

        [JsonPropertyName("lastUpdatedAt")]
        public long? LastUpdatedAt { get; set; }

        [JsonPropertyName("obsoletedBy")]
        public string? ObsoletedBy { get; set; }

        [JsonPropertyName("obsoletedAt")]
        public long? ObsoletedAt { get; set; }

        public static CatalogDocument From(CatalogVersion version, byte[] package) => new()
        {
            Id = DocumentId(version.BaseWorkflowId, version.VersionNumber),
            BaseWorkflowId = version.BaseWorkflowId,
            VersionNumber = version.VersionNumber,
            SortKey = SortKey(version.BaseWorkflowId, version.VersionNumber),
            WorkflowId = version.WorkflowId,
            Title = version.Title,
            Description = version.Description,
            Status = version.Status.ToString(),
            Tags = version.Tags is { Count: > 0 } t ? [.. t] : null,
            Owner = OwnerDocument.From(version.Owner),
            Sources = version.Sources is { Count: > 0 } s ? [.. s.Select(SourceDocument.From)] : null,
            Hash = version.Hash,
            Package = package,
            CreatedBy = version.CreatedBy,
            CreatedAt = version.CreatedAt.ToUnixTimeMilliseconds(),
        };

        public CatalogVersion ToVersion() => new(
            BaseWorkflowId: this.BaseWorkflowId,
            VersionNumber: this.VersionNumber,
            WorkflowId: this.WorkflowId,
            Title: this.Title,
            Description: this.Description,
            Status: Enum.Parse<CatalogStatus>(this.Status),
            Tags: this.Tags is { Count: > 0 } t ? [.. t] : [],
            Owner: this.Owner.ToOwner(),
            Sources: this.Sources is { Count: > 0 } s ? [.. s.Select(static d => d.ToRef())] : [],
            Hash: this.Hash,
            CreatedBy: this.CreatedBy,
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(this.CreatedAt),
            LastUpdatedBy: this.LastUpdatedBy,
            LastUpdatedAt: this.LastUpdatedAt is { } lua ? DateTimeOffset.FromUnixTimeMilliseconds(lua) : null,
            ObsoletedBy: this.ObsoletedBy,
            ObsoletedAt: this.ObsoletedAt is { } oa ? DateTimeOffset.FromUnixTimeMilliseconds(oa) : null);
    }

    private sealed class OwnerDocument
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("team")]
        public string? Team { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        public static OwnerDocument From(CatalogOwner owner) => new()
        {
            Name = owner.Name,
            Email = owner.Email,
            Team = owner.Team,
            Url = owner.Url,
        };

        public CatalogOwner ToOwner() => new(this.Name, this.Email, this.Team, this.Url);
    }

    private sealed class SourceDocument
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        public static SourceDocument From(CatalogSourceRef source) => new() { Name = source.Name, Type = source.Type };

        public CatalogSourceRef ToRef() => new(this.Name, this.Type);
    }

    private sealed class RefResult
    {
        [JsonPropertyName("baseWorkflowId")]
        public string BaseWorkflowId { get; set; } = string.Empty;

        [JsonPropertyName("versionNumber")]
        public int VersionNumber { get; set; }

        [JsonPropertyName("workflowId")]
        public string WorkflowId { get; set; } = string.Empty;
    }
}