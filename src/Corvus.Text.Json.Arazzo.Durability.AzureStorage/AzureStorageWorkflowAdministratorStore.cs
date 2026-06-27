// <copyright file="AzureStorageWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration
/// record for a base workflow id — the mutable set of administrator identities entitled to publish further versions and
/// to manage administration. Each record is one Table entity holding its <see cref="WorkflowAdministrators"/> document
/// in a binary <c>Document</c> property, keyed solely by the (encoded) base workflow id, with a constant PartitionKey so
/// every record lives in a single partition. Its etag travels inside the document (independent of the Table entity
/// ETag), so optimistic concurrency is a read-compare-write. The record holds deployment-stamped identities only — never
/// secret material. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// The store takes no <see cref="AccessContext"/>: it is a CAS key/value persistence seam (like the security-policy and
/// source-credential stores), with authorization the <see cref="SecuredWorkflowCatalog"/>'s concern. The
/// <see cref="PutAsync"/> create-or-replace reads the current document and compares its (in-document) etag before
/// writing, mirroring every other backend; a mismatch — or a present-vs-expected-absent record (and vice versa) —
/// surfaces as <see cref="WorkflowAdministrationConflictException"/>. Tag round-tripping is Corvus.Text.Json end to end
/// (no System.Text.Json): the record bytes are stored and read back verbatim.
/// </remarks>
public sealed class AzureStorageWorkflowAdministratorStore : IWorkflowAdministratorStore
{
    private const string AdministratorsTable = "arazzoWorkflowAdministrators";

    // The reverse administration index (design §15.4): a separate table whose PartitionKey is an administrator digest and
    // whose RowKey is the encoded base id, with the plain base id in a column so a digest's administered base ids are a
    // single-partition query (no RowKey decode). Azure Table has no server-side ORDER BY, so the (bounded) partition is
    // ordered client-side — mirroring the access-request store's keyset approach.
    private const string IndexTable = "arazzoWorkflowAdministratorIndex";
    private const string DocumentColumn = "Document";
    private const string EtagColumn = "Etag";
    private const string BaseWorkflowIdColumn = "BaseWorkflowId";

    // A single logical entity per base workflow id: the partition is constant (one partition for the table) and the
    // row is the encoded base id, so a record is a point read by (PartitionKey, RowKey).
    private const string AdministratorsPartition = "admin";

    private readonly TableClient administrators;
    private readonly TableClient index;
    private readonly TimeProvider timeProvider;

    private AzureStorageWorkflowAdministratorStore(TableClient administrators, TableClient index, TimeProvider timeProvider)
    {
        this.administrators = administrators;
        this.index = index;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the workflow-administrators table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the workflow-administrators table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(AdministratorsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(IndexTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkflowAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkflowAdministratorStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageWorkflowAdministratorStore>(
            new AzureStorageWorkflowAdministratorStore(
                tableService.GetTableClient(AdministratorsTable),
                tableService.GetTableClient(IndexTable),
                timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        byte[]? json = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParsedJsonDocument<WorkflowAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();
        byte[] json;

        // The base id's previous administrator digests (so stale reverse-index rows can be retracted): read from the
        // record on disk, since Table storage has no atomic server-side script holding them. Empty for a fresh record.
        IReadOnlyList<string> oldDigests;
        if (existing is not null)
        {
            // Parse the existing record ONCE, NON-COPYING over the driver's array (the read leaf) — used for both the etag
            // check and the carried-forward merge. Azure Table stores the document in a binary column, so the write stays a
            // byte[] leaf; the column etag is the one we just generated.
            using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existing.AsMemory());

            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            oldDigests = WorkflowAdministeredPaging.DistinctDigests(current.RootElement);
            json = WorkflowAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            oldDigests = [];
            json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        var entity = new TableEntity(AdministratorsPartition, RowKey(baseWorkflowId))
        {
            [EtagColumn] = etag.Value!,
            [DocumentColumn] = json,
        };
        await this.administrators.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.ReindexAsync(baseWorkflowId, oldDigests, WorkflowAdministeredPaging.DistinctDigests(administrators), cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : WorkflowAdministeredPage.DefaultPageSize;
        string? after = WorkflowAdministeredContinuationToken.DecodeCursorToString(pageToken);

        // The digest's partition (the workflows it administers) is bounded; fetch it projecting just the plain base id,
        // order client-side (ordinal — the contract's order), then apply the keyset cursor + page (Azure has no ORDER BY).
        var ids = new List<string>();
        await foreach (TableEntity entity in this.index.QueryAsync<TableEntity>(
            e => e.PartitionKey == adminDigest, select: [BaseWorkflowIdColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(BaseWorkflowIdColumn) is { } baseWorkflowId)
            {
                ids.Add(baseWorkflowId);
            }
        }

        ids.Sort(StringComparer.Ordinal);

        var rows = new List<string>(Math.Min(pageSize + 1, ids.Count));
        foreach (string id in ids)
        {
            if (after is not null && string.CompareOrdinal(id, after) <= 0)
            {
                continue; // at or before the cursor — already returned on an earlier page
            }

            rows.Add(id);
            if (rows.Count > pageSize)
            {
                break;
            }
        }

        return WorkflowAdministeredPaging.ToPage(rows, pageSize);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Reconciles a base id's reverse-index rows (§15.4): delete the rows for digests it no longer administers, then
    // (idempotently) upsert a row for each current digest. Not atomic with the document write (Table storage spans
    // partitions here), but a stale row only ever over-reports until the next write, and the work is bounded by the
    // small administrator set.
    private async ValueTask ReindexAsync(string baseWorkflowId, IReadOnlyList<string> oldDigests, IReadOnlyList<string> newDigests, CancellationToken cancellationToken)
    {
        string rowKey = RowKey(baseWorkflowId);
        foreach (string digest in oldDigests)
        {
            if (!newDigests.Contains(digest))
            {
                try
                {
                    await this.index.DeleteEntityAsync(digest, rowKey, ETag.All, cancellationToken).ConfigureAwait(false);
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Already gone — the reverse index is idempotent.
                }
            }
        }

        foreach (string digest in newDigests)
        {
            var entity = new TableEntity(digest, rowKey)
            {
                [BaseWorkflowIdColumn] = baseWorkflowId,
            };
            await this.index.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
    }

    // The RowKey is the base workflow id, which may contain Table-forbidden characters (/\#? and control chars), so it
    // is URL-safe-base64 encoded; the encoded form is always a permitted Table key (see Enc).
    private static string RowKey(string baseWorkflowId) => Enc(baseWorkflowId);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and those
    // two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key even for the empty
    // string, which Table storage forbids as a key.
    private static string Enc(string value)
        => "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // The record is a point read by (PartitionKey, RowKey); a missing entity is reported by the 404 status code rather
    // than by a thrown exception leaking out (NoThrow), and surfaces as a null document to the caller.
    private async ValueTask<byte[]?> ReadDocumentAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> response = await this.administrators
            .GetEntityIfExistsAsync<TableEntity>(AdministratorsPartition, RowKey(baseWorkflowId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue ? response.Value!.GetBinary(DocumentColumn) : null;
    }
}