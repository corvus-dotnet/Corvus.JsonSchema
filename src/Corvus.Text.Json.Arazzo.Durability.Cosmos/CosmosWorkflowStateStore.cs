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
/// point operation. Provision the database and containers once with
/// <see cref="PrepareAsync(string, string, CancellationToken)"/>, then open the store with
/// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/>; the overloads taking a
/// <see cref="CosmosClient"/> let callers configure the client (for example a least-privileged data-plane
/// managed identity) themselves.
/// </remarks>
public sealed class CosmosWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string PendingStatus = nameof(WorkflowRunStatus.Pending);
    private const string RunningStatus = nameof(WorkflowRunStatus.Running);
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

    /// <summary>Provisions the store's database and containers over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string (typically the account key, which has management-plane rights).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and containers exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CreateClientOptions());
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's database and containers over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Creating a database/container is a Cosmos <em>management-plane</em> operation — the data-plane RBAC
    /// roles (for example <c>Cosmos DB Built-in Data Contributor</c>) cannot do it. So provisioning needs the
    /// account key or a control-plane role and must be separated from the least-privileged data-plane
    /// credential used to <see cref="ConnectAsync(CosmosClient, string, TimeProvider?, CancellationToken)"/>
    /// the store for operation. Run this once at deploy/migration time.
    /// </remarks>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and containers exist (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the store for operation against an already-provisioned database and containers.</summary>
    /// <remarks>
    /// This creates no database or container, so it is safe to use a least-privileged data-plane credential.
    /// Call <see cref="PrepareAsync(string, string, CancellationToken)"/> once beforehand to provision.
    /// </remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<CosmosWorkflowStateStore> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosWorkflowStateStore>(Connect(client, databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a managed identity / <c>TokenCredential</c>
    /// holding only a data-plane role — so the store runs under a least-privileged principal with no account
    /// key. This creates no database or container; call <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="client">A configured Cosmos client; the caller retains ownership and must dispose it.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosWorkflowStateStore> ConnectAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosWorkflowStateStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
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
    public async IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        if (hostedWorkflowIds.Count == 0)
        {
            yield break;
        }

        // Candidate runs are Pending (always claimable) or Running (claimable only if no live lease holds them).
        var candidateQuery = new QueryDefinition(
            "SELECT c.id, c.status FROM c WHERE (c.status = @pending OR c.status = @running) AND ARRAY_CONTAINS(@hosted, c.workflowId)")
            .WithParameter("@pending", PendingStatus)
            .WithParameter("@running", RunningStatus)
            .WithParameter("@hosted", new List<string>(hostedWorkflowIds));

        var candidates = new List<ClaimableResult>();
        bool anyRunning = false;
        await foreach (ClaimableResult result in this.QueryRunsAsync<ClaimableResult>(candidateQuery, cancellationToken).ConfigureAwait(false))
        {
            candidates.Add(result);
            if (result.Status == RunningStatus)
            {
                anyRunning = true;
            }
        }

        // Only consult the leases container if a Running candidate could be held by a live lease.
        var heldRunIds = new HashSet<string>();
        if (anyRunning)
        {
            var leaseQuery = new QueryDefinition("SELECT c.id FROM c WHERE c.expiresAt > @now")
                .WithParameter("@now", now.ToUnixTimeMilliseconds());
            using FeedIterator<IdResult> leaseIterator = this.leases.GetItemQueryIterator<IdResult>(leaseQuery);
            while (leaseIterator.HasMoreResults)
            {
                FeedResponse<IdResult> page = await leaseIterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
                foreach (IdResult lease in page)
                {
                    heldRunIds.Add(lease.Id);
                }
            }
        }

        foreach (ClaimableResult candidate in candidates)
        {
            if (candidate.Status == PendingStatus || (candidate.Status == RunningStatus && !heldRunIds.Contains(candidate.Id)))
            {
                yield return new WorkflowRunId(candidate.Id);
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

        if (query.CreatedAfter is not null)
        {
            conditions.Add("c.createdAt >= @createdAfter");
        }

        if (query.CreatedBefore is not null)
        {
            conditions.Add("c.createdAt < @createdBefore");
        }

        if (query.UpdatedAfter is not null)
        {
            conditions.Add("c.updatedAt >= @updatedAfter");
        }

        if (query.UpdatedBefore is not null)
        {
            conditions.Add("c.updatedAt < @updatedBefore");
        }

        if (query.CorrelationId is not null)
        {
            conditions.Add("c.correlationId = @correlationId");
        }

        var tagParameters = new List<(string Name, string Value)>();
        if (query.Tags is { Count: > 0 } queryTags)
        {
            for (int i = 0; i < queryTags.Count; i++)
            {
                string name = $"@tag{i}";
                conditions.Add($"ARRAY_CONTAINS(c.tags, {name})");
                tagParameters.Add((name, queryTags[i]));
            }
        }

        // Row-security reach (§14.2): translate the filter to a native EXISTS over the embedded securityTags
        // array. Every value is bound as a query parameter (no concatenation); reached only for a store that
        // declares ISupportsRowSecurityFilter.
        var securityParameters = new List<(string Name, string Value)>();
        if (query.Security is { } security)
        {
            int securityParam = 0;
            var emitter = new CosmosSecurityRuleEmitter("c.securityTags", "k", "v", value =>
            {
                string name = "@sec" + securityParam++.ToString(System.Globalization.CultureInfo.InvariantCulture);
                securityParameters.Add((name, value));
                return name;
            });
            conditions.Add(security.ToSqlPredicate(emitter));
        }

        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        if (after is not null)
        {
            conditions.Add("c.id > @after");
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT * FROM c" + where + " ORDER BY c.id");
        if (query.Status is { } s)
        {
            definition = definition.WithParameter("@status", s.ToString());
        }

        if (query.WorkflowId is { } w)
        {
            definition = definition.WithParameter("@workflowId", w);
        }

        if (query.CreatedAfter is { } createdAfter)
        {
            definition = definition.WithParameter("@createdAfter", createdAfter.ToUnixTimeMilliseconds());
        }

        if (query.CreatedBefore is { } createdBefore)
        {
            definition = definition.WithParameter("@createdBefore", createdBefore.ToUnixTimeMilliseconds());
        }

        if (query.UpdatedAfter is { } updatedAfter)
        {
            definition = definition.WithParameter("@updatedAfter", updatedAfter.ToUnixTimeMilliseconds());
        }

        if (query.UpdatedBefore is { } updatedBefore)
        {
            definition = definition.WithParameter("@updatedBefore", updatedBefore.ToUnixTimeMilliseconds());
        }

        if (query.CorrelationId is { } correlationId)
        {
            definition = definition.WithParameter("@correlationId", correlationId);
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

        var runs = new List<WorkflowRunListing>();
        await foreach (RunDocument document in this.QueryRunsAsync<RunDocument>(definition, cancellationToken).ConfigureAwait(false))
        {
            runs.Add(new WorkflowRunListing(new WorkflowRunId(document.Id), document.ToIndexEntry()));
            if (runs.Count > query.Limit)
            {
                // Fetched one beyond the page — a next page exists; stop early to avoid draining the iterator.
                break;
            }
        }

        return WorkflowContinuationToken.Paginate(runs, query.Limit);
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

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(RunsContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(LeasesContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosWorkflowStateStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        // GetDatabase/GetContainer return proxies without network I/O (no creation), so this is a pure
        // data-plane open against the already-provisioned resources.
        Database database = client.GetDatabase(databaseName);
        Container runs = database.GetContainer(RunsContainerId);
        Container leases = database.GetContainer(LeasesContainerId);
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

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("securityTags")]
        public List<SecurityTagDocument>? SecurityTags { get; set; }

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
            CorrelationId = index.CorrelationId,
            Tags = index.Tags is { Count: > 0 } t ? t.ToList() : null,
            SecurityTags = index.SecurityTags is { Count: > 0 } st ? st.Select(SecurityTagDocument.From).ToList() : null,
        };

        public WorkflowRunIndexEntry ToIndexEntry() => new(
            this.WorkflowId,
            Enum.Parse<WorkflowRunStatus>(this.Status),
            DateTimeOffset.FromUnixTimeMilliseconds(this.CreatedAt),
            DateTimeOffset.FromUnixTimeMilliseconds(this.UpdatedAt),
            this.DueAt is { } due ? DateTimeOffset.FromUnixTimeMilliseconds(due) : null,
            this.AwaitingChannel,
            this.AwaitingCorrelationId,
            this.ErrorType,
            CorrelationId: this.CorrelationId,
            Tags: this.Tags,
            SecurityTags: this.SecurityTags is { Count: > 0 } st ? st.Select(t => t.ToSecurityTag()).ToList() : null);
    }

    /// <summary>A security tag as embedded in a document's <c>securityTags</c> array.</summary>
    private sealed class SecurityTagDocument
    {
        [JsonPropertyName("k")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("v")]
        public string Value { get; set; } = string.Empty;

        public static SecurityTagDocument From(SecurityTag tag) => new() { Key = tag.Key, Value = tag.Value };

        public SecurityTag ToSecurityTag() => new(this.Key, this.Value);
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

    private sealed class ClaimableResult
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}