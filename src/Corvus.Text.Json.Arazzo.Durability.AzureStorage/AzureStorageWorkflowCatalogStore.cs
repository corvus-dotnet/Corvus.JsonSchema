// <copyright file="AzureStorageWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Corvus.Runtime.InteropServices;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Storage-backed <see cref="IWorkflowCatalogStore"/>: each version's projected, searchable metadata is
/// a Table entity (PartitionKey = base workflow id, RowKey = the zero-padded version number) while the canonical
/// package envelope — which can exceed Table storage's ~64KB per-property limit — is a block blob, exactly as the
/// run store (<see cref="AzureStorageWorkflowStateStore"/>) holds its checkpoint in a blob and its index in a
/// table. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// The RowKey is the version number formatted <c>D10</c> so Table storage's natural (PartitionKey, RowKey) order
/// is exactly the catalog's (base workflow id, version number) sort order, and the
/// <c>{base}{version:D10}</c> keyset token pages identically to every other backend. Provision the container and
/// table once with <see cref="PrepareAsync(string, CancellationToken)"/>, then open the store with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/>.
/// </remarks>
public sealed class AzureStorageWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter
{
    private const string ObsoleteStatus = nameof(CatalogStatus.Obsolete);
    private const string CatalogContainer = "arazzo-catalog";
    private const string CatalogTable = "arazzocatalog";

    // The Blob SDK defaults to the newest REST API version, which the Azurite emulator (and older real
    // accounts) may not yet recognise. Pin to a broadly-supported version so requests are accepted everywhere;
    // none of the features used here need anything newer.
    private const BlobClientOptions.ServiceVersion BlobApiVersion = BlobClientOptions.ServiceVersion.V2024_11_04;

    private readonly BlobContainerClient packages;
    private readonly TableClient catalog;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;

    private AzureStorageWorkflowCatalogStore(BlobContainerClient packages, TableClient catalog, TimeProvider timeProvider, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.packages = packages;
        this.catalog = catalog;
        this.timeProvider = timeProvider;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
    }

    /// <summary>Provisions the store's blob container and table over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(BlobServiceClient, TableServiceClient, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create the container and table.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container and table exist (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(
            new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)),
            new TableServiceClient(connectionString),
            cancellationToken);
    }

    /// <summary>
    /// Provisions the store's blob container and table. Container/table creation is a broader right than the
    /// per-blob / per-entity data access the store needs at runtime, so run this once at deploy/migration time,
    /// separately from the least-privileged credential used to <see cref="ConnectAsync(BlobServiceClient, TableServiceClient, TimeProvider?, CancellationToken)"/>
    /// the store for operation.
    /// </summary>
    /// <param name="blobService">A blob service client (for example one built with a managed identity / <c>TokenCredential</c>).</param>
    /// <param name="tableService">A table service client for the same account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container and table exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(BlobServiceClient blobService, TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(tableService);

        await blobService.GetBlobContainerClient(CatalogContainer).CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(CatalogTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned container and table.</summary>
    /// <remarks>
    /// This creates no container or table, so it is safe to use a least-privileged data-plane credential (for
    /// example a managed identity granted only blob and table <em>data</em> roles). Call
    /// <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand to provision the resources.
    /// </remarks>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(
            new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)),
            new TableServiceClient(connectionString),
            timeProvider,
            metadataProvider,
            executorProvider,
            cancellationToken);
    }

    /// <summary>Opens the store for operation over caller-supplied service clients.</summary>
    /// <remarks>
    /// Supply clients the caller configured — for example with a managed identity / <c>TokenCredential</c>
    /// holding only data-plane roles — so the store runs under a least-privileged principal with no key in a
    /// connection string. This creates no container or table; call <see cref="PrepareAsync(BlobServiceClient, TableServiceClient, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="blobService">A blob service client.</param>
    /// <param name="tableService">A table service client for the same account.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkflowCatalogStore> ConnectAsync(
        BlobServiceClient blobService,
        TableServiceClient tableService,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();

        BlobContainerClient packages = blobService.GetBlobContainerClient(CatalogContainer);
        TableClient catalog = tableService.GetTableClient(CatalogTable);
        return new ValueTask<AzureStorageWorkflowCatalogStore>(new AzureStorageWorkflowCatalogStore(packages, catalog, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider));
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
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        NullableResponse<TableEntity> existing = await this.catalog
            .GetEntityIfExistsAsync<TableEntity>(baseWorkflowId, RowKey(versionNumber), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return existing.HasValue ? ReadVersion(existing.Value!) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        byte[]? package = await this.LoadPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return package is null ? null : (ReadOnlyMemory<byte>?)package;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(documentName);
        byte[]? package = await this.LoadPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return package is null ? null : CatalogPackage.GetDocument(package, documentName);
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

        // Table storage returns entities ordered by PartitionKey then RowKey; PartitionKey is the base workflow
        // id and RowKey is the D10 version, so results arrive in (base, version) order — the catalog sort order.
        // Server-side filtering covers the exact base-id and status predicates; the text/owner/tag predicates
        // (case-insensitive contains, contains-ALL) are applied client-side because Table OData cannot express
        // them, then the keyset "take limit (+1 to detect a further page)" cut is applied to the filtered stream.
        //
        // In DISTINCT mode the server-side status filter must NOT be applied: a base is included if ANY of its
        // versions matches, and the representative is chosen by status precedence across all its versions, so the
        // full partition span must be scanned (the status predicate is instead applied client-side in Matches).
        string? filter = null;
        if (query.BaseWorkflowId is { } baseId)
        {
            filter = TableClient.CreateQueryFilter($"PartitionKey eq {baseId}");
        }

        if (query.Status is { } status && !query.DistinctWorkflows)
        {
            string clause = TableClient.CreateQueryFilter($"Status eq {status.ToString()}");
            filter = filter is null ? clause : filter + " and " + clause;
        }

        if (query.DistinctWorkflows)
        {
            return await this.QueryDistinctWorkflowsAsync(query, filter, after, limit, cancellationToken).ConfigureAwait(false);
        }

        // The page is a pooled batch of disposable version documents (the caller disposes the page). Each candidate is
        // parsed once; matches are kept in the batch, non-matches and the look-ahead row are disposed immediately.
        var matches = new PooledDocumentList<CatalogVersion>(limit);
        string? nextSortKey = null;
        try
        {
            await foreach (TableEntity entity in this.catalog.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                string sortKey = SortKey(entity.PartitionKey, ParseRowKey(entity.RowKey));
                if (after is not null && string.CompareOrdinal(sortKey, after) <= 0)
                {
                    continue;
                }

                ParsedJsonDocument<CatalogVersion> candidate = ReadVersion(entity);
                if (!Matches(candidate.RootElement, query))
                {
                    candidate.Dispose();
                    continue;
                }

                if (matches.Count == limit)
                {
                    // There is at least one more matching row beyond this page.
                    CatalogVersionRef lastRef = matches[matches.Count - 1].Ref;
                    nextSortKey = SortKey(lastRef.BaseWorkflowId, lastRef.VersionNumber);
                    candidate.Dispose();
                    break;
                }

                matches.Add(candidate);
            }
        }
        catch
        {
            matches.Dispose();
            throw;
        }

        return nextSortKey is not null ? CatalogPage.Create(matches, nextSortKey) : CatalogPage.Create(matches);
    }

    // The distinct-workflow (collapse-by-base) query: one representative version per base workflow, keyset-paged by base
    // id. A base is included if any of its versions matches the filter; the representative is the best-matching version —
    // the newest Active, else the newest Obsolete, else the newest (matching the UI's client-side collapse it replaces).
    // Table storage has no group-by/window functions, so the collapse is done in process over the SAME server-filtered
    // stream the version-mode branch uses (base-id predicate only; the status predicate and the text/owner/tag/reach
    // predicates are re-applied here via Matches, because a base is selected by ANY matching version). Each candidate is
    // parsed once to filter + rank, retaining only the winning per-base TableEntity; the page window's winners are
    // re-parsed into the pooled batch. Table (PartitionKey, RowKey) order is byte-ordinal, so the bases arrive already in
    // StringComparer.Ordinal order — the same order the keyset cursor (the last emitted base id alone) advances through.
    private async ValueTask<CatalogPage> QueryDistinctWorkflowsAsync(CatalogQuery query, string? filter, string? after, int limit, CancellationToken cancellationToken)
    {
        // Collect the best-matching representative entity per base id. The dictionary is ordinal-ordered so the emit
        // loop below walks bases in byte-ordinal (== StringComparer.Ordinal) order, matching the keyset cursor.
        var reps = new SortedDictionary<string, RepCandidate>(StringComparer.Ordinal);
        await foreach (TableEntity entity in this.catalog.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            using ParsedJsonDocument<CatalogVersion> candidate = ReadVersion(entity);
            if (!Matches(candidate.RootElement, query))
            {
                continue;
            }

            CatalogVersionRef reference = candidate.RootElement.Ref;
            var incoming = new RepCandidate(entity, StatusRank(candidate.RootElement.StatusValue), reference.VersionNumber);
            if (!reps.TryGetValue(reference.BaseWorkflowId, out RepCandidate existing) || incoming.IsBetterThan(existing))
            {
                reps[reference.BaseWorkflowId] = incoming;
            }
        }

        // The page is a pooled batch of disposable representative documents (the caller disposes the page). Seek strictly
        // past the cursor base id, take limit, and stop on the (+1) look-ahead base to signal a further page.
        var matches = new PooledDocumentList<CatalogVersion>(limit);
        string? nextBaseId = null;
        try
        {
            foreach (KeyValuePair<string, RepCandidate> entry in reps)
            {
                if (after is not null && string.CompareOrdinal(entry.Key, after) <= 0)
                {
                    continue;
                }

                if (matches.Count == limit)
                {
                    // There is at least one more matching base workflow beyond this page.
                    nextBaseId = matches[matches.Count - 1].Ref.BaseWorkflowId;
                    break;
                }

                matches.Add(ReadVersion(entry.Value.Entity));
            }
        }
        catch
        {
            matches.Dispose();
            throw;
        }

        return nextBaseId is not null ? CatalogPage.Create(matches, nextBaseId) : CatalogPage.Create(matches);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        NullableResponse<TableEntity> existing = await this.catalog
            .GetEntityIfExistsAsync<TableEntity>(baseWorkflowId, RowKey(versionNumber), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!existing.HasValue)
        {
            return null;
        }

        TableEntity entity = existing.Value!;

        CatalogStatus status;
        CatalogOwner owner;
        TagSet tags;
        string? obsoletedBy;
        DateTimeOffset? obsoletedAt;

        // The current row is read into a pooled, disposable document only to source the unchanged fields; its
        // field accessors return OWNED COPIES, so the values are safe after the document is disposed.
        using (ParsedJsonDocument<CatalogVersion> currentDoc = ReadVersion(entity))
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

        WriteGovernance(entity, status, tags, owner, patch.UpdatedBy, now, obsoletedBy, obsoletedAt);

        // Re-tag (§14.2): replace the encoded SecurityTags property (which in-process reach-filtering reads) with the
        // effective set; absent → the property is left unchanged.
        if (patch.SecurityTags is { } newSecurityTags)
        {
            entity["SecurityTags"] = EncodeSecurityTags(newSecurityTags);
        }

        await this.catalog.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return ReadVersion(entity);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        Response response = await this.catalog
            .DeleteEntityAsync(baseWorkflowId, RowKey(versionNumber), ETag.All, cancellationToken)
            .ConfigureAwait(false);
        await this.packages.GetBlobClient(BlobName(baseWorkflowId, versionNumber)).DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        // DeleteEntityAsync returns 404 (not an exception) when no entity existed.
        return response.Status != 404;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"Status eq {ObsoleteStatus}");
        var refs = new List<CatalogVersionRef>();
        await foreach (TableEntity entity in this.catalog.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            refs.Add(new CatalogVersionRef(
                entity.PartitionKey,
                ParseRowKey(entity.RowKey),
                entity.GetString("WorkflowId") ?? string.Empty));
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

    private static string RowKey(int versionNumber) => versionNumber.ToString("D10", CultureInfo.InvariantCulture);

    private static int ParseRowKey(string rowKey) => int.Parse(rowKey, CultureInfo.InvariantCulture);

    private static string BlobName(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}/{versionNumber:D10}");

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    // The representative-precedence rank: newest Active wins over newest Obsolete wins over newest of any other status.
    private static int StatusRank(CatalogStatus status) => status switch
    {
        CatalogStatus.Active => 0,
        CatalogStatus.Obsolete => 1,
        _ => 2,
    };

    private static bool Matches(in CatalogVersion version, CatalogQuery query)
    {
        if (query.BaseWorkflowId is { } baseId && version.Ref.BaseWorkflowId != baseId)
        {
            return false;
        }

        if (query.WorkflowIdPrefix is { Length: > 0 } prefix
            && !((string)version.WorkflowId).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.Status is { } status && version.StatusValue != status)
        {
            return false;
        }

        if (query.Text is { Length: > 0 } text)
        {
            string title = (string)version.Title;
            string? description = version.DescriptionOrNull;
            if (title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0
                && (description is null || description.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return false;
            }
        }

        if (query.Owner is { Length: > 0 } owner)
        {
            CatalogOwner ownerValue = version.OwnerValue;
            if (ownerValue.Name.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0
                && ownerValue.Email.IndexOf(owner, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }

        if (!query.Tags.AllContainedIn(version.TagsValue))
        {
            return false;
        }

        // Row-security reach (§14.2): Table OData cannot match inside the serialized security tags, so apply the
        // reach filter in process over the version's persisted tags — the only correct option for this backend.
        if (query.Security is not { } security)
        {
            return true;
        }

        SecurityTagSet securityTags = version.SecurityTags.IsNotUndefined()
            ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(version.SecurityTags).Memory)
            : SecurityTagSet.Empty;
        return security.IsSatisfiedBy(securityTags);
    }

    private static TableEntity BuildEntity(in CatalogVersion version)
    {
        CatalogVersionRef reference = version.Ref;
        var entity = new TableEntity(reference.BaseWorkflowId, RowKey(reference.VersionNumber))
        {
            ["WorkflowId"] = reference.WorkflowId,
            ["Title"] = (string)version.Title,
            ["Hash"] = (string)version.Hash,
            ["Runnable"] = (bool)version.Runnable,
            ["CreatedBy"] = (string)version.CreatedBy,
            ["CreatedAt"] = version.CreatedAtValue.ToUnixTimeMilliseconds(),
            ["Sources"] = version.SourcesValue.ToJsonStringOrNull() ?? "[]",
            ["SecurityTags"] = EncodeSecurityTags(version.SecurityTagsValue),
        };

        if (version.DescriptionOrNull is { } description)
        {
            entity["Description"] = description;
        }

        WriteGovernance(
            entity,
            version.StatusValue,
            version.TagsValue,
            version.OwnerValue,
            version.LastUpdatedByOrNull,
            version.LastUpdatedAtValue,
            version.ObsoletedByOrNull,
            version.ObsoletedAtValue);
        return entity;
    }

    private static void WriteGovernance(
        TableEntity entity,
        CatalogStatus status,
        TagSet tags,
        CatalogOwner owner,
        string? lastUpdatedBy,
        DateTimeOffset? lastUpdatedAt,
        string? obsoletedBy,
        DateTimeOffset? obsoletedAt)
    {
        entity["Status"] = status.ToString();
        entity["Tags"] = tags.ToJsonStringOrNull() ?? "[]";
        entity["OwnerName"] = owner.Name;
        entity["OwnerEmail"] = owner.Email;
        entity["OwnerTeam"] = owner.Team;
        entity["OwnerUrl"] = owner.Url;
        entity["LastUpdatedBy"] = lastUpdatedBy;
        entity["LastUpdatedAt"] = lastUpdatedAt?.ToUnixTimeMilliseconds();
        entity["ObsoletedBy"] = obsoletedBy;
        entity["ObsoletedAt"] = obsoletedAt?.ToUnixTimeMilliseconds();
    }

    private static ParsedJsonDocument<CatalogVersion> ReadVersion(TableEntity entity)
        => CatalogVersion.Create(
            baseWorkflowId: entity.PartitionKey,
            versionNumber: ParseRowKey(entity.RowKey),
            workflowId: entity.GetString("WorkflowId") ?? string.Empty,
            title: entity.GetString("Title") ?? string.Empty,
            description: entity.GetString("Description"),
            status: Enum.Parse<CatalogStatus>(entity.GetString("Status") ?? nameof(CatalogStatus.Active)),
            tags: TagSet.FromJsonStringOrEmpty(entity.GetString("Tags")),
            owner: new CatalogOwner(
                entity.GetString("OwnerName") ?? string.Empty,
                entity.GetString("OwnerEmail") ?? string.Empty,
                entity.GetString("OwnerTeam"),
                entity.GetString("OwnerUrl")),
            sources: SourceSet.FromJsonStringOrEmpty(entity.GetString("Sources")),
            hash: entity.GetString("Hash") ?? string.Empty,
            createdBy: entity.GetString("CreatedBy") ?? string.Empty,
            createdAt: DateTimeOffset.FromUnixTimeMilliseconds(entity.GetInt64("CreatedAt") ?? 0),
            lastUpdatedBy: entity.GetString("LastUpdatedBy"),
            lastUpdatedAt: entity.GetInt64("LastUpdatedAt") is { } lua ? DateTimeOffset.FromUnixTimeMilliseconds(lua) : null,
            obsoletedBy: entity.GetString("ObsoletedBy"),
            obsoletedAt: entity.GetInt64("ObsoletedAt") is { } oa ? DateTimeOffset.FromUnixTimeMilliseconds(oa) : null,
            runnable: entity.GetBoolean("Runnable") ?? false,
            securityTags: DecodeSecurityTags(entity.GetString("SecurityTags")));

    // Security tags round-trip as a JSON property so a single-row read carries them for the control-plane's
    // authorization check (§14.2); the in-process reach filter below reads the same persisted tags.
    private static string? EncodeSecurityTags(SecurityTagSet tags)
        => tags.ToJsonStringOrNull();

    private static SecurityTagSet DecodeSecurityTags(string? encoded)
        => SecurityTagSet.FromJsonStringOrEmpty(encoded);

    private async ValueTask<ParsedJsonDocument<CatalogVersion>> AddCoreAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        // Assign the next version number safely: find the partition's current max, project + insert with
        // create-if-not-exists (AddEntityAsync) so a racing add cannot reuse a number, and retry on the 409 a
        // collision raises — mirroring the run store's optimistic create-then-retry concurrency handling.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int versionNumber = await this.MaxVersionAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false) + 1;
            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);

            // The returned version is a pooled, disposable document (the caller owns it). It is built once and the
            // Table entity is projected from it; on a 409 retry the document is disposed so the loop cannot leak it.
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

            // Write the package blob first; it is keyed by (base, version) and is overwritten harmlessly on a
            // retry. The Table entity, written with create-if-not-exists, is the authority for the version's
            // existence, so a partial failure before it lands leaves an orphan blob, not a phantom version.
            try
            {
                await this.packages.GetBlobClient(BlobName(baseWorkflowId, versionNumber))
                    .UploadAsync(BinaryData.FromBytes(projection.CanonicalPackage), overwrite: true, cancellationToken)
                    .ConfigureAwait(false);

                await this.catalog.AddEntityAsync(BuildEntity(version.RootElement), cancellationToken).ConfigureAwait(false);
                return version;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                // Another add claimed this version number concurrently — recompute the max and try again.
                version.Dispose();
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
        // RowKey is the D10 version, so the partition's highest RowKey is the highest version. There is no
        // server-side max, so enumerate the partition (descending is not supported); the partition is small.
        int max = 0;
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {baseWorkflowId}");
        await foreach (TableEntity entity in this.catalog
            .QueryAsync<TableEntity>(filter, select: ["RowKey"], cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            int version = ParseRowKey(entity.RowKey);
            if (version > max)
            {
                max = version;
            }
        }

        return max;
    }

    private async ValueTask<byte[]?> LoadPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        BlobClient blob = this.packages.GetBlobClient(BlobName(baseWorkflowId, versionNumber));
        try
        {
            Response<BlobDownloadResult> response = await blob.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // A base workflow's best-matching representative during a distinct-workflow scan: the winning version's Table entity
    // (re-parsed into the pooled batch for the emitted page) plus the fields the precedence compares (lower status rank
    // wins; ties break to the higher version number). Each queried TableEntity is a distinct object, so holding the
    // winner and reading it after the scan is safe.
    private readonly record struct RepCandidate(TableEntity Entity, int Rank, int VersionNumber)
    {
        public bool IsBetterThan(RepCandidate other)
            => this.Rank != other.Rank ? this.Rank < other.Rank : this.VersionNumber > other.VersionNumber;
    }
}