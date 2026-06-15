// <copyright file="CosmosWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Runtime.CompilerServices;
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
/// point operation. Documents are written and read through the Cosmos <em>stream</em> APIs, so persistence flows
/// through Corvus.Text.Json schema types (<see cref="RunDocument"/>/<see cref="LeaseDocument"/>) and never the SDK's
/// reflection serializer. Provision the database and containers once with
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

    private static readonly byte[] IdProperty = "id"u8.ToArray();
    private static readonly byte[] StatusProperty = "status"u8.ToArray();
    private static readonly byte[] AwaitingCorrelationIdProperty = "awaitingCorrelationId"u8.ToArray();

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

    /// <summary>The Cosmos client options the store relies on.</summary>
    /// <remarks>
    /// The store reads and writes through the Cosmos stream APIs and serializes documents with Corvus.Text.Json, so
    /// no SDK serializer is configured.
    /// </remarks>
    /// <returns>The Cosmos client options used by the connection-string overload.</returns>
    public static CosmosClientOptions CreateClientOptions() => new();

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
        var partition = new PartitionKey(id.Value);

        // Serialize the run straight into the pooled write stream — no intermediate RunDocument value, no re-serialization.
        using var stream = CosmosJson.WriteToStream(
            (Id: id, Checkpoint: checkpoint, Index: index),
            static (Utf8JsonWriter writer, in (WorkflowRunId Id, byte[] Checkpoint, WorkflowRunIndexEntry Index) ctx)
                => RunDocument.WriteJson(writer, ctx.Id, ctx.Checkpoint, ctx.Index));

        if (expected.IsNone)
        {
            using ResponseMessage response = await this.runs.CreateItemStreamAsync(stream, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Conflict)
            {
                throw new WorkflowConflictException(id, expected);
            }

            response.EnsureSuccessStatusCode();
            return new WorkflowEtag(response.Headers.ETag);
        }
        else
        {
            var options = new ItemRequestOptions { IfMatchEtag = expected.Value };
            using ResponseMessage response = await this.runs.ReplaceItemStreamAsync(stream, id.Value, partition, options, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
            {
                throw new WorkflowConflictException(id, expected);
            }

            response.EnsureSuccessStatusCode();
            return new WorkflowEtag(response.Headers.ETag);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        using ResponseMessage response = await this.runs.ReadItemStreamAsync(id.Value, new PartitionKey(id.Value), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
        RunDocument document = RunDocument.FromJson(payload.Memory);
        return new WorkflowCheckpoint(document.CheckpointBytes(), new WorkflowEtag(response.Headers.ETag));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");
        var partition = new PartitionKey(id.Value);

        // Serialize the lease straight into the pooled write stream — no intermediate LeaseDocument value.
        MemoryStream BuildLeaseStream() => CosmosJson.WriteToStream(
            (Id: id.Value, Owner: owner, Token: token, ExpiresAt: expiresAt.ToUnixTimeMilliseconds()),
            static (Utf8JsonWriter writer, in (string Id, string Owner, string Token, long ExpiresAt) ctx)
                => LeaseDocument.WriteJson(writer, ctx.Id, ctx.Owner, ctx.Token, ctx.ExpiresAt));

        using ResponseMessage read = await this.leases.ReadItemStreamAsync(id.Value, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (read.StatusCode == HttpStatusCode.NotFound)
        {
            using var createStream = BuildLeaseStream();
            using ResponseMessage created = await this.leases.CreateItemStreamAsync(createStream, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (created.StatusCode is HttpStatusCode.Conflict)
            {
                // Another worker created the lease concurrently.
                return null;
            }

            created.EnsureSuccessStatusCode();
            return new WorkflowLease(id, owner, token, expiresAt);
        }

        read.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(read.Content, cancellationToken).ConfigureAwait(false);
        LeaseDocument existing = LeaseDocument.FromJson(payload.Memory);
        if (existing.ExpiresAtValue > now.ToUnixTimeMilliseconds() && existing.OwnerValue != owner)
        {
            return null;
        }

        var options = new ItemRequestOptions { IfMatchEtag = read.Headers.ETag };
        using var replaceStream = BuildLeaseStream();
        using ResponseMessage replaced = await this.leases.ReplaceItemStreamAsync(replaceStream, id.Value, partition, options, cancellationToken).ConfigureAwait(false);
        if (replaced.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            // Another worker advanced the lease concurrently.
            return null;
        }

        replaced.EnsureSuccessStatusCode();
        return new WorkflowLease(id, owner, token, expiresAt);
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        var partition = new PartitionKey(lease.RunId.Value);
        using ResponseMessage read = await this.leases.ReadItemStreamAsync(lease.RunId.Value, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (read.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        read.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(read.Content, cancellationToken).ConfigureAwait(false);
        if (LeaseDocument.FromJson(payload.Memory).TokenValue != lease.Token)
        {
            return;
        }

        var options = new ItemRequestOptions { IfMatchEtag = read.Headers.ETag };
        using ResponseMessage deleted = await this.leases.DeleteItemStreamAsync(lease.RunId.Value, partition, options, cancellationToken).ConfigureAwait(false);
        if (deleted.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.PreconditionFailed)
        {
            // The lease was already released or superseded.
            return;
        }

        deleted.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        var partition = new PartitionKey(id.Value);
        await DeleteIfExistsAsync(this.runs, id.Value, partition, cancellationToken).ConfigureAwait(false);
        await DeleteIfExistsAsync(this.leases, id.Value, partition, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT c.id FROM c WHERE c.status = @status AND IS_DEFINED(c.dueAt) AND c.dueAt <= @before")
            .WithParameter("@status", SuspendedStatus)
            .WithParameter("@before", before.ToUnixTimeMilliseconds());
        await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.runs, query, cancellationToken).ConfigureAwait(false))
        {
            if (CosmosJson.GetString(element, IdProperty) is { } runId)
            {
                yield return new WorkflowRunId(runId);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        // Filter by channel server-side; the null-correlation rule is applied client-side because a document
        // awaiting any correlation omits the property entirely.
        var query = new QueryDefinition("SELECT c.id, c.awaitingCorrelationId FROM c WHERE c.status = @status AND c.awaitingChannel = @channel")
            .WithParameter("@status", SuspendedStatus)
            .WithParameter("@channel", channel);
        await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.runs, query, cancellationToken).ConfigureAwait(false))
        {
            string? runId = CosmosJson.GetString(element, IdProperty);
            if (runId is null)
            {
                continue;
            }

            string? awaitingCorrelationId = CosmosJson.GetString(element, AwaitingCorrelationIdProperty);
            if (correlationId is null || awaitingCorrelationId is null || awaitingCorrelationId == correlationId)
            {
                yield return new WorkflowRunId(runId);
            }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, [EnumeratorCancellation] CancellationToken cancellationToken)
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

        var candidates = new List<(string Id, string Status)>();
        bool anyRunning = false;
        await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.runs, candidateQuery, cancellationToken).ConfigureAwait(false))
        {
            string? candidateId = CosmosJson.GetString(element, IdProperty);
            string? candidateStatus = CosmosJson.GetString(element, StatusProperty);
            if (candidateId is null || candidateStatus is null)
            {
                continue;
            }

            candidates.Add((candidateId, candidateStatus));
            if (candidateStatus == RunningStatus)
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
            await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.leases, leaseQuery, cancellationToken).ConfigureAwait(false))
            {
                if (CosmosJson.GetString(element, IdProperty) is { } leaseId)
                {
                    heldRunIds.Add(leaseId);
                }
            }
        }

        foreach ((string id, string status) in candidates)
        {
            if (status == PendingStatus || (status == RunningStatus && !heldRunIds.Contains(id)))
            {
                yield return new WorkflowRunId(id);
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
        if (!query.Tags.IsEmpty)
        {
            // The needle is materialized to strings only here, at the SQL parameter-binding leaf the Cosmos query
            // requires; the stored row tags are matched server-side by ARRAY_CONTAINS and never materialized.
            List<string> queryTags = query.Tags.ToList();
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
        await foreach (ReadOnlyMemory<byte> element in QueryElementsAsync(this.runs, definition, cancellationToken).ConfigureAwait(false))
        {
            RunDocument document = RunDocument.FromJson(element);
            runs.Add(new WorkflowRunListing(new WorkflowRunId(document.IdValue), document.ToIndexEntry()));
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
        using ResponseMessage response = await container.DeleteItemStreamAsync(id, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Already absent.
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryElementsAsync(Container container, QueryDefinition query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator iterator = container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                yield return element;
            }
        }
    }
}