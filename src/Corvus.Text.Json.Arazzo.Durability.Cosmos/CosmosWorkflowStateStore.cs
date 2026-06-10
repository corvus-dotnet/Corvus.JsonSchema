// <copyright file="CosmosWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>. Each run
/// is a document holding the opaque checkpoint (as a base64 byte array) plus the projected index fields;
/// optimistic concurrency maps to the document's native <c>_etag</c> (an <c>If-Match</c> replace), and the
/// single-owner lease to a small leases container guarded by the same ETag mechanism.
/// </summary>
/// <remarks>
/// The run id is both the document id and the partition key, so every checkpoint operation is a single-partition
/// point operation. Create instances with <see cref="CreateAsync(string, string, TimeProvider?, CancellationToken)"/>;
/// the overload taking a <see cref="CosmosClient"/> exists for callers (such as the emulator-based tests) that
/// must configure the client themselves.
/// </remarks>
public sealed class CosmosWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string RunsContainerId = "workflow_runs";
    private const string LeasesContainerId = "workflow_leases";

    private readonly CosmosClient client;
    private readonly Container runs;
    private readonly Container leases;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosWorkflowStateStore(CosmosClient client, Container runs, Container leases, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.runs = runs;
        this.leases = leases;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
    }

    /// <summary>Opens a store over the given Cosmos connection string, creating its database and containers.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, initialised store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosWorkflowStateStore> CreateAsync(
        string connectionString,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return InitialiseAsync(client, databaseName, timeProvider, ownsClient: true, cancellationToken);
    }

    /// <summary>Opens a store over a caller-supplied <see cref="CosmosClient"/>, creating its database and containers.</summary>
    /// <param name="client">A configured Cosmos client; the caller retains ownership and must dispose it.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, initialised store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosWorkflowStateStore> CreateAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return InitialiseAsync(client, databaseName, timeProvider, ownsClient: false, cancellationToken);
    }

    /// <summary>The serializer options the store relies on (camelCase property names, null properties omitted).</summary>
    /// <returns>The Cosmos client options used by the connection-string overload.</returns>
    public static CosmosClientOptions CreateClientOptions() => new()
    {
        UseSystemTextJsonSerializerWithOptions = SerializerOptions,
    };

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8.ToArray(), index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        var document = RunDocument.Build(id, checkpoint, index);
        var partition = new PartitionKey(id.Value);
        try
        {
            if (expected.IsNone)
            {
                ItemResponse<RunDocument> created = await this.runs.CreateItemAsync(document, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new WorkflowEtag(created.ETag);
            }

            var options = new ItemRequestOptions { IfMatchEtag = expected.Value };
            ItemResponse<RunDocument> replaced = await this.runs.ReplaceItemAsync(document, id.Value, partition, options, cancellationToken).ConfigureAwait(false);
            return new WorkflowEtag(replaced.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            throw new WorkflowConflictException(id, expected);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        try
        {
            ItemResponse<RunDocument> response = await this.runs.ReadItemAsync<RunDocument>(id.Value, new PartitionKey(id.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
            return new WorkflowCheckpoint(response.Resource.Checkpoint, new WorkflowEtag(response.ETag));
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
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
        var document = new LeaseDocument { Id = id.Value, Owner = owner, Token = token, ExpiresAt = expiresAt.ToUnixTimeMilliseconds() };
        var partition = new PartitionKey(id.Value);

        try
        {
            LeaseDocument? existing;
            string existingEtag;
            try
            {
                ItemResponse<LeaseDocument> read = await this.leases.ReadItemAsync<LeaseDocument>(id.Value, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
                existing = read.Resource;
                existingEtag = read.ETag;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                existing = null;
                existingEtag = string.Empty;
            }

            if (existing is null)
            {
                await this.leases.CreateItemAsync(document, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
                return new WorkflowLease(id, owner, token, expiresAt);
            }

            if (existing.ExpiresAt > now.ToUnixTimeMilliseconds() && existing.Owner != owner)
            {
                return null;
            }

            var options = new ItemRequestOptions { IfMatchEtag = existingEtag };
            await this.leases.ReplaceItemAsync(document, id.Value, partition, options, cancellationToken).ConfigureAwait(false);
            return new WorkflowLease(id, owner, token, expiresAt);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            // Another worker created or advanced the lease concurrently.
            return null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        var partition = new PartitionKey(lease.RunId.Value);
        try
        {
            ItemResponse<LeaseDocument> read = await this.leases.ReadItemAsync<LeaseDocument>(lease.RunId.Value, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (read.Resource.Token != lease.Token)
            {
                return;
            }

            var options = new ItemRequestOptions { IfMatchEtag = read.ETag };
            await this.leases.DeleteItemAsync<LeaseDocument>(lease.RunId.Value, partition, options, cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            // The lease was already released or superseded.
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        var partition = new PartitionKey(id.Value);
        await DeleteIfExistsAsync(this.runs, id.Value, partition, cancellationToken).ConfigureAwait(false);
        await DeleteIfExistsAsync(this.leases, id.Value, partition, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT c.id FROM c WHERE c.status = @status AND IS_DEFINED(c.dueAt) AND c.dueAt <= @before")
            .WithParameter("@status", SuspendedStatus)
            .WithParameter("@before", before.ToUnixTimeMilliseconds());
        await foreach (IdResult result in this.QueryRunsAsync<IdResult>(query, cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(result.Id);
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        // Filter by channel server-side; the null-correlation rule is applied client-side because a document
        // awaiting any correlation omits the property entirely.
        var query = new QueryDefinition("SELECT c.id, c.awaitingCorrelationId FROM c WHERE c.status = @status AND c.awaitingChannel = @channel")
            .WithParameter("@status", SuspendedStatus)
            .WithParameter("@channel", channel);
        await foreach (AwaitingResult result in this.QueryRunsAsync<AwaitingResult>(query, cancellationToken).ConfigureAwait(false))
        {
            if (correlationId is null || result.AwaitingCorrelationId is null || result.AwaitingCorrelationId == correlationId)
            {
                yield return new WorkflowRunId(result.Id);
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        var conditions = new List<string>();
        if (query.Status is not null)
        {
            conditions.Add("c.status = @status");
        }

        if (query.WorkflowId is not null)
        {
            conditions.Add("c.workflowId = @workflowId");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT * FROM c" + where);
        if (query.Status is { } s)
        {
            definition = definition.WithParameter("@status", s.ToString());
        }

        if (query.WorkflowId is { } w)
        {
            definition = definition.WithParameter("@workflowId", w);
        }

        var runs = new List<WorkflowRunListing>();
        await foreach (RunDocument document in this.QueryRunsAsync<RunDocument>(definition, cancellationToken).ConfigureAwait(false))
        {
            if (runs.Count >= query.Limit)
            {
                break;
            }

            runs.Add(new WorkflowRunListing(new WorkflowRunId(document.Id), document.ToIndexEntry()));
        }

        return new WorkflowRunPage(runs);
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

    private static async ValueTask<CosmosWorkflowStateStore> InitialiseAsync(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        Container runs = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(RunsContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
        Container leases = await database.CreateContainerIfNotExistsAsync(new ContainerProperties(LeasesContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
        return new CosmosWorkflowStateStore(client, runs, leases, timeProvider ?? TimeProvider.System, ownsClient);
    }

    private static async ValueTask DeleteIfExistsAsync(Container container, string id, PartitionKey partition, CancellationToken cancellationToken)
    {
        try
        {
            await container.DeleteItemAsync<object>(id, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already absent.
        }
    }

    private async IAsyncEnumerable<T> QueryRunsAsync<T>(QueryDefinition query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator<T> iterator = this.runs.GetItemQueryIterator<T>(query);
        while (iterator.HasMoreResults)
        {
            FeedResponse<T> page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (T item in page)
            {
                yield return item;
            }
        }
    }

    private sealed class RunDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("checkpoint")]
        public byte[] Checkpoint { get; set; } = [];

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("workflowId")]
        public string WorkflowId { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("updatedAt")]
        public long UpdatedAt { get; set; }

        [JsonPropertyName("dueAt")]
        public long? DueAt { get; set; }

        [JsonPropertyName("awaitingChannel")]
        public string? AwaitingChannel { get; set; }

        [JsonPropertyName("awaitingCorrelationId")]
        public string? AwaitingCorrelationId { get; set; }

        [JsonPropertyName("errorType")]
        public string? ErrorType { get; set; }

        public static RunDocument Build(WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index) => new()
        {
            Id = id.Value,
            Checkpoint = checkpoint,
            Status = index.Status.ToString(),
            WorkflowId = index.WorkflowId,
            CreatedAt = index.CreatedAt.ToUnixTimeMilliseconds(),
            UpdatedAt = index.UpdatedAt.ToUnixTimeMilliseconds(),
            DueAt = index.DueAt?.ToUnixTimeMilliseconds(),
            AwaitingChannel = index.AwaitingChannel,
            AwaitingCorrelationId = index.AwaitingCorrelationId,
            ErrorType = index.ErrorType,
        };

        public WorkflowRunIndexEntry ToIndexEntry() => new(
            this.WorkflowId,
            Enum.Parse<WorkflowRunStatus>(this.Status),
            DateTimeOffset.FromUnixTimeMilliseconds(this.CreatedAt),
            DateTimeOffset.FromUnixTimeMilliseconds(this.UpdatedAt),
            this.DueAt is { } due ? DateTimeOffset.FromUnixTimeMilliseconds(due) : null,
            this.AwaitingChannel,
            this.AwaitingCorrelationId,
            this.ErrorType);
    }

    private sealed class LeaseDocument
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("owner")]
        public string Owner { get; set; } = string.Empty;

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("expiresAt")]
        public long ExpiresAt { get; set; }
    }

    private sealed class IdResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class AwaitingResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("awaitingCorrelationId")]
        public string? AwaitingCorrelationId { get; set; }
    }
}