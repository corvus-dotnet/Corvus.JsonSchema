// <copyright file="RunnerRegistryPruneServiceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// P1-12: a runner whose heartbeat has gone stale is pruned by the hosted sweep, so it stops satisfying the hosting
/// gates, and each prune is audited and counted.
/// </summary>
[TestClass]
public sealed class RunnerRegistryPruneServiceTests
{
    [TestMethod]
    [Timeout(30000)]
    public async Task A_stale_runner_is_pruned_within_the_window_and_stops_satisfying_the_hosting_gate()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var registry = new InMemoryRunnerRegistry();
        await registry.RegisterAsync(Runner("dead-runner", lastSeenAt: now.AddMinutes(-2)), default);
        await registry.RegisterAsync(Runner("live-runner", lastSeenAt: now), default);
        (await registry.IsVersionHostedAsync("wf", 1, "production", RunIsolationModel.InProcess, default)).ShouldBeTrue();

        var sink = new InMemoryAuditSink();
        GovernanceAuditor auditor = GovernanceAuditor.CreateInMemory(sink);
        var prune = new RunnerRegistryPruneService(registry, auditor, TimeSpan.FromMilliseconds(50), RunnerRegistryPruneService.DefaultDeadAfter, logger: null);
        await prune.StartAsync(default);
        try
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (await registry.GetAsync("dead-runner", default) is not null)
            {
                (DateTime.UtcNow < deadline).ShouldBeTrue("the stale runner was not pruned within the window");
                await Task.Delay(10);
            }
        }
        finally
        {
            await prune.StopAsync(default);
        }

        // The live runner hosts nothing, so the gate the dead one satisfied now refuses.
        (await registry.GetAsync("live-runner", default)).ShouldNotBeNull();
        (await registry.IsVersionHostedAsync("wf", 1, "production", RunIsolationModel.InProcess, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_sweep_records_each_pruned_runner_and_leaves_a_live_one_alone()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var registry = new InMemoryRunnerRegistry();
        await registry.RegisterAsync(Runner("dead-runner", lastSeenAt: now.AddMinutes(-2)), default);
        await registry.RegisterAsync(Runner("live-runner", lastSeenAt: now), default);
        var sink = new InMemoryAuditSink();
        GovernanceAuditor auditor = GovernanceAuditor.CreateInMemory(sink);
        var prune = new RunnerRegistryPruneService(registry, auditor, RunnerRegistryPruneService.DefaultInterval, RunnerRegistryPruneService.DefaultDeadAfter, logger: null);

        (await prune.SweepAsync(default)).ShouldBe(1);
        (await prune.SweepAsync(default)).ShouldBe(0);

        (await registry.GetAsync("dead-runner", default)).ShouldBeNull();
        (await registry.GetAsync("live-runner", default)).ShouldNotBeNull();
        string audit = string.Concat(sink.ChainIds.Select(id => Encoding.UTF8.GetString(sink.Snapshot(id))));
        audit.ShouldContain("runner.prune");
        audit.ShouldContain("dead-runner");
        audit.ShouldNotContain("live-runner");
    }

    [TestMethod]
    public void The_interval_and_the_window_must_be_positive()
    {
        var registry = new InMemoryRunnerRegistry();
        GovernanceAuditor auditor = GovernanceAuditor.CreateInMemory();
        Should.Throw<ArgumentOutOfRangeException>(() => new RunnerRegistryPruneService(registry, auditor, TimeSpan.Zero, TimeSpan.FromSeconds(45), logger: null));
        Should.Throw<ArgumentOutOfRangeException>(() => new RunnerRegistryPruneService(registry, auditor, TimeSpan.FromSeconds(15), TimeSpan.Zero, logger: null));
        RunnerRegistryPruneService.DefaultInterval.ShouldBe(TimeSpan.FromSeconds(15));
        RunnerRegistryPruneService.DefaultDeadAfter.ShouldBe(TimeSpan.FromSeconds(45));
    }

    private static RunnerRegistration Runner(string runnerId, DateTimeOffset lastSeenAt)
    {
        string startedAt = lastSeenAt.AddMinutes(-10).ToString("O", CultureInfo.InvariantCulture);
        string seen = lastSeenAt.ToString("O", CultureInfo.InvariantCulture);
        string json = $$"""
            {
              "runnerId": "{{runnerId}}",
              "environment": "production",
              "startedAt": "{{startedAt}}",
              "lastSeenAt": "{{seen}}",
              "maxConcurrency": 4,
              "transports": ["http"],
              "hostedVersions": [{{(runnerId == "dead-runner" ? """{ "baseWorkflowId": "wf", "versionNumber": 1, "hash": "sha256:abc", "loaded": true }""" : string.Empty)}}]
            }
            """;
        return RunnerRegistration.FromJson(Encoding.UTF8.GetBytes(json));
    }
}