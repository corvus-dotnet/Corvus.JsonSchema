// <copyright file="WorkflowDispatcherTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves store-as-queue dispatch end to end: a fresh Pending run (and a lease-expired Running orphan) is
/// claimed by the <see cref="WorkflowDispatcher"/>, leased (CAS — a held run is skipped), and driven to
/// completion through the hosted-workflow resumer over a real catalogued executor.
/// </summary>
[TestClass]
public class WorkflowDispatcherTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string WorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string PetstoreOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": {
                "operationId": "getPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Claims_a_pending_run_and_drives_it_to_completion()
    {
        var clock = new MutableClock(T0);
        IWorkflowCatalogStore catalog = await RunnableCatalogAsync();
        var runStore = new InMemoryWorkflowStateStore(clock);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using (WorkflowRun pending = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement, clock))
        {
            await pending.EnqueueAsync(default);
        }

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-1", clock);

        int dispatched = await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default);

        dispatched.ShouldBe(1);
        transport.Requests[0].Path.ShouldBe("/pets/42");

        // The run is now Completed and no longer claimable.
        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(runStore, "run-1", clock, default);
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Completed);
        (await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default)).ShouldBe(0);
    }

    [TestMethod]
    public async Task Skips_a_run_another_runner_holds()
    {
        var clock = new MutableClock(T0);
        IWorkflowCatalogStore catalog = await RunnableCatalogAsync();
        var runStore = new InMemoryWorkflowStateStore(clock);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using (WorkflowRun pending = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement, clock))
        {
            await pending.EnqueueAsync(default);
        }

        // Another runner holds an unexpired lease.
        await runStore.AcquireLeaseAsync("run-1", "other-runner", TimeSpan.FromMinutes(5), default);

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)new MockApiTransport(), System.StringComparer.Ordinal), null));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-1", clock);

        (await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default)).ShouldBe(0);
    }

    [TestMethod]
    public async Task Reclaims_a_running_orphan_whose_lease_expired()
    {
        var clock = new MutableClock(T0);
        IWorkflowCatalogStore catalog = await RunnableCatalogAsync();
        var runStore = new InMemoryWorkflowStateStore(clock);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using (WorkflowRun orphan = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement, clock))
        {
            // A crashed runner left it Running at cursor 0 and never released its lease.
            await orphan.CheckpointAsync(0, default);
        }

        await runStore.AcquireLeaseAsync("dead-runner", "dead-runner", TimeSpan.FromMinutes(1), default);
        await runStore.AcquireLeaseAsync("run-1", "dead-runner", TimeSpan.FromMinutes(1), default);
        clock.Advance(TimeSpan.FromMinutes(2)); // the dead runner's lease has now expired

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-2", clock);

        int dispatched = await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default);

        dispatched.ShouldBe(1);
        using WorkflowRun? reloaded = await WorkflowRun.ResumeAsync(runStore, "run-1", clock, default);
        reloaded!.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task A_closed_dispatch_gate_claims_nothing_then_an_open_gate_dispatches()
    {
        var clock = new MutableClock(T0);
        IWorkflowCatalogStore catalog = await RunnableCatalogAsync();
        var runStore = new InMemoryWorkflowStateStore(clock);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using (WorkflowRun pending = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement, clock))
        {
            await pending.EnqueueAsync(default);
        }

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        // The §5.5 authorization gate: while the runner is not authorized for its environment it claims nothing, even
        // though a Pending run is waiting for a version it hosts. The run stays Pending (claimable by an authorized peer).
        bool authorized = false;
        var dispatcher = new WorkflowDispatcher(runStore, "runner-1", clock, dispatchGate: _ => ValueTask.FromResult(authorized));

        (await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default)).ShouldBe(0);
        using (WorkflowRun? stillPending = await WorkflowRun.ResumeAsync(runStore, "run-1", clock, default))
        {
            stillPending!.Status.ShouldBe(WorkflowRunStatus.Pending);
        }

        transport.Requests.Count.ShouldBe(0, "a gated runner must not even reach the transport");

        // Once authorized, the same dispatcher claims and drives the run to completion.
        authorized = true;
        (await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default)).ShouldBe(1);
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, "run-1", clock, default);
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Claims_only_runs_pinned_to_the_runners_environment_and_environment_round_trips()
    {
        var clock = new MutableClock(T0);
        IWorkflowCatalogStore catalog = await RunnableCatalogAsync();
        var runStore = new InMemoryWorkflowStateStore(clock);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using (WorkflowRun prod = WorkflowRun.CreateNew(runStore, "run-prod", "adopt-v1", inputs.RootElement, clock, environment: "production"))
        {
            await prod.EnqueueAsync(default);
        }

        using (WorkflowRun staging = WorkflowRun.CreateNew(runStore, "run-staging", "adopt-v1", inputs.RootElement, clock, environment: "staging"))
        {
            await staging.EnqueueAsync(default);
        }

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        // §5.5: a runner serving production claims only the production-pinned run; the staging run is left for a staging runner.
        var dispatcher = new WorkflowDispatcher(runStore, "runner-prod", clock, runnerEnvironment: "production");
        (await dispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default)).ShouldBe(1);

        using (WorkflowRun? prodRun = await WorkflowRun.ResumeAsync(runStore, "run-prod", clock, default))
        {
            prodRun!.Status.ShouldBe(WorkflowRunStatus.Completed);
            prodRun.Environment.ShouldBe("production"); // the pinned environment round-trips through the checkpoint
        }

        using (WorkflowRun? stagingRun = await WorkflowRun.ResumeAsync(runStore, "run-staging", clock, default))
        {
            stagingRun!.Status.ShouldBe(WorkflowRunStatus.Pending); // not claimed by the production runner
            stagingRun.Environment.ShouldBe("staging");
        }

        // A staging runner then claims the staging run.
        var stagingDispatcher = new WorkflowDispatcher(runStore, "runner-staging", clock, runnerEnvironment: "staging");
        (await stagingDispatcher.DispatchClaimableAsync(["adopt-v1"], resumer.AsResumer(), default)).ShouldBe(1);
        using WorkflowRun? nowDone = await WorkflowRun.ResumeAsync(runStore, "run-staging", clock, default);
        nowDone!.Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    [TestMethod]
    public async Task Ignores_runs_for_versions_it_does_not_host()
    {
        var clock = new MutableClock(T0);
        IWorkflowCatalogStore catalog = await RunnableCatalogAsync();
        var runStore = new InMemoryWorkflowStateStore(clock);

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using (WorkflowRun pending = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement, clock))
        {
            await pending.EnqueueAsync(default);
        }

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)new MockApiTransport(), System.StringComparer.Ordinal), null));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-1", clock);

        (await dispatcher.DispatchClaimableAsync(["other-v3"], resumer.AsResumer(), default)).ShouldBe(0);
    }

    private static async Task<IWorkflowCatalogStore> RunnableCatalogAsync()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        byte[] package = WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))]);
        await catalog.AddAsync("adopt", package, new CatalogMetadata(new CatalogOwner("Team", "team@example.com"), "alice"), default);
        return catalog;
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }
}
