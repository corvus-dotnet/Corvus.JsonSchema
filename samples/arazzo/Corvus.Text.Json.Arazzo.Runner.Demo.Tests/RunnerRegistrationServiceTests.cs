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
            catalog: null!, // registration is exercised elsewhere; its failure path is already non-fatal by design
            new RunnerOptions("runner-under-test", "development"),
            NullLogger<RunnerRegistrationService>.Instance,
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

        public ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
