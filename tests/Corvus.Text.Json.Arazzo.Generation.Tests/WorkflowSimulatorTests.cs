// <copyright file="WorkflowSimulatorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves <see cref="WorkflowSimulator"/> (workflow-designer design §8): deterministic replay over
/// the real compile path with the structured trace — step records with exchanges, post-hoc criterion
/// truth tables, inferred routing — plus stateless stepping (pause-before-step, breakpoints),
/// the step budget, retry clock advances, and the content-hash compile cache.
/// </summary>
[TestClass]
public sealed class WorkflowSimulatorTests
{
    private const string WorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "get-pet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "give-up", "type": "end", "criteria": [ { "condition": "$statusCode == 404" } ] } ],
                  "outputs": { "petName": "$response.body#/name" }
                },
                {
                  "stepId": "adopt-pet",
                  "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ],
              "outputs": { "name": "$steps.get-pet.outputs.petName" }
            }
          ]
        }
        """;

    // No workflow-level outputs: they reference step outputs, which never materialise on the
    // failure path, and evaluating them at the end-action completion FAULTS (engine semantics —
    // the happy-path document keeps its outputs to prove extraction; this one proves routing).
    private const string FailureWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "GiveUp", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "give-up-flow",
              "steps": [
                {
                  "stepId": "get-pet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "give-up", "type": "end", "criteria": [ { "condition": "$statusCode == 404" } ] } ]
                },
                {
                  "stepId": "adopt-pet",
                  "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ]
                }
              ]
            }
          ]
        }
        """;

    private const string RetryWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Retry", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "retrying",
              "steps": [
                {
                  "stepId": "get-pet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "again", "type": "retry", "retryAfter": 5, "retryLimit": 3 } ]
                }
              ]
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
                "responses": {
                  "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } },
                  "404": { "description": "unknown pet" },
                  "503": { "description": "unavailable" },
                  "default": { "description": "unexpected" }
                }
              }
            },
            "/pets/{petId}/adopt": {
              "post": {
                "operationId": "adoptPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "adopted" } }
              }
            }
          }
        }
        """;

    private static readonly byte[] WorkflowUtf8 = Encoding.UTF8.GetBytes(WorkflowJson);
    private static readonly byte[] RetryWorkflowUtf8 = Encoding.UTF8.GetBytes(RetryWorkflowJson);
    private static readonly byte[] FailureWorkflowUtf8 = Encoding.UTF8.GetBytes(FailureWorkflowJson);
    private static readonly IReadOnlyList<KeyValuePair<string, byte[]>> Sources =
        [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))];

    private static ParsedJsonDocument<JsonElement>? sharedBody;

    private static SimulationScenario HappyScenario(ParsedJsonDocument<JsonElement> inputs)
    {
        sharedBody ??= ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        return new()
        {
            Inputs = inputs.RootElement,
            Mocks =
            [
                new("get", "/pets/{petId}", 200, sharedBody.RootElement),
                new("post", "/pets/{petId}/adopt", 200, default),
            ],
        };
    }

    [TestMethod]
    public async Task A_full_replay_traces_steps_exchanges_truth_tables_and_outputs()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks =
            [
                new("get", "/pets/{petId}", 200, body.RootElement),
                new("post", "/pets/{petId}/adopt", 200, default),
            ],
        };

        using SimulationResult result = await simulator.SimulateAsync(WorkflowUtf8, Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);
        result.Steps.Count.ShouldBe(2);
        result.StepsExecuted.ShouldBe(2);

        SimulatedStepRecord first = result.Steps[0];
        first.StepId.ShouldBe("get-pet");
        first.Faulted.ShouldBeFalse();
        first.ExchangeCount.ShouldBe(1);
        result.Exchanges[first.FirstExchange].Path.ShouldBe("/pets/42");
        result.Exchanges[first.FirstExchange].StatusCode.ShouldBe(200);
        first.SuccessCriteria.Count.ShouldBe(1);
        first.SuccessCriteria[0].Condition.ShouldBe("$statusCode == 200");
        first.SuccessCriteria[0].Satisfied.ShouldBeTrue();
        first.ActionTaken.ShouldNotBeNull();
        first.ActionTaken!.Value.Type.ShouldBe("fallThrough");
        first.Outputs.ValueKind.ShouldBe(JsonValueKind.Object);

        result.Steps[1].StepId.ShouldBe("adopt-pet");
        result.Outputs.ValueKind.ShouldBe(JsonValueKind.Object);
        result.Outputs.GetProperty("name").GetString().ShouldBe("Fido");
    }

    [TestMethod]
    public async Task A_matched_failure_action_ends_the_run_and_the_truth_table_shows_the_failed_criterion()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 404, default)],
        };

        using SimulationResult result = await simulator.SimulateAsync(FailureWorkflowUtf8, Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed, "the give-up end action completes the workflow");
        result.Steps.Count.ShouldBe(1, "adopt-pet never ran");
        result.Steps[0].SuccessCriteria[0].Satisfied.ShouldBeFalse();
        result.Steps[0].ActionTaken!.Value.Type.ShouldBe("end");
        result.Steps[0].ActionTaken!.Value.Name.ShouldBe("give-up");
    }

    [TestMethod]
    public async Task Pause_before_a_step_returns_the_trace_up_to_it_and_nothing_further_ran()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        SimulationScenario scenario = HappyScenario(inputs);

        using SimulationResult paused = await simulator.SimulateAsync(
            WorkflowUtf8, Sources, scenario, new SimulationStop { BeforeStepId = "adopt-pet" });

        paused.Outcome.ShouldBe(SimulationOutcome.Paused);
        paused.PausedBefore.ShouldBe("adopt-pet");
        paused.Steps.Count.ShouldBe(1, "only get-pet ran");
        paused.Exchanges.Count.ShouldBe(1, "the paused step never touched the transport");

        // Pause before the FIRST step: nothing runs at all.
        using SimulationResult atStart = await simulator.SimulateAsync(
            WorkflowUtf8, Sources, scenario, new SimulationStop { Breakpoints = new HashSet<string> { "get-pet" } });
        atStart.Outcome.ShouldBe(SimulationOutcome.Paused);
        atStart.PausedBefore.ShouldBe("get-pet");
        atStart.Steps.Count.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_retry_with_delay_advances_the_virtual_clock_and_records_each_attempt()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks =
            [
                new("get", "/pets/{petId}", 503, default),
                new("get", "/pets/{petId}", 503, default),
                new("get", "/pets/{petId}", 200, body.RootElement),
            ],
        };

        using SimulationResult result = await simulator.SimulateAsync(RetryWorkflowUtf8, Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);
        result.ClockAdvances.Count.ShouldBe(2, "two failed attempts, each retryAfter=5s");
        result.Steps.Count.ShouldBe(3, "each attempt is a trace record");
        result.Steps.All(s => s.StepId == "get-pet").ShouldBeTrue();
        result.Steps[2].SuccessCriteria[0].Satisfied.ShouldBeTrue();

        // Nothing waited in real time: the clock advanced 10 virtual seconds.
        (result.ClockAdvances[1].To - result.ClockAdvances[0].To).ShouldBe(TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task Auto_advance_off_suspends_at_the_first_timer_with_its_due_moment()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 503, default)],
            AutoAdvanceClock = false,
        };

        using SimulationResult result = await simulator.SimulateAsync(RetryWorkflowUtf8, Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Suspended);
        result.Wait.ShouldNotBeNull();
        result.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Timer);
        result.Wait!.Value.DueAt.ShouldBe(new DateTimeOffset(2020, 1, 1, 0, 0, 5, TimeSpan.Zero), "the fixed epoch plus retryAfter");
    }

    [TestMethod]
    public async Task The_step_budget_halts_a_runaway_retry_loop()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 503, default)],
        };

        using SimulationResult result = await simulator.SimulateAsync(
            RetryWorkflowUtf8, Sources, scenario, budget: new SimulationBudget { MaxSteps = 2 });

        result.Outcome.ShouldBe(SimulationOutcome.BudgetExhausted);
        result.StepsExecuted.ShouldBeGreaterThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task An_overridden_step_is_skipped_and_its_provided_outputs_stand()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> provided = ParsedJsonDocument<JsonElement>.Parse("""{"petName":"Rex"}"""u8.ToArray());

        // Only adopt-pet is mocked: the overridden get-pet must never touch the transport.
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("post", "/pets/{petId}/adopt", 200, default)],
            StepOutputOverrides = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["get-pet"] = provided.RootElement },
        };

        using SimulationResult result = await simulator.SimulateAsync(WorkflowUtf8, Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);
        result.Steps.Count.ShouldBe(2);
        result.StepsExecuted.ShouldBe(2, "a skipped step still counts against the budget");

        SimulatedStepRecord skipped = result.Steps[0];
        skipped.StepId.ShouldBe("get-pet");
        skipped.Skipped.ShouldBeTrue();
        skipped.Attempt.ShouldBe(0);
        skipped.Faulted.ShouldBeFalse();
        skipped.ExchangeCount.ShouldBe(0, "the overridden step never executed its exchange");
        skipped.SuccessCriteria.Count.ShouldBe(0, "no exchange, no truth table");
        skipped.ActionTaken.ShouldNotBeNull();
        skipped.ActionTaken!.Value.Type.ShouldBe("fallThrough", "no criteria-less success action is declared");
        skipped.Outputs.GetProperty("petName").GetString().ShouldBe("Rex");

        result.Steps[1].StepId.ShouldBe("adopt-pet");
        result.Steps[1].Skipped.ShouldBeFalse();
        result.Exchanges.Count.ShouldBe(1, "only adopt-pet exchanged");

        // The workflow outputs read $steps.get-pet.outputs.petName through the REAL executor —
        // the provided outputs stand for downstream resolution.
        result.Outputs.GetProperty("name").GetString().ShouldBe("Rex");
    }

    [TestMethod]
    public async Task A_breakpoint_on_an_overridden_step_still_pauses_first()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> provided = ParsedJsonDocument<JsonElement>.Parse("""{"petName":"Rex"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("post", "/pets/{petId}/adopt", 200, default)],
            StepOutputOverrides = new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["get-pet"] = provided.RootElement },
        };

        using SimulationResult result = await simulator.SimulateAsync(
            WorkflowUtf8, Sources, scenario, new SimulationStop { Breakpoints = new HashSet<string> { "get-pet" } });

        result.Outcome.ShouldBe(SimulationOutcome.Paused);
        result.PausedBefore.ShouldBe("get-pet");
        result.Steps.Count.ShouldBe(0, "the pause wins over the override: nothing was skipped or executed");
    }

    [TestMethod]
    public async Task The_compile_cache_builds_once_per_document_state()
    {
        var counting = new CountingProvider(new WorkflowExecutorProvider(durable: true));
        using var simulator = new WorkflowSimulator(counting);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        SimulationScenario scenario = HappyScenario(inputs);

        using (await simulator.SimulateAsync(WorkflowUtf8, Sources, scenario))
        {
        }

        using (await simulator.SimulateAsync(WorkflowUtf8, Sources, scenario, new SimulationStop { BeforeStepId = "adopt-pet" }))
        {
        }

        counting.Builds.ShouldBe(1, "the second command replayed the cached executor");

        using (await simulator.SimulateAsync(RetryWorkflowUtf8, Sources, scenario))
        {
        }

        counting.Builds.ShouldBe(2, "a different document state compiles once more");
    }

    [TestMethod]
    public async Task An_uncompilable_document_reports_not_executable()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        byte[] broken = Encoding.UTF8.GetBytes("""{"arazzo":"1.1.0","info":{"title":"x","version":"1"},"workflows":[]}""");

        using SimulationResult result = await simulator.SimulateAsync(broken, [], new SimulationScenario());

        result.Outcome.ShouldBe(SimulationOutcome.NotExecutable);
        result.Steps.Count.ShouldBe(0);
    }

    private sealed class CountingProvider(IWorkflowExecutorProvider inner) : IWorkflowExecutorProvider
    {
        public int Builds { get; private set; }

        public WorkflowExecutorArtifact? BuildExecutor(
            ReadOnlyMemory<byte> workflowUtf8,
            IReadOnlyList<KeyValuePair<string, byte[]>> sources,
            string packageHash)
        {
            this.Builds++;
            return inner.BuildExecutor(workflowUtf8, sources, packageHash);
        }
    }
}