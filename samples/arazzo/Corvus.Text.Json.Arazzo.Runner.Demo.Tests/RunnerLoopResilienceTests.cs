// <copyright file="RunnerLoopResilienceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using VaultSharp;

namespace Corvus.Text.Json.Arazzo.Runner.Demo.Tests;

/// <summary>
/// The resilience contract shared by every runner background loop: a transient fault inside one iteration is
/// logged and the next tick retries — it never faults <c>ExecuteTask</c>, because the host's default
/// BackgroundServiceExceptionBehavior is StopHost and a faulted loop terminates the whole runner (the live
/// failure that took down the runner AND the system-runner on one Npgsql read timeout).
/// </summary>
[TestClass]
public sealed class RunnerLoopResilienceTests
{
    [TestMethod]
    public async Task Dispatch_loop_survives_store_faults_and_keeps_polling()
    {
        var catalogStore = new ThrowingCatalogStore();
        var catalog = new SecuredWorkflowCatalog(catalogStore, new StubWaitIndex(), "test");
        using var service = new WorkflowDispatchService(
            new StubStateStore(),
            new StubAuthorizations(),
            catalog,
            resumer: null!, // never reached: the catalog read faults first, and the loop must absorb that
            new RunnerOptions("runner-under-test", "development"),
            NullLogger<WorkflowDispatchService>.Instance);

        await service.StartAsync(CancellationToken.None);

        // The poll interval is 2s and the first cycle runs immediately: two observed catalog reads prove the
        // loop survived the first fault and came back for another cycle.
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (catalogStore.Queries < 2 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse($"a transient store fault must not fault (and so stop) the runner host: {executeTask.Exception}");
        catalogStore.Queries.ShouldBeGreaterThanOrEqualTo(2, "the dispatch loop must keep polling through transient store faults");

        await service.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task Vault_refresh_loop_survives_client_faults_and_keeps_ticking()
    {
        var vault = new ThrowingVault();
        using var service = new VaultTokenLifecycleService(vault, NullLogger<VaultTokenLifecycleService>.Instance, refreshInterval: TimeSpan.FromMilliseconds(10));

        await service.StartAsync(CancellationToken.None);

        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (vault.Accesses < 3 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        vault.Accesses.ShouldBeGreaterThanOrEqualTo(3, "the refresh loop must keep ticking through vault faults");
        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse("a vault fault must not fault (and so stop) the runner host");

        await service.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task Draft_run_pump_survives_runner_faults_and_keeps_polling()
    {
        // A null runner makes every RunPendingAsync throw — the pump must absorb each fault and keep polling.
        using var service = new DraftRunPumpService(runner: null!, NullLogger<DraftRunPumpService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await Task.Delay(700); // several 200ms poll cycles, each faulting

        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse("a pump fault must not fault (and so stop) the runner host");

        await service.StopAsync(CancellationToken.None);
    }

    private sealed class ThrowingCatalogStore : IWorkflowCatalogStore
    {
        private int queries;

        public int Queries => this.queries;

        public ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
        {
            this.queries++;
            throw new TimeoutException("Timeout during reading attempt");
        }

        public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubWaitIndex : IWorkflowWaitIndex
    {
        public ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken) => throw new NotSupportedException();

        public IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubStateStore : IWorkflowStateStore, IWorkflowDispatchIndex, IWorkflowWaitIndex
    {
        public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, CancellationToken cancellationToken) => throw new NotSupportedException();

        public IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<WorkflowEtag> SaveAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpointUtf8, in WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubAuthorizations : IEnvironmentRunnerAuthorizationStore
    {
        public ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, string? principal, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ThrowingVault : IVaultClient
    {
        private int accesses;

        public int Accesses => this.accesses;

        public VaultSharp.V1.IVaultClientV1 V1
        {
            get
            {
                this.accesses++;
                throw new TimeoutException("Timeout during reading attempt");
            }
        }

        public VaultClientSettings Settings => throw new NotSupportedException();
    }
}
