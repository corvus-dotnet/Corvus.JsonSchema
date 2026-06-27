// <copyright file="AzureStorageAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5): a principal's
/// request for elevated capability on a workflow, its decision state, and audit metadata. Each request is one Table
/// entity holding its <see cref="AccessRequest"/> schema document in a binary <c>Doc</c> property, keyed by the
/// (encoded) request id under a constant partition so a record is a point read by (PartitionKey, RowKey). The
/// filterable fields (status, target workflow, subject, creation instant) are mirrored into entity columns so
/// <see cref="ListAsync(AccessRequestQuery, CancellationToken)"/> can apply a server-side filter; ordering is client-side
/// (oldest first) because Table queries are unordered. The etag travels inside the document, so optimistic concurrency on a decision is a
/// read-compare-write. Works against Azure Storage and the Azurite emulator.
/// </summary>
public sealed class AzureStorageAccessRequestStore : IAccessRequestStore
{
    private const string RequestsTable = "arazzoAccessRequests";
    private const string RequestPartition = "request";
    private const string DocumentColumn = "Doc";
    private const string BaseWorkflowIdColumn = "BaseWorkflowId";
    private const string SubjectClaimTypeColumn = "SubjectClaimType";
    private const string SubjectClaimValueColumn = "SubjectClaimValue";
    private const string StatusColumn = "Status";
    private const string CreatedAtColumn = "CreatedAt";

    // The shared client-side ordering for ListAsync: oldest first by creation instant, then id (Table queries are
    // unordered, so the snapshot is sorted after the (filtered) read).
    private static readonly IComparer<ParsedJsonDocument<AccessRequest>> OldestFirst =
        Comparer<ParsedJsonDocument<AccessRequest>>.Create(static (a, b) =>
        {
            int byCreated = a.RootElement.CreatedAtValue.CompareTo(b.RootElement.CreatedAtValue);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.RootElement.IdValue, b.RootElement.IdValue);
        });

    private readonly TableClient requests;
    private readonly TimeProvider timeProvider;

    private AzureStorageAccessRequestStore(TableClient requests, TimeProvider timeProvider)
    {
        this.requests = requests;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the access-requests table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the access-requests table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(RequestsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageAccessRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageAccessRequestStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageAccessRequestStore>(
            new AzureStorageAccessRequestStore(tableService.GetTableClient(RequestsTable), timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, now, etag);
        var entity = new TableEntity(RequestPartition, Enc(id))
        {
            [BaseWorkflowIdColumn] = draft.BaseWorkflowIdValue,
            [SubjectClaimTypeColumn] = draft.SubjectClaimTypeValue,
            [SubjectClaimValueColumn] = draft.SubjectClaimValueValue,
            [StatusColumn] = AccessRequestStatusNames.Pending,
            [CreatedAtColumn] = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            [DocumentColumn] = json,
        };
        await this.requests.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<AccessRequest>();
        string? filter = BuildFilter(query);
        await foreach (TableEntity entity in this.requests.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocumentColumn) is { } bytes)
            {
                list.Add(ParsedJsonDocument<AccessRequest>.Parse(bytes.AsMemory()));
            }
        }

        list.Sort(OldestFirst);
        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<AccessRequestPage> ListAsync(AccessRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AccessRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to strings (the leaf) only here — createdAt as the ISO-8601 "o"
        // form the CreatedAt column stores (reconstructed from the token's UTC ticks), id as text. Undefined = first page.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AccessRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (AccessRequestContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorCreatedAt = new DateTime(cursorTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    cursorId = Encoding.UTF8.GetString(cursorIdUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // Table Storage has no server-side ORDER BY and the RowKey is Base64(id) (not the (createdAt, id) keyset order), so
        // the order is materialised client-side — but over a PROJECTION of just the keyset fields (RowKey + CreatedAt, no
        // Doc), with the filter applied server-side (those are entity columns). Only the page's documents are then point-read
        // — never every request's Doc. createdAt is the fixed-width ISO "o" form (ordinal == chronological) and id is
        // recovered from RowKey so the compare is byte-ordinal == the in-memory pager's.
        string? filter = BuildFilter(query);
        var keys = new List<(string CreatedAt, string Id)>();
        await foreach (TableEntity entity in this.requests
            .QueryAsync<TableEntity>(filter, select: ["RowKey", CreatedAtColumn], cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (entity.GetString(CreatedAtColumn) is { } createdAt)
            {
                keys.Add((createdAt, Dec(entity.RowKey)));
            }
        }

        keys.Sort(static (a, b) =>
        {
            int byCreated = string.CompareOrdinal(a.CreatedAt, b.CreatedAt);
            return byCreated != 0 ? byCreated : string.CompareOrdinal(a.Id, b.Id);
        });

        // Keyset skip past the cursor, then take one id beyond the page (lookahead) — all in memory over the projection.
        var pageIds = new List<string>(pageSize + 1);
        foreach ((string createdAt, string id) in keys)
        {
            if (cursorCreatedAt is not null)
            {
                int byCreated = string.CompareOrdinal(createdAt, cursorCreatedAt);
                bool after = byCreated > 0 || (byCreated == 0 && string.CompareOrdinal(id, cursorId) > 0);
                if (!after)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }
            }

            pageIds.Add(id);
            if (pageIds.Count > pageSize)
            {
                break; // one id beyond the page → a next page exists
            }
        }

        bool hasMore = pageIds.Count > pageSize;
        int take = hasMore ? pageSize : pageIds.Count;
        var page = new PooledDocumentList<AccessRequest>(take);
        try
        {
            for (int i = 0; i < take; i++)
            {
                byte[]? doc = await this.DocumentAsync(pageIds[i], cancellationToken).ConfigureAwait(false);
                if (doc is not null)
                {
                    page.Add(ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory()));
                }
            }

            if (!hasMore || page.Count == 0)
            {
                return AccessRequestPage.Create(page);
            }

            AccessRequest last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return AccessRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
        byte[] json = AccessRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);

        // The immutable filterable columns (workflow/subject/createdAt) carry through from the loaded document so the
        // replaced entity keeps them; only Status and Doc change on a decision.
        var entity = new TableEntity(RequestPartition, Enc(id))
        {
            [StatusColumn] = AccessRequestStatusNames.ToWire(decision.Status),
            [DocumentColumn] = json,
            [BaseWorkflowIdColumn] = current.RootElement.BaseWorkflowIdValue,
            [SubjectClaimTypeColumn] = current.RootElement.SubjectClaimTypeValue,
            [SubjectClaimValueColumn] = current.RootElement.SubjectClaimValueValue,
            [CreatedAtColumn] = current.RootElement.CreatedAtValue.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
        };

        await this.requests.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static string Enc(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // The inverse of Enc: recovers a request id from its Base64 RowKey so the keyset order can be computed from the
    // projection without reading documents. Only ever applied to RowKeys this store wrote (round-trips exactly).
    private static string Dec(string rowKey)
        => Encoding.UTF8.GetString(Convert.FromBase64String(rowKey.Replace('_', '/').Replace('-', '+')));

    // Builds the OData filter for the optional query criteria (an absent criterion matches anything); null when the
    // query is empty so the read is an unfiltered scan of the single partition.
    private static string? BuildFilter(AccessRequestQuery query)
    {
        // The column name must be a LITERAL part of the OData filter; only the value is an interpolation hole.
        // TableClient.CreateQueryFilter quotes every hole, so a hole for the column name would emit 'Status' eq 'x'
        // (a quoted literal, not a property reference) — an invalid query condition.
        var conditions = new List<string>(4);
        if (query.Status is { } status)
        {
            conditions.Add(TableClient.CreateQueryFilter($"Status eq {AccessRequestStatusNames.ToWire(status)}"));
        }

        if (query.BaseWorkflowId.IsNotUndefined())
        {
            conditions.Add(TableClient.CreateQueryFilter($"BaseWorkflowId eq {(string)query.BaseWorkflowId}"));
        }

        if (query.SubjectClaimType is { } subjectType)
        {
            conditions.Add(TableClient.CreateQueryFilter($"SubjectClaimType eq {subjectType}"));
        }

        if (query.SubjectClaimValue is { } subjectValue)
        {
            conditions.Add(TableClient.CreateQueryFilter($"SubjectClaimValue eq {subjectValue}"));
        }

        if (query.AdministeredBaseWorkflowIds is { Count: > 0 } administered)
        {
            // The approver inbox (§16.5): BaseWorkflowId IN (the administered set) — Azure Table has no IN, so it is an OR
            // group over the server-derived base ids (each value quoted by CreateQueryFilter). The set is never empty here
            // (the handler short-circuits a caller who administers nothing to an empty page before the store).
            var anyOf = new List<string>(administered.Count);
            foreach (string baseWorkflowId in administered)
            {
                anyOf.Add(TableClient.CreateQueryFilter($"BaseWorkflowId eq {baseWorkflowId}"));
            }

            conditions.Add("(" + string.Join(" or ", anyOf) + ")");
        }

        return conditions.Count == 0 ? null : string.Join(" and ", conditions);
    }

    // The record is a point read by (PartitionKey, RowKey); a missing entity surfaces as a null document to the caller.
    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> response = await this.requests
            .GetEntityIfExistsAsync<TableEntity>(RequestPartition, Enc(id), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue ? response.Value!.GetBinary(DocumentColumn) : null;
    }
}