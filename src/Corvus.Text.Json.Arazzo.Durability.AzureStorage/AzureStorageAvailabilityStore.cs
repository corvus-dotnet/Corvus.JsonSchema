// <copyright file="AzureStorageAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow
/// versions are available in which environments) persisted as Table entities. Each entry is one entity holding its
/// <see cref="AvailabilityEntry"/> document in a binary <c>Document</c> property, keyed by a constant PartitionKey and a
/// deterministic RowKey derived from the composite key (baseWorkflowId, versionNumber, environment). AvailabilityEntry has no
/// mutable state and carries no security tags — an entry is created (idempotently) to make a version available and deleted
/// to withdraw it; authorization and readiness are the control-plane surface's concern. Works against Azure Storage and
/// the Azurite emulator.
/// </summary>
/// <remarks>
/// Table storage cannot express the two list axes' ordering natively (its only order is (PartitionKey, RowKey), and the
/// RowKey is URL-safe base64 that is not ordinal-order-preserving). Correctness-first, each <c>List</c> therefore queries
/// the relevant subset on the plain key columns (BaseWorkflowId / VersionNumber / Environment), materializes it, then
/// sorts and keyset-pages it in memory exactly as <see cref="InMemoryAvailabilityStore"/> does. Document bytes are stored
/// and read verbatim — Corvus.Text.Json end to end, no System.Text.Json.
/// </remarks>
public sealed class AzureStorageAvailabilityStore : IAvailabilityStore
{
    private const string AvailabilityTable = "arazzoAvailability";
    private const string Partition = "avail";
    private const string DocumentColumn = "Document";
    private const string BaseWorkflowIdColumn = "BaseWorkflowId";
    private const string VersionNumberColumn = "VersionNumber";
    private const string EnvironmentColumn = "Environment";

    private readonly TableClient availability;
    private readonly TimeProvider timeProvider;

    private AzureStorageAvailabilityStore(TableClient availability, TimeProvider timeProvider)
    {
        this.availability = availability;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the availability table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the availability table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(AvailabilityTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageAvailabilityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageAvailabilityStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageAvailabilityStore>(
            new AzureStorageAvailabilityStore(tableService.GetTableClient(AvailabilityTable), timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);

        // Idempotent: if the version is already available in the environment, return the existing entry unchanged.
        string rowKey = RowKey(baseWorkflowId, versionNumber, environment);
        NullableResponse<TableEntity> existing = await this.availability.GetEntityIfExistsAsync<TableEntity>(
            Partition, rowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (existing.HasValue && existing.Value!.GetBinary(DocumentColumn) is { } existingJson)
        {
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>(existingJson), false);
        }

        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
        byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var entity = new TableEntity(Partition, rowKey)
        {
            [BaseWorkflowIdColumn] = baseWorkflowId,
            [VersionNumberColumn] = versionNumber,
            [EnvironmentColumn] = environment,
            [DocumentColumn] = json,
        };
        await this.availability.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return (PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        NullableResponse<TableEntity> entity = await this.availability.GetEntityIfExistsAsync<TableEntity>(
            Partition, RowKey(baseWorkflowId, versionNumber, environment), cancellationToken: cancellationToken).ConfigureAwait(false);
        return entity.HasValue && entity.Value!.GetBinary(DocumentColumn) is { } json
            ? PersistedJson.ToPooledDocument<AvailabilityEntry>(json)
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);

        // Read-before-delete to report whether the entry actually existed: DeleteEntityAsync with the wildcard ETag does
        // not reliably surface a 404 for a missing entity (the Azurite emulator returns success), so the prior existence
        // check is authoritative — mirroring the sibling source store's DeleteAsync.
        string rowKey = RowKey(baseWorkflowId, versionNumber, environment);
        NullableResponse<TableEntity> existing = await this.availability.GetEntityIfExistsAsync<TableEntity>(
            Partition, rowKey, select: [DocumentColumn], cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!existing.HasValue)
        {
            return false;
        }

        await this.availability.DeleteEntityAsync(Partition, rowKey, ETag.All, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Rows for this exact version, ordered by environment (the only varying key part on this axis). Table queries are
        // unordered, so the candidate set is pulled on the plain key columns and sorted/paged in memory. The column names
        // are literal format text, not interpolation holes: CreateQueryFilter quotes every hole, so a column-name hole
        // would emit 'BaseWorkflowId' eq 'checkout' (literal vs literal, never matching). Only the values are holes.
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {Partition} and BaseWorkflowId eq {baseWorkflowId} and VersionNumber eq {versionNumber}");
        List<Row> rows = await this.QueryRowsAsync(filter, cancellationToken).ConfigureAwait(false);
        rows.Sort(static (x, y) => string.CompareOrdinal(x.Environment, y.Environment));
        return BuildPage(rows, pageSize, hasCursor, static (row, cursor) => string.CompareOrdinal(row.Environment, cursor.Environment), cursor);
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        // Rows for this environment, ordered by base workflow id then version number. Pulled on the plain key columns and
        // sorted/paged in memory, since Table storage cannot express this order natively. The column names are literal
        // format text, not interpolation holes (CreateQueryFilter quotes every hole); only the values are holes.
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {Partition} and Environment eq {environment}");
        List<Row> rows = await this.QueryRowsAsync(filter, cancellationToken).ConfigureAwait(false);
        rows.Sort(static (x, y) => CompareWorkflowVersion(x.BaseWorkflowId, x.VersionNumber, y.BaseWorkflowId, y.VersionNumber));
        return BuildPage(rows, pageSize, hasCursor, static (row, cursor) => CompareWorkflowVersion(row.BaseWorkflowId, row.VersionNumber, cursor.BaseWorkflowId, cursor.VersionNumber), cursor);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The by-environment total order: base workflow id (ordinal), then version number (numeric). Mirrors
    // InMemoryAvailabilityStore.CompareWorkflowVersion.
    private static int CompareWorkflowVersion(string b1, int v1, string b2, int v2)
    {
        int c = string.CompareOrdinal(b1, b2);
        return c != 0 ? c : v1.CompareTo(v2);
    }

    private static bool TryDecodeCursor(JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    // Scans the sorted rows past the cursor (per the axis comparer), takes a page, and emits a token from the last
    // included row's full key when more rows remain. Each page row is parsed into a pooled document the caller owns.
    private static AvailabilityPage BuildPage(
        List<Row> sorted,
        int pageSize,
        bool hasCursor,
        Func<(string BaseWorkflowId, int VersionNumber, string Environment), (string BaseWorkflowId, int VersionNumber, string Environment), int> compareToCursor,
        (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        var docs = new PooledDocumentList<AvailabilityEntry>(Math.Min(pageSize, sorted.Count));
        bool hasMore = false;
        string lastBaseWorkflowId = string.Empty, lastEnvironment = string.Empty;
        int lastVersionNumber = 0;
        try
        {
            foreach (Row row in sorted)
            {
                if (hasCursor && compareToCursor((row.BaseWorkflowId, row.VersionNumber, row.Environment), cursor) <= 0)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }

                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(row.Json));
                lastBaseWorkflowId = row.BaseWorkflowId;
                lastVersionNumber = row.VersionNumber;
                lastEnvironment = row.Environment;
            }

            return hasMore
                ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, lastEnvironment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    // The RowKey is a deterministic, collision-free derivation of the composite key. The three parts are joined on a
    // U+0000 control byte (which cannot appear in a key part) and the result is URL-safe-base64 encoded so the RowKey
    // holds only characters Table storage permits. A leading '~' guarantees a non-empty key. The plain key parts are kept
    // as their own entity columns for the list queries/sort, so the RowKey itself never needs decoding.
    private static string RowKey(string baseWorkflowId, int versionNumber, string environment)
    {
        string raw = string.Create(
            CultureInfo.InvariantCulture,
            $"{baseWorkflowId}\0{versionNumber}\0{environment}");
        return "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).Replace('/', '_').Replace('+', '-');
    }

    // Pulls the candidate entities for a list axis — only the plain key columns plus the Document bytes — into rows the
    // sort/page step orders in memory.
    private async ValueTask<List<Row>> QueryRowsAsync(string filter, CancellationToken cancellationToken)
    {
        var rows = new List<Row>();
        await foreach (TableEntity entity in this.availability.QueryAsync<TableEntity>(
            filter,
            select: [BaseWorkflowIdColumn, VersionNumberColumn, EnvironmentColumn, DocumentColumn],
            cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(BaseWorkflowIdColumn) is not { } baseWorkflowId ||
                entity.GetString(EnvironmentColumn) is not { } environment ||
                entity.GetInt32(VersionNumberColumn) is not { } versionNumber ||
                entity.GetBinary(DocumentColumn) is not { } json)
            {
                continue;
            }

            rows.Add(new Row(baseWorkflowId, versionNumber, environment, json));
        }

        return rows;
    }

    // A materialized list candidate: the decoded plain key parts (used to sort/page into the contractual total order) and
    // the entry's document bytes (parsed lazily, only for rows that make the page).
    private readonly record struct Row(string BaseWorkflowId, int VersionNumber, string Environment, byte[] Json);
}