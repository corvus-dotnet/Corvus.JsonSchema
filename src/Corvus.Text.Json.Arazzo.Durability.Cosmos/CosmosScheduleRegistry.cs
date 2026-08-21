// <copyright file="CosmosScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// An Azure Cosmos DB-backed <see cref="IScheduleRegistry"/>: one document per <c>scheduleId</c> whose create
/// conflict enforces the schedules contract's deployment-global uniqueness (the schedule id is both the
/// document id and the partition key). Under the composite <c>(environment, runId)</c> run key (ADR 0065
/// decision 9) the registry is the sole guardian of that uniqueness — a run-key collision can no longer
/// observe a schedule created in another environment — so a Cosmos deployment needs this durable registry,
/// not the in-memory one, or every control-plane restart forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// Documents flow through the Cosmos stream APIs and <see cref="ScheduleRegistrationDocument"/> — never the
/// SDK's reflection serializer. Provision the container once with
/// <see cref="PrepareAsync(CosmosClient, string, CancellationToken)"/>, then open the registry with
/// <see cref="ConnectAsync(CosmosClient, string, CancellationToken)"/>.
/// </remarks>
public sealed class CosmosScheduleRegistry : IScheduleRegistry, IAsyncDisposable
{
    private const string ContainerId = "schedule_registrations";

    private readonly CosmosClient client;
    private readonly Container registrations;
    private readonly bool ownsClient;

    private CosmosScheduleRegistry(CosmosClient client, Container registrations, bool ownsClient)
    {
        this.client = client;
        this.registrations = registrations;
        this.ownsClient = ownsClient;
    }

    /// <summary>Provisions the registry container over the given connection string.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string (typically the account key, which has management-plane rights).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString, CosmosWorkflowStateStore.CreateClientOptions());
        await PrepareAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the registry container over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <remarks>
    /// Creating a container is a Cosmos management-plane operation, so this needs the account key or a
    /// control-plane role; run it once at deploy time, separately from the least-privileged data-plane
    /// credential used to <see cref="ConnectAsync(CosmosClient, string, CancellationToken)"/> the registry.
    /// </remarks>
    /// <param name="client">A configured Cosmos client (the caller retains ownership and must dispose it).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the container exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(CosmosClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/id"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation against an already-provisioned container.</summary>
    /// <param name="connectionString">An Azure Cosmos DB connection string.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the client).</returns>
    public static ValueTask<CosmosScheduleRegistry> ConnectAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new CosmosClient(connectionString, CosmosWorkflowStateStore.CreateClientOptions());
        return new ValueTask<CosmosScheduleRegistry>(new CosmosScheduleRegistry(client, client.GetDatabase(databaseName).GetContainer(ContainerId), ownsClient: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied <see cref="CosmosClient"/>.</summary>
    /// <param name="client">A configured Cosmos client; the caller retains ownership and must dispose it.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosScheduleRegistry> ConnectAsync(CosmosClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosScheduleRegistry>(new CosmosScheduleRegistry(client, client.GetDatabase(databaseName).GetContainer(ContainerId), ownsClient: false));
    }

    /// <summary>Disposes the client if this registry created it (from a connection string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient)
        {
            this.client.Dispose();
        }

        return default;
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        var partition = new PartitionKey(scheduleId);

        // The create conflict IS the uniqueness gate; the read below sees the committed occupant.
        using (Stream stream = CosmosJson.WriteToStream(
            (ScheduleId: scheduleId, Environment: registration.Environment, RunId: registration.RunId.Value),
            static (Utf8JsonWriter writer, in (string ScheduleId, string Environment, string RunId) ctx)
                => ScheduleRegistrationDocument.WriteJson(writer, ctx.ScheduleId, ctx.Environment, ctx.RunId)))
        {
            using ResponseMessage created = await this.registrations.CreateItemStreamAsync(stream, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (created.StatusCode != HttpStatusCode.Conflict)
            {
                created.EnsureSuccessStatusCode();
                return;
            }
        }

        // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds; anything
        // else refuses without disclosing the occupant (the caller may have no reach over its environment).
        using ResponseMessage read = await this.registrations.ReadItemStreamAsync(scheduleId, partition, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (read.StatusCode != HttpStatusCode.NotFound)
        {
            read.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(read.Content, cancellationToken).ConfigureAwait(false);
            ScheduleRegistrationDocument existing = ScheduleRegistrationDocument.FromJson(payload.Memory);
            if (new ScheduleRegistration(existing.EnvironmentValue, new WorkflowRunId(existing.RunIdValue)) == registration)
            {
                return;
            }
        }

        ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        using ResponseMessage read = await this.registrations.ReadItemStreamAsync(scheduleId, new PartitionKey(scheduleId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (read.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        read.EnsureSuccessStatusCode();
        using CosmosJson.RentedResponse payload = await CosmosJson.ReadAllAsync(read.Content, cancellationToken).ConfigureAwait(false);
        ScheduleRegistrationDocument document = ScheduleRegistrationDocument.FromJson(payload.Memory);
        return new ScheduleRegistration(document.EnvironmentValue, new WorkflowRunId(document.RunIdValue));
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        using ResponseMessage deleted = await this.registrations.DeleteItemStreamAsync(scheduleId, new PartitionKey(scheduleId), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (deleted.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        deleted.EnsureSuccessStatusCode();
    }
}