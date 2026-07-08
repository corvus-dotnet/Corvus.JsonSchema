// <copyright file="AzureStorageWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Storage-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working
/// copies persisted so a working copy survives a restart, keyed by its server-minted <c>id</c> alone (globally unique).
/// A working copy's whole <see cref="WorkspaceWorkflow"/> document is stored as a BLOB (blob name = the id), because an
/// Arazzo document plus its attached sources routinely exceeds the 64 KB Azure Table per-property limit — the same
/// document-in-blob layout the catalog and draft-run stores use. A companion Table entity is the queryable index: one
/// entity per working copy (PartitionKey = the encoded id, a single fixed RowKey) carrying the plain id (for the keyset
/// list order) and the working copy's management tags (for the §14.2 reach filter), so a list reach-filters from the
/// Table and fetches only the page's blobs, never every document. The document's etag travels inside the document, so
/// optimistic concurrency is a read-compare-write. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// Reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — a working copy outside reach is
/// reported as absent (non-disclosing). Get/Update/Delete read the blob and reach-check the document's own tags; the
/// list reach-checks the Table's tags column first, then fetches the admitted page's blobs. Table queries are unordered,
/// so <see cref="ListAsync"/> sorts its id snapshot client-side (the id is globally unique, so it is the whole total
/// order and needs no tie-breaker) to match every other backend. The document is carried Corvus.Text.Json end to end
/// (no System.Text.Json): the working-copy bytes are stored and read verbatim (#803) via <see cref="ParsedJsonDocument{T}"/>
/// and the shared pooled serialization, never a per-op detached clone.
/// </remarks>
public sealed class AzureStorageWorkspaceWorkflowStore : IWorkspaceWorkflowStore
{
    private const string WorkspaceWorkflowsTable = "arazzoWorkspaceWorkflows";
    private const string WorkspaceWorkflowsContainer = "arazzo-workspace-workflows";
    private const BlobClientOptions.ServiceVersion BlobApiVersion = BlobClientOptions.ServiceVersion.V2024_11_04;
    private const string IdColumn = "Id";
    private const string TagsColumn = "Tags";

    // A working copy is keyed by its globally-unique id alone, so each id owns a one-entity partition
    // (PartitionKey = Enc(id)) and this fixed RowKey is the sole row within it. Table storage forbids an empty RowKey, so
    // it is a constant non-empty literal.
    private const string SingleRow = "workingCopy";

    private readonly BlobContainerClient documents;
    private readonly TableClient index;
    private readonly TimeProvider timeProvider;

    private AzureStorageWorkspaceWorkflowStore(BlobContainerClient documents, TableClient index, TimeProvider timeProvider)
    {
        this.documents = documents;
        this.index = index;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the working-copies blob container and index table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create the container and table.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container and table exist (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(
            new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)),
            new TableServiceClient(connectionString),
            cancellationToken);
    }

    /// <summary>Provisions the working-copies blob container and index table over caller-supplied service clients.</summary>
    /// <remarks>Container/table creation is a broader right than the per-blob / per-entity data access the store needs at
    /// runtime, so run this once at deploy/migration time, separately from the least-privileged data-plane credential
    /// used to <see cref="ConnectAsync(BlobServiceClient, TableServiceClient, TimeProvider?, CancellationToken)"/>.</remarks>
    /// <param name="blobService">A blob service client (for example one built with a managed identity).</param>
    /// <param name="tableService">A table service client for the same account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container and table exist (idempotent).</returns>
    public static async ValueTask PrepareAsync(BlobServiceClient blobService, TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(tableService);
        await blobService.GetBlobContainerClient(WorkspaceWorkflowsContainer).CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(WorkspaceWorkflowsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned container and table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkspaceWorkflowStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(
            new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion)),
            new TableServiceClient(connectionString),
            timeProvider,
            cancellationToken);
    }

    /// <summary>Opens the store for operation over caller-supplied service clients.</summary>
    /// <remarks>This creates no container or table, so it is safe to use a least-privileged data-plane credential; call
    /// <see cref="PrepareAsync(BlobServiceClient, TableServiceClient, CancellationToken)"/> once beforehand.</remarks>
    /// <param name="blobService">A blob service client.</param>
    /// <param name="tableService">A table service client for the same account.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkspaceWorkflowStore> ConnectAsync(BlobServiceClient blobService, TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobService);
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageWorkspaceWorkflowStore>(
            new AzureStorageWorkspaceWorkflowStore(
                blobService.GetBlobContainerClient(WorkspaceWorkflowsContainer),
                tableService.GetTableClient(WorkspaceWorkflowsTable),
                timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // The durable backend mints its own opaque, globally-unique id (a fresh GUID never collides, so the add needs no
        // duplicate-key handling and no reach check — a create is authorised by the caller having reached the handler).
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);

        // The document is the source of truth (blob); the Table entity is its queryable index. Write the blob first so a
        // present index row always resolves to a document.
        await this.BlobFor(id).UploadAsync(BinaryData.FromBytes(json), overwrite: true, cancellationToken).ConfigureAwait(false);
        ParsedJsonDocument<WorkspaceWorkflow> result = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        try
        {
            await this.index.AddEntityAsync(IndexEntity(id, result.RootElement.ManagementTagsValue), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            result.Dispose();
            throw;
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? json = await this.FindForManagementAsync(id, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // The contractual order is id ascending (globally unique, so the id is the whole total order and the tie-breaker
        // is empty). The index rows (id + reach tags) are read WITHOUT the documents, sorted in memory into the total
        // order, reach-filtered, and only the page's admitted documents are then fetched from their blobs.
        var rows = new List<(string Id, string Tags)>();
        await foreach (TableEntity entity in this.index.QueryAsync<TableEntity>(
            select: [IdColumn, TagsColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(IdColumn) is { } id)
            {
                rows.Add((id, entity.GetString(TagsColumn) ?? string.Empty));
            }
        }

        rows.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            string lastId = string.Empty;
            foreach ((string id, string tags) in rows)
            {
                // Skip ids at or before the cursor in id order.
                if (hasCursor && string.CompareOrdinal(id, cursor.Id) <= 0)
                {
                    continue;
                }

                // Reach-filter from the index's tags — no blob fetch for a working copy the caller cannot see.
                if (!context.Admits(AccessVerb.Read, SecurityTagSet.FromJsonStringOrEmpty(tags)))
                {
                    continue;
                }

                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                // Fetch the document only now, for an admitted id within the page.
                byte[]? json = await this.LoadDocumentAsync(id, cancellationToken).ConfigureAwait(false);
                if (json is null)
                {
                    continue;
                }

                docs.Add(PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json));
                lastId = id;
            }

            return hasMore
                ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                : WorkspaceWorkflowPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        // SerializeUpdated performs the optimistic-concurrency check (a stale expectedEtag throws) and carries the
        // immutable id/provenance/tags/audit forward bytes-to-bytes; the key is unchanged, so the blob + index are replaced.
        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await this.BlobFor(id).UploadAsync(BinaryData.FromBytes(json), overwrite: true, cancellationToken).ConfigureAwait(false);
        ParsedJsonDocument<WorkspaceWorkflow> result = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        try
        {
            await this.index.UpsertEntityAsync(IndexEntity(id, result.RootElement.ManagementTagsValue), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            result.Dispose();
            throw;
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
        }

        // Delete the index row first (so a list stops reporting it), then the document blob.
        await this.index.DeleteEntityAsync(PartitionKey(id), SingleRow, ETag.All, cancellationToken).ConfigureAwait(false);
        await this.BlobFor(id).DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The index row for a working copy: its plain id (the keyset order) and its management tags (the reach filter),
    // keyed by the encoded id. The document itself lives in the blob, never a Table property (64 KB limit).
    private static TableEntity IndexEntity(string id, SecurityTagSet managementTags) => new(PartitionKey(id), SingleRow)
    {
        [IdColumn] = id,
        [TagsColumn] = managementTags.ToJsonStringOrNull() ?? "[]",
    };

    // The PartitionKey is the encoded working-copy id (URL-safe base64), so the key layout matches the other id-keyed
    // backends and stays robust to any future id shape; the id is a server-minted `wc-<guid>` (already key-safe).
    private static string PartitionKey(string id) => Enc(id);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -), reused as the blob name; the base64
    // alphabet plus those two replacements is valid in both a Table key and a blob name. A leading '~' guarantees a
    // non-empty key.
    private static string Enc(string value)
        => "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    private BlobClient BlobFor(string id) => this.documents.GetBlobClient(Enc(id));

    // Downloads a working copy's document bytes from its blob, or null if the blob is absent (the id does not exist, or a
    // racing delete removed it between the index read and the fetch).
    private async ValueTask<byte[]?> LoadDocumentAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            Response<BlobDownloadResult> response = await this.BlobFor(id).DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return response.Value.Content.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes. The
    // document blob is the source of truth, so this reads it directly and reach-checks the document's own tags; a working
    // copy outside reach (or absent) is invisible (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        byte[]? json = await this.LoadDocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }

        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
    }
}