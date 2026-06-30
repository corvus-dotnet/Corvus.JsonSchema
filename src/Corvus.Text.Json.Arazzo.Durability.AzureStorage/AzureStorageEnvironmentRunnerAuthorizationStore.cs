// <copyright file="AzureStorageEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a
/// deployment environment (design §5.5), its decision state, and audit metadata. Each authorization is one Table entity
/// holding its <see cref="EnvironmentRunnerAuthorization"/> schema document in a binary <c>Doc</c> property, keyed on the
/// composite key by <c>PartitionKey = base64url(environment)</c> and <c>RowKey = base64url(runnerId)</c> so a record is a
/// point read by <c>(PartitionKey, RowKey)</c>. The filterable fields (status) and the plain key parts (environment, runner
/// id) are mirrored into entity columns so the list queries can filter server-side and recover the keyset order; ordering by
/// <c>(environment, runnerId)</c> is materialised client-side because the encoded keys are URL-safe base64 (not
/// ordinal-order-preserving) and Table queries are unordered. The etag travels inside the document, so optimistic concurrency
/// on a decision is a read-compare-write. Works against Azure Storage and the Azurite emulator. Mirrors
/// <see cref="AzureStorageAvailabilityRequestStore"/>, keyed by environment + runner rather than a single id.
/// </summary>
public sealed class AzureStorageEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore
{
    private const string AuthorizationsTable = "arazzoEnvironmentRunnerAuthorizations";
    private const string DocumentColumn = "Doc";
    private const string EnvironmentColumn = "Environment";
    private const string RunnerIdColumn = "RunnerId";
    private const string StatusColumn = "Status";

    // The shared client-side ordering for ListAsync: (environment, runnerId) ordinal (Table queries are unordered and the
    // keys are URL-safe base64, not ordinal-order-preserving, so the snapshot is sorted after the (filtered) read).
    private static readonly IComparer<ParsedJsonDocument<EnvironmentRunnerAuthorization>> ByEnvironmentThenRunner =
        Comparer<ParsedJsonDocument<EnvironmentRunnerAuthorization>>.Create(static (a, b) =>
        {
            // String-free ordinal compare over the JSON values' UTF-8 (a view for unescaped values, so no per-comparison
            // string is realised); byte order also matches the SQL backends' COLLATE "C"/binary ordering.
            using UnescapedUtf8JsonString ae = a.RootElement.Environment.GetUtf8String();
            using UnescapedUtf8JsonString be = b.RootElement.Environment.GetUtf8String();
            int byEnvironment = ae.Span.SequenceCompareTo(be.Span);
            if (byEnvironment != 0)
            {
                return byEnvironment;
            }

            using UnescapedUtf8JsonString ar = a.RootElement.RunnerId.GetUtf8String();
            using UnescapedUtf8JsonString br = b.RootElement.RunnerId.GetUtf8String();
            return ar.Span.SequenceCompareTo(br.Span);
        });

    private readonly TableClient authorizations;
    private readonly TimeProvider timeProvider;

    private AzureStorageEnvironmentRunnerAuthorizationStore(TableClient authorizations, TimeProvider timeProvider)
    {
        this.authorizations = authorizations;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the runner-authorizations table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the runner-authorizations table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(AuthorizationsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageEnvironmentRunnerAuthorizationStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageEnvironmentRunnerAuthorizationStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageEnvironmentRunnerAuthorizationStore>(
            new AzureStorageEnvironmentRunnerAuthorizationStore(tableService.GetTableClient(AuthorizationsTable), timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runnerId);
        ArgumentNullException.ThrowIfNull(actor);

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending) — return the existing entity unchanged.
        byte[]? existing = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), etag);
        var entity = new TableEntity(Enc(environment), Enc(runnerId))
        {
            [EnvironmentColumn] = environment,
            [RunnerIdColumn] = runnerId,
            [StatusColumn] = RunnerAuthorizationStatusNames.Pending,
            [DocumentColumn] = json,
        };
        await this.authorizations.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        byte[]? doc = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        try
        {
            string? filter = BuildFilter(query);
            await foreach (TableEntity entity in this.authorizations.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (entity.GetBinary(DocumentColumn) is { } bytes)
                {
                    list.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(bytes));
                }
            }

            // Table queries are unordered and the keys are URL-safe base64 (not ordinal-order-preserving), so the result
            // order — (environment, runnerId) ordinal — is materialised client-side.
            list.Sort(ByEnvironmentThenRunner);
            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to the strings the in-memory keyset compare needs only
        // here. Undefined token = first page; a malformed token throws FormatException.
        string? cursorEnvironment = null;
        string? cursorRunnerId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(EnvironmentRunnerAuthorizationContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (EnvironmentRunnerAuthorizationContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> cursorEnvUtf8, out ReadOnlySpan<byte> cursorRunnerUtf8))
                {
                    cursorEnvironment = Encoding.UTF8.GetString(cursorEnvUtf8);
                    cursorRunnerId = Encoding.UTF8.GetString(cursorRunnerUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        // Table Storage has no server-side ORDER BY and the keys are URL-safe base64 (not the (environment, runnerId)
        // keyset order), so the order is materialised client-side — but over a PROJECTION of just the keyset fields
        // (Environment + RunnerId, no Doc), with the filter applied server-side (those are entity columns). Only the page's
        // documents are then point-read — never every authorization's Doc. The compare is ordinal == the in-memory pager's.
        string? filter = BuildFilter(query);
        var keys = new List<(string Environment, string RunnerId)>();
        await foreach (TableEntity entity in this.authorizations
            .QueryAsync<TableEntity>(filter, select: [EnvironmentColumn, RunnerIdColumn], cancellationToken: cancellationToken)
            .ConfigureAwait(false))
        {
            if (entity.GetString(EnvironmentColumn) is { } environment && entity.GetString(RunnerIdColumn) is { } runnerId)
            {
                keys.Add((environment, runnerId));
            }
        }

        keys.Sort(static (a, b) =>
        {
            int byEnvironment = string.CompareOrdinal(a.Environment, b.Environment);
            return byEnvironment != 0 ? byEnvironment : string.CompareOrdinal(a.RunnerId, b.RunnerId);
        });

        // Keyset skip past the cursor, then take one key beyond the page (lookahead) — all in memory over the projection.
        var pageKeys = new List<(string Environment, string RunnerId)>(pageSize + 1);
        foreach ((string environment, string runnerId) in keys)
        {
            if (cursorEnvironment is not null)
            {
                int byEnvironment = string.CompareOrdinal(environment, cursorEnvironment);
                bool after = byEnvironment > 0 || (byEnvironment == 0 && string.CompareOrdinal(runnerId, cursorRunnerId) > 0);
                if (!after)
                {
                    continue; // at or before the cursor — already returned in an earlier page
                }
            }

            pageKeys.Add((environment, runnerId));
            if (pageKeys.Count > pageSize)
            {
                break; // one key beyond the page → a next page exists
            }
        }

        bool hasMore = pageKeys.Count > pageSize;
        int take = hasMore ? pageSize : pageKeys.Count;
        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(take);
        try
        {
            for (int i = 0; i < take; i++)
            {
                byte[]? doc = await this.DocumentAsync(pageKeys[i].Environment, pageKeys[i].RunnerId, cancellationToken).ConfigureAwait(false);
                if (doc is not null)
                {
                    page.Add(PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc));
                }
            }

            if (!hasMore || page.Count == 0)
            {
                return EnvironmentRunnerAuthorizationPage.Create(page);
            }

            EnvironmentRunnerAuthorization last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastEnv = last.Environment.GetUtf8String();
            using UnescapedUtf8JsonString lastRunner = last.RunnerId.GetUtf8String();
            return EnvironmentRunnerAuthorizationPage.Create(page, lastEnv.Span, lastRunner.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        byte[]? doc = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(doc.AsMemory());
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), etag);

        // The plain key columns (environment / runner id) carry through from the loaded document so the replaced entity keeps
        // them; only Status and Doc change on a decision.
        var entity = new TableEntity(Enc(environment), Enc(runnerId))
        {
            [EnvironmentColumn] = current.RootElement.EnvironmentValue,
            [RunnerIdColumn] = current.RootElement.RunnerIdValue,
            [StatusColumn] = RunnerAuthorizationStatusNames.ToWire(decision.Status),
            [DocumentColumn] = json,
        };

        await this.authorizations.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // A key part is encoded URL-safe-base64 so PartitionKey/RowKey hold only characters Table storage permits (the raw
    // environment / runner id may contain characters — '/', '\', '#', '?', control bytes — that keys forbid). The plain
    // key parts are kept as their own entity columns for the list filter/sort, so the keys themselves never need decoding.
    private static string Enc(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // Builds the OData filter for the optional query criteria (an absent criterion matches anything); null when the
    // query is empty so the read is an unfiltered scan.
    private static string? BuildFilter(RunnerAuthorizationQuery query)
    {
        // The column name must be a LITERAL part of the OData filter; only the value is an interpolation hole.
        // TableClient.CreateQueryFilter quotes every hole, so a hole for the column name would emit 'Status' eq 'x'
        // (a quoted literal, not a property reference) — an invalid query condition.
        var conditions = new List<string>(3);
        if (query.Status is { } status)
        {
            conditions.Add(TableClient.CreateQueryFilter($"Status eq {RunnerAuthorizationStatusNames.ToWire(status)}"));
        }

        if (query.Environment is { } environment)
        {
            conditions.Add(TableClient.CreateQueryFilter($"Environment eq {environment}"));
        }

        if (query.AdministeredEnvironments is { Count: > 0 } administered)
        {
            // The approver inbox (§5.5): Environment IN (the administered set) — Azure Table has no IN, so it is an OR group
            // over the server-derived environments (each value quoted by CreateQueryFilter). The set is never empty here
            // (the handler short-circuits a caller who administers nothing to an empty page before the store).
            var anyOf = new List<string>(administered.Count);
            foreach (string administeredEnvironment in administered)
            {
                anyOf.Add(TableClient.CreateQueryFilter($"Environment eq {administeredEnvironment}"));
            }

            conditions.Add("(" + string.Join(" or ", anyOf) + ")");
        }

        return conditions.Count == 0 ? null : string.Join(" and ", conditions);
    }

    // The record is a point read by (PartitionKey, RowKey); a missing entity surfaces as a null document to the caller.
    private async ValueTask<byte[]?> DocumentAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> response = await this.authorizations
            .GetEntityIfExistsAsync<TableEntity>(Enc(environment), Enc(runnerId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue ? response.Value!.GetBinary(DocumentColumn) : null;
    }
}