// <copyright file="StoreControlPlaneCapacityGuardTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Capacity;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The standing capacity limits (ADR 0065 decision 3). These bound magnitudes rather than rates, so what matters is
/// that they measure what the store actually holds, that concurrency and storage are told apart, and that a limit left
/// unset enforces nothing.
/// </summary>
[TestClass]
public sealed class StoreControlPlaneCapacityGuardTests
{
    private const string Tenant = "acme";
    private const string Environment = "production";

    [TestMethod]
    public async Task An_unset_limit_enforces_nothing()
    {
        // Every limit is opt-in, and StoredRuns ships unset because nothing reclaims stored runs yet. A guard that
        // refused on an unset limit would make the default deployment unusable.
        Fixture fixture = await Fixture.WithRunsAsync(50, WorkflowRunStatus.Completed);
        var guard = fixture.Guard(new ControlPlaneCapacityOptions { StoredRunsPerTenant = 0 });

        (await guard.TryAdmitAsync(ControlPlaneCapacityKind.StoredRuns, Tenant, AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_stored_limit_counts_terminal_runs()
    {
        // The distinction that makes this a STORAGE bound: a completed run still holds its row, and there is no
        // automatic retention, so it counts. If it did not, the limit would bound nothing a tenant accumulates.
        Fixture fixture = await Fixture.WithRunsAsync(5, WorkflowRunStatus.Completed);
        var guard = fixture.Guard(new ControlPlaneCapacityOptions { StoredRunsPerTenant = 5 });

        ControlPlaneCapacityRejection refused = (await guard.TryAdmitAsync(ControlPlaneCapacityKind.StoredRuns, Tenant, AccessContext.System, default)).ShouldNotBeNull();
        refused.Quota.ShouldBe("run-count/tenant");
        refused.Counter.ShouldBe(Tenant);
        refused.Limit.ShouldBe(5);
    }

    [TestMethod]
    public async Task The_concurrency_limit_ignores_terminal_runs()
    {
        // The mirror image, and the reason both limits exist. A tenant sitting at zero concurrency with a pile of
        // finished runs is using no dispatch capacity at all, and a concurrency limit that counted them would refuse
        // work the deployment has ample room for.
        Fixture fixture = await Fixture.WithRunsAsync(50, WorkflowRunStatus.Completed);
        var guard = fixture.Guard(new ControlPlaneCapacityOptions { ConcurrentRunsPerTenant = 5 });

        (await guard.TryAdmitAsync(ControlPlaneCapacityKind.ConcurrentRuns, Tenant, AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_concurrency_limit_counts_every_in_flight_status()
    {
        // In flight is Pending OR Running OR Suspended, and WorkflowQuery carries one status at a time, so the guard
        // walks all three. Counting only one would let a tenant hold three times its limit by spreading across them.
        Fixture fixture = await Fixture.EmptyAsync();
        await fixture.SeedAsync(2, WorkflowRunStatus.Pending);
        await fixture.SeedAsync(2, WorkflowRunStatus.Running);
        await fixture.SeedAsync(2, WorkflowRunStatus.Suspended);

        var guard = fixture.Guard(new ControlPlaneCapacityOptions { ConcurrentRunsPerTenant = 6 });

        ControlPlaneCapacityRejection refused = (await guard.TryAdmitAsync(ControlPlaneCapacityKind.ConcurrentRuns, Tenant, AccessContext.System, default)).ShouldNotBeNull();
        refused.Quota.ShouldBe("concurrent-runs/tenant");
        refused.Observed.ShouldBe(6);
    }

    [TestMethod]
    public async Task There_is_room_below_the_limit()
    {
        Fixture fixture = await Fixture.WithRunsAsync(4, WorkflowRunStatus.Running);
        var guard = fixture.Guard(new ControlPlaneCapacityOptions { ConcurrentRunsPerTenant = 5 });

        (await guard.TryAdmitAsync(ControlPlaneCapacityKind.ConcurrentRuns, Tenant, AccessContext.System, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_limit_refuses_at_it_not_past_it()
    {
        // The check admits ONE MORE, so a tenant sitting exactly at its limit has no room. Refusing only above the
        // limit would let every tenant hold limit+1.
        Fixture fixture = await Fixture.WithRunsAsync(5, WorkflowRunStatus.Running);
        var guard = fixture.Guard(new ControlPlaneCapacityOptions { ConcurrentRunsPerTenant = 5 });

        (await guard.TryAdmitAsync(ControlPlaneCapacityKind.ConcurrentRuns, Tenant, AccessContext.System, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task The_runner_limit_counts_every_authorization_whatever_its_status()
    {
        // Counting only dispatchable runners would let a tenant accumulate unbounded rows by registering runners it
        // never gets authorized -- rows the store holds either way, which is what the cap exists to bound.
        Fixture fixture = await Fixture.EmptyAsync();
        await fixture.SeedRunnersAsync(3);

        var guard = fixture.Guard(new ControlPlaneCapacityOptions { RegisteredRunnersPerEnvironment = 3 });

        ControlPlaneCapacityRejection refused = (await guard.TryAdmitAsync(ControlPlaneCapacityKind.RegisteredRunners, Environment, AccessContext.System, default)).ShouldNotBeNull();
        refused.Quota.ShouldBe("registered-runners/environment");
        refused.Counter.ShouldBe(Environment);
    }

    [TestMethod]
    public async Task A_runner_limit_is_per_environment()
    {
        // The blast radius is drawn around the environment, so one environment filling up must not refuse another's
        // registrations.
        Fixture fixture = await Fixture.EmptyAsync();
        await fixture.SeedRunnersAsync(3);

        var guard = fixture.Guard(new ControlPlaneCapacityOptions { RegisteredRunnersPerEnvironment = 3 });

        (await guard.TryAdmitAsync(ControlPlaneCapacityKind.RegisteredRunners, "staging", AccessContext.System, default)).ShouldBeNull();
    }

    private sealed class Fixture
    {
        private readonly InMemoryWorkflowStateStore store;
        private readonly SecuredWorkflowManagement management;
        private readonly InMemoryEnvironmentRunnerAuthorizationStore runnerAuthorizations;
        private int seeded;

        private Fixture(InMemoryWorkflowStateStore store, SecuredWorkflowManagement management, InMemoryEnvironmentRunnerAuthorizationStore runnerAuthorizations)
        {
            this.store = store;
            this.management = management;
            this.runnerAuthorizations = runnerAuthorizations;
        }

        public static ValueTask<Fixture> EmptyAsync()
        {
            var store = new InMemoryWorkflowStateStore();
            return ValueTask.FromResult(new Fixture(store, new SecuredWorkflowManagement(store, "test"), new InMemoryEnvironmentRunnerAuthorizationStore()));
        }

        public static async ValueTask<Fixture> WithRunsAsync(int count, WorkflowRunStatus status)
        {
            Fixture fixture = await EmptyAsync();
            await fixture.SeedAsync(count, status);
            return fixture;
        }

        public StoreControlPlaneCapacityGuard Guard(ControlPlaneCapacityOptions options)
            => new(this.management, this.runnerAuthorizations, options);

        public async ValueTask SeedAsync(int count, WorkflowRunStatus status)
        {
            for (int i = 0; i < count; ++i)
            {
                string runId = $"run-{this.seeded++}";
                byte[] checkpoint = Checkpoint(runId, status);
                await this.store.SaveAsync(
                    new WorkflowRunId(runId),
                    checkpoint,
                    WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                    WorkflowEtag.None,
                    default);
            }
        }

        public async ValueTask SeedRunnersAsync(int count)
        {
            for (int i = 0; i < count; ++i)
            {
                // A mix of decided and undecided rows: the cap counts the row, not the decision.
                using ParsedJsonDocument<EnvironmentRunnerAuthorization> row =
                    await this.runnerAuthorizations.EnsurePendingAsync(Environment, $"runner-{i}", "test", $"principal-{i}", default);
            }
        }

        private static byte[] Checkpoint(string runId, WorkflowRunStatus status)
        {
            using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
            using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
            return WorkflowCheckpointSerializer.Serialize(
                new WorkflowRunId(runId),
                "capacity-v1",
                status,
                cursor: 0,
                sequence: 1,
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                retryCounters,
                new Dictionary<string, byte[]>(),
                inputs: default,
                stepOutputs,
                outputs: default,
                environment: Environment,
                updatedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }
    }
}