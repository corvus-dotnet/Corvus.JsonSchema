// <copyright file="RunnerRegistrationServiceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Corvus.Text.Json.Arazzo.Runner.Demo.Tests;

/// <summary>
/// The runner's registration/heartbeat loop must survive transient store faults: the host's default
/// BackgroundServiceExceptionBehavior is StopHost, so an unguarded heartbeat exception terminates the whole
/// runner (observed live: one Npgsql read timeout took down the runner AND the system-runner). A timeout is
/// logged and the next tick retries; the loop never faults.
/// </summary>
[TestClass]
public sealed class RunnerRegistrationServiceTests
{
    [TestMethod]
    public async Task Heartbeat_timeouts_are_survived_and_the_loop_keeps_ticking()
    {
        // Two consecutive read timeouts, then a healthy store again.
        var registry = new FlakyRegistry(failures: 2);
        using var service = new RunnerRegistrationService(
            registry,
            environments: null!,
            runnerAuthorizations: null!,
            EmptyCatalog(),
            new RunnerOptions("runner-under-test", "development"),
            NullLogger<RunnerRegistrationService>.Instance,
            providedIsolation: RunIsolationModel.InProcess,
            registrar: null,
            heartbeatInterval: TimeSpan.FromMilliseconds(10));

        await service.StartAsync(CancellationToken.None);

        // The loop outlives the faulted ticks: it reaches the healthy store on tick 3 and keeps going.
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (registry.Heartbeats < 4 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        registry.Heartbeats.ShouldBeGreaterThanOrEqualTo(4, "the heartbeat loop must keep ticking through transient timeouts");
        Task executeTask = service.ExecuteTask.ShouldNotBeNull();
        executeTask.IsFaulted.ShouldBeFalse("a transient timeout must not fault (and so stop) the runner host");

        await service.StopAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task A_runner_advertising_more_isolation_than_its_backend_provides_refuses_to_start()
    {
        // A deploy misconfiguration: the runner advertises Isolated to the control plane but its wired execution backend
        // provides only InProcess. The advertise-vs-wire fence (ADR 0058) is fatal — the runner refuses to start rather than
        // register a lie that would pass every control-plane check yet run isolated-required work in-process.
        using var service = new RunnerRegistrationService(
            new FlakyRegistry(failures: 0),
            environments: null!,
            runnerAuthorizations: null!,
            EmptyCatalog(),
            new RunnerOptions("mis-wired-runner", "development", IsolationModel: "Isolated"),
            NullLogger<RunnerRegistrationService>.Instance,
            providedIsolation: RunIsolationModel.InProcess,
            registrar: null,
            heartbeatInterval: TimeSpan.FromMilliseconds(10));

        // The fence faults ExecuteAsync before it registers; the fault surfaces on the ExecuteTask (a BackgroundService
        // captures a pre-await throw into its returned task rather than throwing out of StartAsync).
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!;
        });
        ex.Message.ShouldContain("advertises Isolated");
        ex.Message.ShouldContain("provides only InProcess");
    }

    [TestMethod]
    public async Task Registering_through_the_control_plane_without_a_runner_api_refuses_to_start()
    {
        // The versions a runner advertises are the ones the control plane resolves for its machine principal (ADR 0065),
        // so the authenticated topology has to be able to ask. Without the client it would register an empty set
        // forever, and the control plane's IsVersionHostedAsync would believe it — a silent failure to be dispatchable
        // that looks exactly like "no work available".
        using var service = new RunnerRegistrationService(
            new FlakyRegistry(failures: 0),
            environments: null!,
            runnerAuthorizations: null!,
            catalog: null,
            new RunnerOptions("api-less-runner", "development"),
            NullLogger<RunnerRegistrationService>.Instance,
            providedIsolation: RunIsolationModel.InProcess,
            registrar: Registrar(),
            heartbeatInterval: TimeSpan.FromMilliseconds(10),
            runnerApi: null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!;
        });

        ex.Message.ShouldContain("no runner-API client");
    }

    [TestMethod]
    public async Task Registering_store_direct_without_a_catalog_refuses_to_start()
    {
        // The store-direct topology has no control plane to ask, so it reads the catalog itself. With neither source the
        // runner would heartbeat forever and never register, because a registration failure is non-fatal by design —
        // which is exactly the shape of failure that hides.
        using var service = new RunnerRegistrationService(
            new FlakyRegistry(failures: 0),
            environments: null!,
            runnerAuthorizations: null!,
            catalog: null,
            new RunnerOptions("catalog-less-runner", "development"),
            NullLogger<RunnerRegistrationService>.Instance,
            providedIsolation: RunIsolationModel.InProcess,
            registrar: null,
            heartbeatInterval: TimeSpan.FromMilliseconds(10));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await service.StartAsync(CancellationToken.None);
            await service.ExecuteTask!;
        });

        ex.Message.ShouldContain("no catalog");
    }

    // A registrar that is never called: the guard under test runs before any registration attempt, so this only has to
    // exist.
    private static ControlPlaneRunnerRegistrar Registrar()
        => new(new HttpClient { BaseAddress = new Uri("https://control-plane.invalid") }, "https://control-plane.invalid", "development", "https://idp.invalid/token", "client", "secret");

    // An empty catalog over in-memory stores: enough to satisfy the store-direct topology's requirement without the test
    // caring what is in it.
    private static SecuredWorkflowCatalog EmptyCatalog()
    {
        var runs = new InMemoryWorkflowStateStore();
        return new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), runs, "test");
    }

    private sealed class FlakyRegistry(int failures) : IRunnerRegistry
    {
        private int calls;

        public int Heartbeats => this.calls;

        public ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
        {
            this.calls++;
            if (this.calls <= failures)
            {
                throw new TimeoutException("Timeout during reading attempt");
            }

            return ValueTask.FromResult(true);
        }

        public ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, RunIsolationModel requiredIsolation, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}