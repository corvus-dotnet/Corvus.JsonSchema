// <copyright file="AzureStorageWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Storage-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>: the opaque
/// checkpoint is a block blob whose ETag is the optimistic-concurrency token, while the projected index and
/// the single-owner lease are Table storage entities. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// The checkpoint blob is the authoritative version (its ETag is the etag callers see); the index table is
/// updated after each successful checkpoint write. Create instances with <see cref="CreateAsync"/>.
/// </remarks>
public sealed class AzureStorageWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string IndexPartition = "run";
    private const string LeasePartition = "lease";

    // The Blob SDK defaults to the newest REST API version, which the Azurite emulator (and older real
    // accounts) may not yet recognise. Pin to a broadly-supported version so requests are accepted everywhere;
    // none of the features used here need anything newer.
    private const BlobClientOptions.ServiceVersion BlobApiVersion = BlobClientOptions.ServiceVersion.V2024_11_04;

    private readonly BlobContainerClient runs;
    private readonly TableClient index;
    private readonly TableClient leases;
    private readonly TimeProvider timeProvider;

    private AzureStorageWorkflowStateStore(BlobContainerClient runs, TableClient index, TableClient leases, TimeProvider timeProvider)
    {
        this.runs = runs;
        this.index = index;
        this.leases = leases;
        this.timeProvider = timeProvider;
    }

    /// <summary>Opens a store over the given storage connection string, creating its container and tables.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, initialised store.</returns>
    public static async ValueTask<AzureStorageWorkflowStateStore> CreateAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var blobService = new BlobServiceClient(connectionString, new BlobClientOptions(BlobApiVersion));
        BlobContainerClient runs = blobService.GetBlobContainerClient("arazzo-runs");
        await runs.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var tableService = new TableServiceClient(connectionString);
        TableClient index = tableService.GetTableClient("arazzoindex");
        TableClient leases = tableService.GetTableClient("arazzoleases");
        await index.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await leases.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        return new AzureStorageWorkflowStateStore(runs, index, leases, timeProvider ?? TimeProvider.System);
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8.ToArray(), index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] checkpoint, WorkflowRunIndexEntry indexEntry, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        BlobClient blob = this.runs.GetBlobClient(id.Value);
        var conditions = expected.IsNone
            ? new BlobRequestConditions { IfNoneMatch = ETag.All }
            : new BlobRequestConditions { IfMatch = new ETag(expected.Value!) };

        ETag etag;
        try
        {
            Response<BlobContentInfo> response = await blob.UploadAsync(
                BinaryData.FromBytes(checkpoint),
                new BlobUploadOptions { Conditions = conditions },
                cancellationToken).ConfigureAwait(false);
            etag = response.Value.ETag;
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            throw new WorkflowConflictException(id, expected);
        }

        await this.index.UpsertEntityAsync(BuildIndexEntity(id, indexEntry), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return new WorkflowEtag(etag.ToString());
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        BlobClient blob = this.runs.GetBlobClient(id.Value);
        try
        {
            Response<BlobDownloadResult> response = await blob.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return new WorkflowCheckpoint(response.Value.Content.ToArray(), new WorkflowEtag(response.Value.Details.ETag.ToString()));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");
        var entity = new TableEntity(LeasePartition, id.Value)
        {
            ["Owner"] = owner,
            ["Token"] = token,
            ["ExpiresAt"] = expiresAt.ToUnixTimeMilliseconds(),
        };

        NullableResponse<TableEntity> existing = await this.leases.GetEntityIfExistsAsync<TableEntity>(LeasePartition, id.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
        try
        {
            if (!existing.HasValue)
            {
                await this.leases.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
                return new WorkflowLease(id, owner, token, expiresAt);
            }

            long currentExpiresAt = existing.Value!.GetInt64("ExpiresAt") ?? 0;
            string currentOwner = existing.Value.GetString("Owner") ?? string.Empty;
            if (currentExpiresAt > now.ToUnixTimeMilliseconds() && currentOwner != owner)
            {
                return null;
            }

            await this.leases.UpdateEntityAsync(entity, existing.Value.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
            return new WorkflowLease(id, owner, token, expiresAt);
        }
        catch (RequestFailedException ex) when (ex.Status is 409 or 412)
        {
            // Another worker created or advanced the lease concurrently.
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> existing = await this.leases.GetEntityIfExistsAsync<TableEntity>(LeasePartition, lease.RunId.Value, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (existing is { HasValue: true, Value: { } current } && current.GetString("Token") == lease.Token)
        {
            try
            {
                await this.leases.DeleteEntityAsync(LeasePartition, lease.RunId.Value, current.ETag, cancellationToken).ConfigureAwait(false);
            }
            catch (RequestFailedException ex) when (ex.Status is 404 or 412)
            {
                // The lease was already released or superseded.
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.runs.GetBlobClient(id.Value).DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await this.index.DeleteEntityAsync(IndexPartition, id.Value, ETag.All, cancellationToken).ConfigureAwait(false);
        await this.leases.DeleteEntityAsync(LeasePartition, id.Value, ETag.All, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {IndexPartition} and Status eq {SuspendedStatus} and DueAt le {before.ToUnixTimeMilliseconds()}");
        await foreach (TableEntity entity in this.index.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(entity.RowKey);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        // Filter by channel server-side; the null-correlation rule is applied client-side because Table
        // storage cannot query for an absent property.
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {IndexPartition} and Status eq {SuspendedStatus} and AwaitingChannel eq {channel}");
        await foreach (TableEntity entity in this.index.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            string? stored = entity.GetString("AwaitingCorrelationId");
            if (correlationId is null || stored is null || stored == correlationId)
            {
                yield return new WorkflowRunId(entity.RowKey);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {IndexPartition}");
        if (query.Status is { } status)
        {
            filter += TableClient.CreateQueryFilter($" and Status eq {status.ToString()}");
        }

        if (query.WorkflowId is { } workflowId)
        {
            filter += TableClient.CreateQueryFilter($" and WorkflowId eq {workflowId}");
        }

        var runs = new List<WorkflowRunListing>();
        await foreach (TableEntity entity in this.index.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (runs.Count >= query.Limit)
            {
                break;
            }

            runs.Add(new WorkflowRunListing(new WorkflowRunId(entity.RowKey), ReadIndexEntity(entity)));
        }

        return new WorkflowRunPage(runs);
    }

    private static TableEntity BuildIndexEntity(WorkflowRunId id, in WorkflowRunIndexEntry index)
    {
        var entity = new TableEntity(IndexPartition, id.Value)
        {
            ["Status"] = index.Status.ToString(),
            ["WorkflowId"] = index.WorkflowId,
            ["CreatedAt"] = index.CreatedAt.ToUnixTimeMilliseconds(),
            ["UpdatedAt"] = index.UpdatedAt.ToUnixTimeMilliseconds(),
        };

        if (index.DueAt is { } due)
        {
            entity["DueAt"] = due.ToUnixTimeMilliseconds();
        }

        if (index.AwaitingChannel is { } channel)
        {
            entity["AwaitingChannel"] = channel;
        }

        if (index.AwaitingCorrelationId is { } correlationId)
        {
            entity["AwaitingCorrelationId"] = correlationId;
        }

        if (index.ErrorType is { } errorType)
        {
            entity["ErrorType"] = errorType;
        }

        return entity;
    }

    private static WorkflowRunIndexEntry ReadIndexEntity(TableEntity entity)
    {
        long? due = entity.GetInt64("DueAt");
        return new WorkflowRunIndexEntry(
            entity.GetString("WorkflowId") ?? string.Empty,
            Enum.Parse<WorkflowRunStatus>(entity.GetString("Status") ?? nameof(WorkflowRunStatus.Pending)),
            DateTimeOffset.FromUnixTimeMilliseconds(entity.GetInt64("CreatedAt") ?? 0),
            DateTimeOffset.FromUnixTimeMilliseconds(entity.GetInt64("UpdatedAt") ?? 0),
            due is { } d ? DateTimeOffset.FromUnixTimeMilliseconds(d) : null,
            entity.GetString("AwaitingChannel"),
            entity.GetString("AwaitingCorrelationId"),
            entity.GetString("ErrorType"));
    }
}