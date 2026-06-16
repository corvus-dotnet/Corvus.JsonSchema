// <copyright file="CosmosRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as a
/// document keyed by (and partitioned on) its runner id, holding the canonical registration JSON as a base64 byte
/// array plus a queryable <c>lastSeenAt</c> column used for pruning. Register is a single-partition upsert and
/// heartbeat is a single-partition read-modify-write, while list and prune run as cross-partition queries.
/// </summary>
/// <remarks>
/// Documents are written and read through the Cosmos <em>stream</em> APIs so persistence flows through the
/// <see cref="RunnerDocument"/> Corvus.Text.Json schema type and never the SDK's reflection serializer. Provision
/// the database and container once with <see cref="PrepareAsync(string, string, CancellationToken)"/>, then open
/// the registry with <see cref="ConnectAsync(string, string, CancellationToken)"/>; the overloads taking a
/// <see cref="CosmosClient"/> let callers configure the client (for example a least-privileged data-plane managed
/// identity) themselves.
/// </remarks>
public sealed class CosmosRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string RunnersContainerId = "runners";

    private static readonly byte[] IdProperty = "id"u8.ToArray();

    private readonly CosmosClient client;
    private readonly Container runners;
    private readonly bool ownsClient;

    private CosmosRunnerRegistry(CosmosClient client, Container runners, bool ownsClient)
    {
        this.client = client;
        this.runners = runners;
        this.ownsClient = ownsClient;
    }

    /// <summary>Provisions the registry's database and container over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string (typically the account key, which has management-plane rights).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CreateClientOptions());
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the registry's database and container over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Creating a database/container is a Cosmos <em>management-plane</em> operation — the data-plane RBAC roles
    /// (for example <c>Cosmos DB Built-in Data Contributor</c>) cannot do it. So provisioning needs the account
    /// key or a control-plane role and must be separated from the least-privileged data-plane credential used to
    /// <see cref="ConnectAsync(CosmosClient, string, CancellationToken)"/> the registry for operation. Run this
    /// once at deploy/migration time.
    /// </remarks>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the registry for operation against an already-provisioned database and container.</summary>
    /// <remarks>
    /// This creates no database or container, so it is safe to use a least-privileged data-plane credential.
    /// Call <see cref="PrepareAsync(string, string, CancellationToken)"/> once beforehand to provision.
    /// </remarks>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the client).</returns>
    public static ValueTask<CosmosRunnerRegistry> ConnectAsync(
        string connectionString,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CreateClientOptions());
        return new ValueTask<CosmosRunnerRegistry>(Connect(client, databaseName, ownsClient: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a managed identity / <c>TokenCredential</c>
    /// holding only a data-plane role — so the registry runs under a least-privileged principal with no account
    /// key. This creates no database or container; call <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="client">A configured Cosmos client; the caller retains ownership and must dispose it.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosRunnerRegistry> ConnectAsync(
        CosmosClient client,
        string databaseName = "arazzo",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosRunnerRegistry>(Connect(client, databaseName, ownsClient: false));
    }

    /// <summary>The Cosmos client options the registry relies on.</summary>
    /// <remarks>
    /// The registry reads and writes through the Cosmos stream APIs and serializes documents with
    /// Corvus.Text.Json, so no SDK serializer is configured.
    /// </remarks>
    /// <returns>The Cosmos client options used by the connection-string overloads.</returns>
    public static CosmosClientOptions CreateClientOptions() => new();

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        using var stream = RunnerDocument.WriteEnvelopeStream(runnerId, registration);
        using ResponseMessage response = await this.runners.UpsertItemStreamAsync(stream, new PartitionKey(runnerId), cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        var definition = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c JOIN h IN c.loadedVersions WHERE h.baseWorkflowId = @baseWorkflowId AND h.versionNumber = @versionNumber")
            .WithParameter("@baseWorkflowId", baseWorkflowId)
            .WithParameter("@versionNumber", versionNumber);

        await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(definition, cancellationToken).ConfigureAwait(false))
        {
            if (CosmosJson.AsInt64OrNull(element) is > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        var partition = new PartitionKey(runnerId);

        using ResponseMessage read = await this.runners.ReadItemStreamAsync(runnerId, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (read.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        read.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(read.Content, cancellationToken).ConfigureAwait(false);

        // The registration is embedded as raw nested JSON (no base64) — parse it straight from the doc value's bytes.
        RunnerRegistration current = RunnerRegistration.FromJson(CosmosJson.GetRawValue(payload.Memory, "doc"u8));

        using var stream = RunnerDocument.WriteEnvelopeStream(runnerId, current, at);
        using ResponseMessage response = await this.runners.UpsertItemStreamAsync(stream, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT * FROM c");
        var result = new List<RunnerRegistration>();
        await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(definition, cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson(CosmosJson.GetRawValue(element, "doc"u8)));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        var definition = new QueryDefinition("SELECT c.id FROM c WHERE c.lastSeenAt < @cutoff")
            .WithParameter("@cutoff", deadBefore.ToUnixTimeMilliseconds());

        var stale = new List<string>();
        await foreach (ReadOnlyMemory<byte> element in this.QueryElementsAsync(definition, cancellationToken).ConfigureAwait(false))
        {
            if (CosmosJson.GetString(element, IdProperty) is { } runnerId)
            {
                stale.Add(runnerId);
            }
        }

        foreach (string runnerId in stale)
        {
            using ResponseMessage response = await this.runners.DeleteItemStreamAsync(runnerId, new PartitionKey(runnerId), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        return stale.Count;
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
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(RunnersContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosRunnerRegistry Connect(CosmosClient client, string databaseName, bool ownsClient)
    {
        // GetDatabase/GetContainer return proxies without network I/O (no creation), so this is a pure
        // data-plane open against the already-provisioned resources.
        Database database = client.GetDatabase(databaseName);
        Container runners = database.GetContainer(RunnersContainerId);
        return new CosmosRunnerRegistry(client, runners, ownsClient);
    }

    private async IAsyncEnumerable<ReadOnlyMemory<byte>> QueryElementsAsync(QueryDefinition query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator iterator = this.runners.GetItemQueryStreamIterator(query);
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