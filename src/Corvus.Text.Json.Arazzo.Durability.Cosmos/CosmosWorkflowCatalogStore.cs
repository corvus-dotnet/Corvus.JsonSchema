// <copyright file="CosmosWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
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
/// the same optimistic-concurrency discipline the run store uses for its checkpoint writes. Documents are written
/// and read through the Cosmos <em>stream</em> APIs so persistence flows through the <see cref="CatalogDocument"/>
/// Corvus.Text.Json schema type and never the SDK's reflection serializer. Provision the database and container
/// once with <see cref="PrepareAsync(string, string, CancellationToken)"/>, then open the store with
/// <see cref="ConnectAsync(string, string, TimeProvider?, IWorkflowMetadataProvider?, IWorkflowExecutorProvider?, CancellationToken)"/>;
/// the overloads taking a <see cref="CosmosClient"/> let callers configure the client (for example a
/// least-privileged data-plane managed identity) themselves.
/// </remarks>
public sealed class CosmosWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string CatalogContainerId = "workflow_catalog";

    private static readonly byte[] BaseWorkflowIdProperty = "baseWorkflowId"u8.ToArray();
    private static readonly byte[] VersionNumberProperty = "versionNumber"u8.ToArray();
    private static readonly byte[] WorkflowIdProperty = "workflowId"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container catalog;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;
    private readonly bool ownsClient;

    private CosmosWorkflowCatalogStore(CosmosClient client, Container catalog, TimeProvider timeProvider, bool ownsClient, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.client = client;
        this.catalog = catalog;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
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
    /// <see cref="ConnectAsync(CosmosClient, string, TimeProvider?, IWorkflowMetadataProvider?, IWorkflowExecutorProvider?, CancellationToken)"/> the store for operation.
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
    /// <param name="metadataProvider">An optional provider that enriches the projected metadata of each added version; <see langword="null"/> to project without it.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked into each added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosWorkflowCatalogStore>(Connect(client, databaseName, timeProvider, ownsClient: true, metadataProvider, executorProvider));
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
    /// <param name="metadataProvider">An optional provider that enriches the projected metadata of each added version; <see langword="null"/> to project without it.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked into each added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosWorkflowCatalogStore> ConnectAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosWorkflowCatalogStore>(Connect(client, databaseName, timeProvider, ownsClient: false, metadataProvider, executorProvider));
    }

    /// <summary>The Cosmos client options the store relies on.</summary>
    /// <remarks>
    /// The store reads and writes through the Cosmos stream APIs and serializes documents with Corvus.Text.Json, so
    /// no SDK serializer is configured.
    /// </remarks>
    /// <returns>The Cosmos client options used by the connection-string overloads.</returns>
    public static CosmosClientOptions CreateClientOptions() => new();

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        (CatalogDocument Document, string Etag)? read = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return read is { } r ? r.Document.ToVersion() : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        (CatalogDocument Document, string Etag)? read = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);

        // The null branch must be a genuine null Nullable: a bare `null` here would bind the conditional to byte[]
        // and the implicit byte[]→ReadOnlyMemory<byte> conversion turns a missing version into an empty (non-null)
        // memory rather than the expected "absent".
        return read is { } r ? r.Document.PackageBytes() : (ReadOnlyMemory<byte>?)null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        (CatalogDocument Document, string Etag)? read = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return read is { } r ? CatalogPackage.GetDocument(r.Document.PackageBytes(), documentName) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

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

        if (query.WorkflowIdPrefix is { Length: > 0 })
        {
            conditions.Add("STARTSWITH(c.workflowIdLower, @workflowIdPrefix)");
        }

        var tagParameters = new List<(string Name, string Value)>();
        if (!query.Tags.IsEmpty)
        {
            List<string> queryTags = query.Tags.ToList();
            for (int i = 0; i < queryTags.Count; i++)
            {
                string name = $"@tag{i.ToString(CultureInfo.InvariantCulture)}";
                conditions.Add($"ARRAY_CONTAINS(c.tags, {name})");
                tagParameters.Add((name, queryTags[i]));
            }
        }

        // Row-security reach (§14.2): translate the filter to a native EXISTS over the embedded securityTags
        // array; every value is bound as a query parameter (no concatenation).
        var securityParameters = new List<(string Name, string Value)>();
        if (query.Security is { } security)
        {
            int securityParam = 0;
            var emitter = new CosmosSecurityRuleEmitter("c.securityTags", "k", "v", value =>
            {
                string name = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                securityParameters.Add((name, value));
                return name;
            });
            conditions.Add(security.ToSqlPredicate(emitter));
        }

        if (after is not null)
        {
            // In distinct mode the cursor is the base workflow id alone (one row per base); in version mode it is the
            // full sort key (one row per version).
            conditions.Add(query.DistinctWorkflows ? "c.baseWorkflowId > @after" : "c.sortKey > @after");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);

        // Distinct mode collapses in-process (Cosmos SQL has no window functions), so the results must stream in base
        // order — baseWorkflowId, then versionNumber — for the representative-per-base grouping to finalize a base when
        // the base id changes. Version mode pages every matching version by its sort key.
        var definition = new QueryDefinition(
            "SELECT * FROM c" + where + (query.DistinctWorkflows ? " ORDER BY c.baseWorkflowId, c.versionNumber" : " ORDER BY c.sortKey"));
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

        if (query.WorkflowIdPrefix is { Length: > 0 } workflowIdPrefix)
        {
            definition = definition.WithParameter("@workflowIdPrefix", workflowIdPrefix.ToLowerInvariant());
        }

        foreach ((string name, string value) in tagParameters)
        {
            definition = definition.WithParameter(name, value);
        }

        foreach ((string name, string value) in securityParameters)
        {
            definition = definition.WithParameter(name, value);
        }

        if (after is not null)
        {
            definition = definition.WithParameter("@after", after);
        }

        if (query.DistinctWorkflows)
        {
            return await this.QueryDistinctWorkflowsAsync(definition, limit, cancellationToken).ConfigureAwait(false);
        }

        // The page is a pooled batch of disposable version documents (the caller disposes the page). One extra row is
        // fetched as a look-ahead to detect a further page; it is not added to the batch.
        var versions = new PooledDocumentList<CatalogVersion>(limit);
        string? nextSortKey = null;
        try
        {
            await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.catalog, definition, cancellationToken).ConfigureAwait(false))
            {
                if (versions.Count == limit)
                {
                    // Fetched one beyond the page — a next page exists; the last kept row is the cursor.
                    CatalogVersionRef last = versions[versions.Count - 1].Ref;
                    nextSortKey = CatalogDocument.ComputeSortKey(last.BaseWorkflowId, last.VersionNumber);
                    break;
                }

                versions.Add(CatalogDocument.FromJson(element).ToVersion());
            }
        }
        catch
        {
            versions.Dispose();
            throw;
        }

        return nextSortKey is not null ? CatalogPage.Create(versions, nextSortKey) : CatalogPage.Create(versions);
    }

    // The distinct-workflow (collapse-by-base) page: one representative version per base workflow, keyset-paged by base
    // id alone. Cosmos SQL has no window functions (no ROW_NUMBER/PARTITION BY), so the pushed-down query is the ordinary
    // filtered query (same WHERE, incl. the reach predicate) ordered baseWorkflowId then versionNumber, and the collapse
    // runs in-process over those base-ordered rows. A base is included when any of its versions matches (the WHERE has
    // already filtered to matching versions); the representative is the best-matching version — the newest Active, else
    // the newest Obsolete, else the newest (status rank Active=0/Obsolete=1/else=2, ties break to the higher version).
    //
    // Because the rows arrive in base order, a base's representative is final once the base id changes, so the read is
    // bounded to a look-ahead: it stops after finalizing limit+1 representatives (the extra one only signals a further
    // page and is not emitted). The winning document is cloned detached from the query page buffer (FromJson) as it is
    // selected, since the raw element span is valid only within its page.
    private async ValueTask<CatalogPage> QueryDistinctWorkflowsAsync(QueryDefinition definition, int limit, CancellationToken cancellationToken)
    {
        var reps = new PooledDocumentList<CatalogVersion>(limit);
        string? nextBaseId = null;
        try
        {
            string? currentBaseId = null;
            CatalogDocument bestForCurrent = default;
            int bestRank = int.MaxValue;
            int bestVersion = int.MinValue;

            await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.catalog, definition, cancellationToken).ConfigureAwait(false))
            {
                CatalogDocument document = CatalogDocument.FromJson(element);
                string baseWorkflowId = (string)document.BaseWorkflowId;

                if (currentBaseId is null)
                {
                    // First row: open the first base.
                    currentBaseId = baseWorkflowId;
                }
                else if (!string.Equals(baseWorkflowId, currentBaseId, StringComparison.Ordinal))
                {
                    // The base id changed, so the previous base's representative is final (rows are base-ordered).
                    if (TryFinalizeDistinctBase(reps, bestForCurrent, limit, out nextBaseId))
                    {
                        // limit+1 representatives finalized — a further page exists; stop the (bounded) read.
                        return CatalogPage.Create(reps, nextBaseId!);
                    }

                    currentBaseId = baseWorkflowId;
                    bestRank = int.MaxValue;
                    bestVersion = int.MinValue;
                }

                int rank = StatusRank((JsonElement)document.Status);
                int versionNumber = (int)document.VersionNumber;
                if (rank < bestRank || (rank == bestRank && versionNumber > bestVersion))
                {
                    bestForCurrent = document;
                    bestRank = rank;
                    bestVersion = versionNumber;
                }
            }

            // Emit the final open base (if any); its representative cannot signal a further page (the stream ended).
            if (currentBaseId is not null && TryFinalizeDistinctBase(reps, bestForCurrent, limit, out nextBaseId))
            {
                return CatalogPage.Create(reps, nextBaseId!);
            }
        }
        catch
        {
            reps.Dispose();
            throw;
        }

        return nextBaseId is not null ? CatalogPage.Create(reps, nextBaseId) : CatalogPage.Create(reps);
    }

    // Adds a finalized base's representative to the page, or — if the page is already full (limit reached) — reports the
    // look-ahead base id as the next cursor without emitting it. Returns true when the look-ahead fired (stop reading).
    private static bool TryFinalizeDistinctBase(PooledDocumentList<CatalogVersion> reps, CatalogDocument representative, int limit, out string? nextBaseId)
    {
        if (reps.Count == limit)
        {
            // One base beyond the page — a next page exists; the last emitted base id is the cursor.
            nextBaseId = reps[reps.Count - 1].Ref.BaseWorkflowId;
            return true;
        }

        nextBaseId = null;
        reps.Add(representative.ToVersion());
        return false;
    }

    // The representative-precedence rank: newest Active wins over newest Obsolete wins over newest of any other status.
    // The status is compared string-free (JsonElement.EqualsString over the persisted value) so ranking a page's worth of
    // rows allocates no per-row status string.
    private static int StatusRank(JsonElement status)
        => status.EqualsString(nameof(CatalogStatus.Active)) ? 0
            : status.EqualsString(nameof(CatalogStatus.Obsolete)) ? 1
            : 2;

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        var partition = new PartitionKey(baseWorkflowId);

        (CatalogDocument Document, string Etag)? read = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (read is not { } found)
        {
            return null;
        }

        // Patch only the changed governance fields through the mutable builder; every other field (incl. securityTags) is
        // carried bytes-to-bytes from the current document — no per-field string realisation. The patched bytes are parsed
        // into a fresh pooled document that is independent of (and outlives) the current one.
        ParsedJsonDocument<CatalogVersion> updated;
        using (ParsedJsonDocument<CatalogVersion> currentDoc = found.Document.ToVersion())
        {
            updated = ParsedJsonDocument<CatalogVersion>.Parse(CatalogVersion.CreatePatchedBytes(currentDoc.RootElement, patch, now));
        }

        try
        {
            var options = new ItemRequestOptions { IfMatchEtag = found.Etag };
            using var stream = CosmosJson.WriteToStream(
                (Version: updated.RootElement, Package: found.Document.PackageBytes()),
                static (Utf8JsonWriter writer, in (CatalogVersion Version, byte[] Package) c) => CatalogDocument.WriteJson(writer, c.Version, c.Package));
            using ResponseMessage response = await this.catalog.ReplaceItemStreamAsync(
                stream, CatalogDocument.DocumentId(baseWorkflowId, versionNumber), partition, options, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                updated.Dispose();
                return null;
            }

            response.EnsureSuccessStatusCode();
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.catalog.DeleteItemStreamAsync(
            CatalogDocument.DocumentId(baseWorkflowId, versionNumber), new PartitionKey(baseWorkflowId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT c.baseWorkflowId, c.versionNumber, c.workflowId FROM c WHERE c.status = @status")
            .WithParameter("@status", nameof(CatalogStatus.Obsolete));

        var refs = new List<CatalogVersionRef>();
        await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.catalog, definition, cancellationToken).ConfigureAwait(false))
        {
            string? baseWorkflowId = CosmosJson.GetString(element, BaseWorkflowIdProperty);
            string? workflowId = CosmosJson.GetString(element, WorkflowIdProperty);
            long? versionNumber = CosmosJson.GetInt64(element, VersionNumberProperty);
            if (baseWorkflowId is not null && workflowId is not null && versionNumber is { } v)
            {
                refs.Add(new CatalogVersionRef(baseWorkflowId, (int)v, workflowId));
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

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(CatalogContainerId, "/baseWorkflowId"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosWorkflowCatalogStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        // GetDatabase/GetContainer return proxies without network I/O (no creation), so this is a pure
        // data-plane open against the already-provisioned resources.
        Database database = client.GetDatabase(databaseName);
        Container catalog = database.GetContainer(CatalogContainerId);
        return new CosmosWorkflowCatalogStore(client, catalog, timeProvider ?? TimeProvider.System, ownsClient, metadataProvider, executorProvider);
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryElementsAsync(Container container, QueryDefinition query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator iterator = container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                yield return element;
            }
        }
    }

    private async ValueTask<ParsedJsonDocument<CatalogVersion>> AddCoreAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        var partition = new PartitionKey(baseWorkflowId);
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int versionNumber = await this.MaxVersionAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);

            // The version is a pooled, disposable document the caller owns on success; on a concurrency conflict it is
            // disposed and rebuilt against the recomputed max.
            ParsedJsonDocument<CatalogVersion> version = CatalogVersion.Create(
                baseWorkflowId: baseWorkflowId,
                versionNumber: versionNumber,
                workflowId: projection.WorkflowId,
                title: projection.Title,
                description: projection.Description,
                status: CatalogStatus.Active,
                tags: tags,
                owner: metadata.Owner,
                sources: SourceSet.FromSources(projection.Sources),
                hash: projection.Hash,
                createdBy: metadata.CreatedBy,
                createdAt: now,
                runnable: projection.HasExecutor,
                securityTags: securityTags);

            try
            {
                using var stream = CosmosJson.WriteToStream(
                    (Version: version.RootElement, Package: projection.CanonicalPackage),
                    static (Utf8JsonWriter writer, in (CatalogVersion Version, ReadOnlyMemory<byte> Package) c) => CatalogDocument.WriteJson(writer, c.Version, c.Package));
                using ResponseMessage response = await this.catalog.CreateItemStreamAsync(stream, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // A concurrent add already claimed this version number; recompute the max and retry.
                    version.Dispose();
                    continue;
                }

                response.EnsureSuccessStatusCode();
                return version;
            }
            catch
            {
                version.Dispose();
                throw;
            }
        }
    }

    private async ValueTask<int> MaxVersionAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT VALUE MAX(c.versionNumber) FROM c WHERE c.baseWorkflowId = @baseWorkflowId")
            .WithParameter("@baseWorkflowId", baseWorkflowId);
        using FeedIterator iterator = this.catalog.GetItemQueryStreamIterator(
            definition, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(baseWorkflowId) });
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                return (int)(CosmosJson.AsInt64OrNull(element) ?? 0);
            }
        }

        return 0;
    }

    private async ValueTask<(CatalogDocument Document, string Etag)?> ReadOneAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.catalog.ReadItemStreamAsync(
            CatalogDocument.DocumentId(baseWorkflowId, versionNumber), new PartitionKey(baseWorkflowId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
        return (CatalogDocument.FromJson(payload.Memory), response.Headers.ETag);
    }
}