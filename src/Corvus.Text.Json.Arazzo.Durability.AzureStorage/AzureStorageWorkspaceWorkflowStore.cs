// <copyright file="AzureStorageWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working
/// copies persisted as Table entities so a working copy survives a restart. Each working copy is one entity holding its
/// <see cref="WorkspaceWorkflow"/> document in a binary <c>Doc</c> property, keyed by its server-minted <c>id</c> alone
/// (globally unique): PartitionKey = the (encoded) id and a single fixed RowKey, so each working copy is a one-entity
/// partition and Get/Update/Delete are a partition-scoped point read. Its etag travels inside the document (independent
/// of the Table entity ETag), so optimistic concurrency is a read-compare-write. Works against Azure Storage and the
/// Azurite emulator.
/// </summary>
/// <remarks>
/// Reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory over the
/// single row for an id (a working copy outside reach is reported as absent, non-disclosing), and per row in keyset
/// order for the list. Table queries are unordered, so <see cref="ListAsync"/> sorts its snapshot client-side by id (the
/// id is globally unique, so it is the whole total order and needs no tie-breaker) to match every other backend's
/// ordering. The document is carried Corvus.Text.Json end to end (no System.Text.Json): the working-copy bytes are
/// stored and read verbatim (#803) via <see cref="ParsedJsonDocument{T}"/> and the shared pooled serialization, never a
/// per-op detached clone.
/// </remarks>
public sealed class AzureStorageWorkspaceWorkflowStore : IWorkspaceWorkflowStore
{
    private const string WorkspaceWorkflowsTable = "arazzoWorkspaceWorkflows";
    private const string DocColumn = "Doc";
    private const string IdColumn = "Id";

    // A working copy is keyed by its globally-unique id alone, so each id owns a one-entity partition
    // (PartitionKey = Enc(id)) and this fixed RowKey is the sole row within it. Table storage forbids an empty RowKey, so
    // it is a constant non-empty literal.
    private const string SingleRow = "workingCopy";

    private readonly TableClient workingCopies;
    private readonly TimeProvider timeProvider;

    private AzureStorageWorkspaceWorkflowStore(TableClient workingCopies, TimeProvider timeProvider)
    {
        this.workingCopies = workingCopies;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the working-copies table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the working-copies table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(WorkspaceWorkflowsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkspaceWorkflowStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkspaceWorkflowStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageWorkspaceWorkflowStore>(
            new AzureStorageWorkspaceWorkflowStore(tableService.GetTableClient(WorkspaceWorkflowsTable), timeProvider ?? TimeProvider.System));
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
        var entity = new TableEntity(PartitionKey(id), SingleRow)
        {
            [IdColumn] = id,
            [DocColumn] = json,
        };
        await this.workingCopies.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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
        // is empty). Table storage orders by (PartitionKey, RowKey) = (Enc(id), fixed), but Enc is URL-safe base64 and
        // therefore NOT ordinal-order-preserving, so the keyset cannot be pushed as a server-side range filter. Instead
        // the plain ids (the Id column) are pulled, sorted in memory into the total order, paged, and only the page's
        // documents are fetched.
        var ids = new List<string>();
        await foreach (TableEntity entity in this.workingCopies.QueryAsync<TableEntity>(
            select: [IdColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(IdColumn) is { } id)
            {
                ids.Add(id);
            }
        }

        ids.Sort(static (a, b) => string.CompareOrdinal(a, b));

        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            string lastId = string.Empty;
            foreach (string id in ids)
            {
                // Skip ids at or before the cursor in id order.
                if (hasCursor && string.CompareOrdinal(id, cursor.Id) <= 0)
                {
                    continue;
                }

                // Fetch the document only now, for ids past the cursor, and only until the page fills plus one.
                TableEntity entity = (await this.workingCopies.GetEntityAsync<TableEntity>(
                    PartitionKey(id), SingleRow, [DocColumn], cancellationToken).ConfigureAwait(false)).Value;
                if (entity.GetBinary(DocColumn) is not { } json)
                {
                    continue;
                }

                ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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
                        break;
                    }

                    docs.Add(cand);
                    kept = true;
                    lastId = id;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
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
        // immutable id/provenance/tags/audit forward bytes-to-bytes; the key is unchanged, so the entity is replaced.
        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var entity = new TableEntity(PartitionKey(id), SingleRow)
        {
            [IdColumn] = id,
            [DocColumn] = json,
        };
        await this.workingCopies.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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

        await this.workingCopies.DeleteEntityAsync(PartitionKey(id), SingleRow, ETag.All, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The PartitionKey is the working-copy id. It is a server-minted `wc-<guid>` (already Table-key-safe), but it is
    // URL-safe-base64 encoded so the key layout matches the other id-keyed backends and stays robust to any future id
    // shape.
    private static string PartitionKey(string id) => Enc(id);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and those
    // two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key.
    private static string Enc(string value)
        => "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the sole key, so its partition holds at most the one row). A working copy outside reach is invisible
    // (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        // The column name PartitionKey must be LITERAL text in the filter; only the value is an interpolation hole.
        // TableClient.CreateQueryFilter quotes every hole, so a hole for the column name would emit 'PartitionKey' eq '…'
        // (a quoted literal, not a property reference) — an invalid query condition.
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey(id)}");
        await foreach (TableEntity entity in this.workingCopies.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocColumn) is not { } json)
            {
                continue;
            }

            using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
            return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
        }

        return null;
    }
}