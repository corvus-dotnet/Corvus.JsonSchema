// <copyright file="AzureStorageRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
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
/// <para>
/// The <c>Doc</c> property is the canonical record — the registration round-trips through it unchanged, exactly as
/// every other backend keeps the JSON verbatim. Provision the table once with
/// <see cref="PrepareAsync(string, CancellationToken)"/>, then open the registry with
/// <see cref="ConnectAsync(string, CancellationToken)"/>.
/// </para>
/// <para>
/// Azure Table cannot query into the JSON <c>Doc</c>, so a second "hosting" index table answers
/// <see cref="IsVersionHostedAsync"/> with a single partition query. Each loaded hosted (base, version) of a
/// runner is projected into one index entity whose PartitionKey encodes the (base, version) pair and whose RowKey
/// is the runner id; <see cref="RegisterAsync"/> re-projects a runner's index entities and <see cref="PruneAsync"/>
/// removes them, mirroring the SQL backends' hosted-versions table.
/// </para>
/// </remarks>
public sealed class AzureStorageRunnerRegistry : IRunnerRegistry
{
    private const string RunnersTable = "arazzoRunners";
    private const string HostingTable = "arazzoRunnerHosting";
    private const string PartitionKey = "runner";

    private readonly TableClient runners;
    private readonly TableClient hosting;

    private AzureStorageRunnerRegistry(TableClient runners, TableClient hosting)
    {
        this.runners = runners;
        this.hosting = hosting;
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
        await tableService.GetTableClient(HostingTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
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
        TableClient hosting = tableService.GetTableClient(HostingTable);
        return new ValueTask<AzureStorageRunnerRegistry>(new AzureStorageRunnerRegistry(runners, hosting));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;

        // Re-project this runner's hosting index. The index lives in a separate table (Azure cannot query into the
        // JSON Doc), so first read the runner's existing entity to learn its OLD loaded hosted versions and delete
        // those index entities, then upsert the runner entity and add one index entity per NEW loaded version.
        NullableResponse<TableEntity> existing = await this.runners
            .GetEntityIfExistsAsync<TableEntity>(PartitionKey, runnerId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (existing.HasValue)
        {
            RunnerRegistration old = RunnerRegistration.FromJson(existing.Value!.GetBinary("Doc") ?? []);
            foreach ((string baseWorkflowId, int versionNumber) in old.LoadedHostedVersions())
            {
                await this.DeleteHostingEntityAsync(baseWorkflowId, versionNumber, runnerId, cancellationToken).ConfigureAwait(false);
            }
        }

        var buffer = new ArrayBufferWriter<byte>();
        registration.WriteTo(buffer);
        byte[] doc = buffer.WrittenSpan.ToArray();
        var entity = new TableEntity(PartitionKey, runnerId)
        {
            ["LastSeenAt"] = registration.LastSeenAtValue.ToUnixTimeMilliseconds(),
            ["Doc"] = doc,
        };
        await this.runners.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            var index = new TableEntity(HostingPartition(baseWorkflowId, versionNumber), runnerId);
            await this.hosting.UpsertEntityAsync(index, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        string partition = HostingPartition(baseWorkflowId, versionNumber);
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {partition}");
        IAsyncEnumerator<TableEntity> enumerator = this.hosting
            .QueryAsync<TableEntity>(filter, maxPerPage: 1, select: ["PartitionKey"], cancellationToken: cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            return await enumerator.MoveNextAsync().ConfigureAwait(false);
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
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
        RunnerRegistration current = RunnerRegistration.FromJson(doc);
        var buffer = new ArrayBufferWriter<byte>();
        current.WriteWithLastSeenAt(buffer, at);
        byte[] json = buffer.WrittenSpan.ToArray();
        var entity = new TableEntity(PartitionKey, runnerId)
        {
            ["LastSeenAt"] = at.ToUnixTimeMilliseconds(),
            ["Doc"] = json,
        };
        await this.runners.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
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
            // Remove this runner's hosting index entities (derived from its stored Doc) before deleting it.
            RunnerRegistration stale = RunnerRegistration.FromJson(entity.GetBinary("Doc") ?? []);
            foreach ((string baseWorkflowId, int versionNumber) in stale.LoadedHostedVersions())
            {
                await this.DeleteHostingEntityAsync(baseWorkflowId, versionNumber, entity.RowKey, cancellationToken).ConfigureAwait(false);
            }

            await this.runners.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, ETag.All, cancellationToken).ConfigureAwait(false);
            removed++;
        }

        return removed;
    }

    /// <summary>
    /// Builds the hosting-index PartitionKey for a (base workflow id, version) pair. The base id is Base64Url-encoded
    /// so the key never contains a character Azure forbids in PartitionKey/RowKey (<c>/ \ # ?</c> and control chars).
    /// </summary>
    private static string HostingPartition(string baseWorkflowId, int versionNumber)
    {
        string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(baseWorkflowId)).Replace('/', '_').Replace('+', '-');
        return $"{encoded}|{versionNumber}";
    }

    private async ValueTask DeleteHostingEntityAsync(string baseWorkflowId, int versionNumber, string runnerId, CancellationToken cancellationToken)
    {
        try
        {
            await this.hosting
                .DeleteEntityAsync(HostingPartition(baseWorkflowId, versionNumber), runnerId, ETag.All, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // The index entity was already absent — nothing to remove.
        }
    }
}