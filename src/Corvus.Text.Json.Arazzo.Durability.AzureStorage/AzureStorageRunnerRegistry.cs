// <copyright file="AzureStorageRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure;
using Azure.Data.Tables;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Storage-backed <see cref="IRunnerRegistry"/>: each <see cref="RunnerRegistration"/> is held as a
/// single Table entity keyed by runner id, storing the registration's JSON document verbatim in a <c>Doc</c>
/// property alongside a queryable <c>LastSeenAt</c> column used for pruning. Every registration shares a fixed
/// PartitionKey so the whole registry is a single, cheaply-enumerable partition. Works against Azure Storage and
/// the Azurite emulator.
/// </summary>
/// <remarks>
/// The <c>Doc</c> property is the canonical record — the registration round-trips through it unchanged, exactly as
/// every other backend keeps the JSON verbatim. Provision the table once with
/// <see cref="PrepareAsync(string, CancellationToken)"/>, then open the registry with
/// <see cref="ConnectAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class AzureStorageRunnerRegistry : IRunnerRegistry
{
    private const string RunnersTable = "arazzoRunners";
    private const string PartitionKey = "runner";

    private readonly TableClient runners;

    private AzureStorageRunnerRegistry(TableClient runners)
    {
        this.runners = runners;
    }

    /// <summary>Provisions the registry's table over the given connection string.</summary>
    /// <remarks>See <see cref="PrepareAsync(TableServiceClient, CancellationToken)"/> for the privilege rationale.</remarks>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create the table.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (the operation is idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>
    /// Provisions the registry's table. Table creation is a broader right than the per-entity data access the
    /// registry needs at runtime, so run this once at deploy/migration time, separately from the least-privileged
    /// credential used to <see cref="ConnectAsync(TableServiceClient, CancellationToken)"/> the registry for operation.
    /// </summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity / <c>TokenCredential</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(RunnersTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation against an already-provisioned table.</summary>
    /// <remarks>
    /// This creates no table, so it is safe to use a least-privileged data-plane credential (for example a managed
    /// identity granted only table <em>data</em> roles). Call <see cref="PrepareAsync(string, CancellationToken)"/>
    /// once beforehand to provision the table.
    /// </remarks>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<AzureStorageRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Opens the registry for operation over a caller-supplied service client.</summary>
    /// <remarks>
    /// Supply a client the caller configured — for example with a managed identity / <c>TokenCredential</c> holding
    /// only data-plane roles — so the registry runs under a least-privileged principal with no key in a connection
    /// string. This creates no table; call <see cref="PrepareAsync(TableServiceClient, CancellationToken)"/> once
    /// beforehand.
    /// </remarks>
    /// <param name="tableService">A table service client.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<AzureStorageRunnerRegistry> ConnectAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        TableClient runners = tableService.GetTableClient(RunnersTable);
        return new ValueTask<AzureStorageRunnerRegistry>(new AzureStorageRunnerRegistry(runners));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        TableEntity entity = BuildEntity(registration);
        await this.runners.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        NullableResponse<TableEntity> existing = await this.runners
            .GetEntityIfExistsAsync<TableEntity>(PartitionKey, runnerId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!existing.HasValue)
        {
            return false;
        }

        byte[] doc = existing.Value!.GetBinary("Doc") ?? [];
        RunnerRegistration updated = RunnerRegistration.FromJson(doc).WithLastSeenAt(at);
        await this.runners.UpsertEntityAsync(BuildEntity(updated), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey}");
        var result = new List<RunnerRegistration>();
        await foreach (TableEntity entity in this.runners.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson(entity.GetBinary("Doc") ?? []));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        long cutoff = deadBefore.ToUnixTimeMilliseconds();
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey} and LastSeenAt lt {cutoff}");
        int removed = 0;
        await foreach (TableEntity entity in this.runners.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            await this.runners.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken).ConfigureAwait(false);
            removed++;
        }

        return removed;
    }

    private static TableEntity BuildEntity(RunnerRegistration registration)
        => new(PartitionKey, registration.RunnerIdValue)
        {
            ["LastSeenAt"] = registration.LastSeenAtValue.ToUnixTimeMilliseconds(),
            ["Doc"] = registration.ToJsonBytes(),
        };
}