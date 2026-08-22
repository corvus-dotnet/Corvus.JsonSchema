// <copyright file="AzureStorageScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;

using Corvus.Text.Json.Arazzo.Durability.Schedules;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table storage-backed <see cref="IScheduleRegistry"/>: one entity per <c>scheduleId</c> whose
/// add-if-absent conflict enforces the schedules contract's deployment-global uniqueness. Under the composite
/// <c>(environment, runId)</c> run key (ADR 0065 decision 9) the registry is the sole guardian of that
/// uniqueness — a run-key collision can no longer observe a schedule created in another environment — so an
/// Azure Storage deployment needs this durable registry, not the in-memory one, or every control-plane
/// restart forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// Provision the table once with <see cref="PrepareAsync(string, CancellationToken)"/>, then open the
/// registry with <see cref="ConnectAsync(string, CancellationToken)"/>; the overloads taking a
/// <see cref="TableServiceClient"/> let callers configure the client (for example a least-privileged
/// data-plane managed identity) themselves.
/// </remarks>
public sealed class AzureStorageScheduleRegistry : IScheduleRegistry
{
    private const string RegistryTable = "arazzoschedules";
    private const string Partition = "schedule";

    private readonly TableClient registrations;

    private AzureStorageScheduleRegistry(TableClient registrations)
    {
        this.registrations = registrations;
    }

    /// <summary>Provisions the registry's table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the registry's table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(RegistryTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<AzureStorageScheduleRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Opens the registry for operation over a caller-supplied service client (the caller retains ownership).</summary>
    /// <param name="tableService">A configured table service client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<AzureStorageScheduleRegistry> ConnectAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageScheduleRegistry>(new AzureStorageScheduleRegistry(tableService.GetTableClient(RegistryTable)));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        // The add-if-absent conflict IS the uniqueness gate; the read below sees the committed occupant.
        var entity = new TableEntity(Partition, scheduleId)
        {
            ["Environment"] = registration.Environment,
            ["RunId"] = registration.RunId.Value,
        };
        try
        {
            await this.registrations.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Occupied: fall through to compare with the occupant.
        }

        // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds; anything
        // else refuses without disclosing the occupant (the caller may have no reach over its environment).
        NullableResponse<TableEntity> existing = await this.registrations.GetEntityIfExistsAsync<TableEntity>(Partition, scheduleId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (existing is { HasValue: true, Value: { } occupant }
            && occupant.GetString("Environment") is { } environment
            && occupant.GetString("RunId") is { } runId
            && new ScheduleRegistration(environment, new WorkflowRunId(runId)) == registration)
        {
            return;
        }

        ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        NullableResponse<TableEntity> existing = await this.registrations.GetEntityIfExistsAsync<TableEntity>(Partition, scheduleId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return existing is { HasValue: true, Value: { } entity }
            && entity.GetString("Environment") is { } environment
            && entity.GetString("RunId") is { } runId
            ? new ScheduleRegistration(environment, new WorkflowRunId(runId))
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await this.registrations.DeleteEntityAsync(Partition, scheduleId, ETag.All, cancellationToken).ConfigureAwait(false);
    }
}