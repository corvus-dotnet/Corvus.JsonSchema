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

    // Defect 0 (pack 3 §15-8a): a parent workflow whose step invokes another workflow by workflowId.
    // The durable code generator emits the sub-workflow call with the wrong argument shape today (a token
    // positionally into the IWorkflowRun? parameter — CS1503), so the provider swallows the compile
    // failure and the simulation reports NotExecutable. It must compile and run.
    private const string SubWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Nested", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                { "stepId": "call-child", "workflowId": "child", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
              ]
            },
            {
              "workflowId": "child",
              "steps": [
                {
                  "stepId": "get-pet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            }
          ]
        }
        """;

    // Defect 0 (pack 3 §15-8a), goto-workflow half: a step whose onSuccess transfers control to another
    // workflow (type: goto with a workflowId). The durable tail-call at ControlFlowEmitter :1030 has the
    // same wrong argument shape (a token into the IWorkflowRun? slot — CS1503), so the provider swallows it
    // and reports NotExecutable. It must compile and run, returning the target workflow's result.
    private const string GotoWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Router", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "router",
              "steps": [
                {
                  "stepId": "check",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onSuccess": [ { "name": "handoff", "type": "goto", "workflowId": "target" } ]
                }
              ]
            },
            {
              "workflowId": "target",
              "steps": [
                {
                  "stepId": "get-pet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            }
          ]
        }
        """;

    [TestMethod]
    public async Task A_durable_simulation_of_a_sub_workflow_step_is_executable()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario);

        // NotExecutable would mean the durable codegen did not compile (the provider swallows the CS1503 into a
        // null artifact); Completed proves the sub-workflow was invoked, its outputs unwrapped, and the parent ran on.
        result.Outcome.ShouldBe(SimulationOutcome.Completed, "durable sub-workflow codegen must compile and run");
    }

    [TestMethod]
    public async Task A_durable_simulation_of_a_goto_workflow_action_is_executable()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(GotoWorkflowJson), Sources, scenario);

        // The same durable compile break lands on the goto-workflow tail-call (ControlFlowEmitter :1030); a
        // transferred-control run returns the target workflow's result as its own.
        result.Outcome.ShouldBe(SimulationOutcome.Completed, "durable goto-workflow codegen must compile and run");
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
    public async Task An_output_whose_pointer_misses_an_absent_field_is_omitted_not_crashed()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> emptyBody = ParsedJsonDocument<JsonElement>.Parse("{}"u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks =
            [
                // 200, but the body has no 'name' — so get-pet's petName ($response.body#/name) does not resolve.
                new("get", "/pets/{petId}", 200, emptyBody.RootElement),
                new("post", "/pets/{petId}/adopt", 200, default),
            ],
        };

        // Before OutputExtractionEmitter guarded each property, an unresolved output stayed a default
        // (Undefined) JsonElement, and adding it tripped the builder's Debug.Assert — terminating the process
        // in a Debug build. An unresolved output must simply be ABSENT and the run must complete.
        using SimulationResult result = await simulator.SimulateAsync(WorkflowUtf8, Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);
        result.StepsExecuted.ShouldBe(2);
        result.Steps[0].Outputs.ValueKind.ShouldBe(JsonValueKind.Object);
        result.Steps[0].Outputs.TryGetProperty("petName"u8, out _).ShouldBeFalse("the pointer missed, so petName is omitted");
        result.Outputs.ValueKind.ShouldBe(JsonValueKind.Object);
        result.Outputs.TryGetProperty("name"u8, out _).ShouldBeFalse("the workflow output derives from the absent petName, so it too is omitted");
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

    // Defect 1 (pack 3 §15-8a): a parent whose sub-workflow executes more steps than MaxSteps. The
    // child runs untracked today (null scope), so its steps escape the budget entirely and the
    // simulation completes; one global budget must count them and fault on exhaustion.
    private const string NestedBudgetWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "NestedBudget", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                { "stepId": "call-child", "workflowId": "child", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
              ]
            },
            {
              "workflowId": "child",
              "steps": [
                { "stepId": "first", "operationId": "getPet", "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ], "successCriteria": [ { "condition": "$statusCode == 200" } ] },
                { "stepId": "second", "operationId": "getPet", "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ], "successCriteria": [ { "condition": "$statusCode == 200" } ] },
                { "stepId": "third", "operationId": "getPet", "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ], "successCriteria": [ { "condition": "$statusCode == 200" } ] }
              ]
            }
          ]
        }
        """;

    // Defect 2 (pack 3 §15-8a): the retry fixture one level down — the child's retryAfter timer must
    // ride the shared ManualTimeProvider (suspension bubbling to the root, auto-advance re-entering,
    // the child replayed fresh per invocation). Today the child runs untracked: no suspension surface,
    // nothing advances its clock.
    private const string NestedTimerWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "NestedTimer", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                { "stepId": "call-child", "workflowId": "child", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
              ]
            },
            {
              "workflowId": "child",
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

    [TestMethod]
    public async Task Sub_workflow_steps_count_against_the_one_global_budget()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(
            Encoding.UTF8.GetBytes(NestedBudgetWorkflowJson), Sources, scenario, budget: new SimulationBudget { MaxSteps = 2 });

        // The child's three steps must count against the parent's MaxSteps=2 (decision §8.2); an
        // untracked child completes the whole run without ever touching the budget.
        result.Outcome.ShouldBe(SimulationOutcome.BudgetExhausted, "sub-workflow steps must count against the one global budget");
    }

    [TestMethod]
    public async Task A_sub_workflow_timer_rides_the_shared_virtual_clock()
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

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(NestedTimerWorkflowJson), Sources, scenario);

        // Mirrors A_retry_with_delay_advances_the_virtual_clock_and_records_each_attempt one level
        // down (decision §8.3): the child's timer suspension bubbles to the root, the driver advances
        // the SHARED clock, and the parent replays the child fresh per invocation until it succeeds.
        result.Outcome.ShouldBe(SimulationOutcome.Completed, "a sub-workflow retry timer must suspend and auto-advance on the shared clock");
        result.ClockAdvances.Count.ShouldBe(2, "two failed child attempts, each retryAfter=5s on the shared clock");
        (result.ClockAdvances[1].To - result.ClockAdvances[0].To).ShouldBe(TimeSpan.FromSeconds(5));
        result.StepsExecuted.ShouldBeGreaterThanOrEqualTo(4, "each child (re)execution counts against the shared budget");
    }

    [TestMethod]
    public async Task A_parent_record_does_not_swallow_the_child_exchanges()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);

        // The child recorder owns the child's exchange range: the parent step's record covers only
        // the parent's own exchanges (none — a workflowId step makes no calls itself). Today the
        // untracked child's exchange lands inside the parent record's range.
        SimulatedStepRecord parent = result.Steps.Single(s => s.StepId == "call-child");
        parent.ExchangeCount.ShouldBe(0, "the child's exchanges belong to the child's records, not the parent step's range");
        result.Exchanges.Count.ShouldBe(1, "the child's exchange is still in the one global exchange list");
    }

    [TestMethod]
    public async Task A_scoped_breakpoint_fires_inside_a_sub_workflow()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(
            Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario,
            new SimulationStop { Breakpoints = new HashSet<string> { "call-child/get-pet" } });

        // Design §3.5: the composed scoped path addresses the step inside the child; the root trace's
        // pausedBefore carries the full scoped path.
        result.Outcome.ShouldBe(SimulationOutcome.Paused, "a scoped breakpoint must fire inside the sub-workflow");
        result.PausedBefore.ShouldBe("call-child/get-pet");
    }

    [TestMethod]
    public async Task A_bare_step_id_breakpoint_does_not_fire_inside_a_sub_workflow()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        // "get-pet" exists only INSIDE the child; a bare id addresses root steps only (design §2),
        // so this run must complete without pausing — pinned deliberately, before and after the seam.
        using SimulationResult result = await simulator.SimulateAsync(
            Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario,
            new SimulationStop { Breakpoints = new HashSet<string> { "get-pet" } });

        result.Outcome.ShouldBe(SimulationOutcome.Completed, "a bare stepId must address root steps only");
    }

    // Slice C (§15-8a): three levels of nesting — a→b→c — so recursion is pinned beyond one hop.
    private const string ThreeLevelWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "ThreeLevels", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "a",
              "steps": [ { "stepId": "call-b", "workflowId": "b", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] } ]
            },
            {
              "workflowId": "b",
              "steps": [ { "stepId": "call-c", "workflowId": "c", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] } ]
            },
            {
              "workflowId": "c",
              "steps": [
                {
                  "stepId": "get-pet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                }
              ]
            }
          ]
        }
        """;

    // Slice C (§15-8a): a workflow that invokes itself — the depth cap must exhaust it predictably.
    private const string SelfRecursiveWorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Recursive", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "loop",
              "steps": [ { "stepId": "again", "workflowId": "loop" } ]
            }
          ]
        }
        """;

    [TestMethod]
    public async Task A_sub_workflow_step_record_carries_its_nested_trace()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);
        SimulatedStepRecord parent = result.Steps.Single(s => s.StepId == "call-child");
        parent.SubTrace.ShouldNotBeNull();

        SimulatedSubTrace sub = parent.SubTrace!;
        sub.WorkflowId.ShouldBe("child");
        sub.Outcome.ShouldBe(SimulationOutcome.Completed);
        sub.StepsExecuted.ShouldBe(1);
        sub.Steps.Count.ShouldBe(1);

        // The child's record indexes the ONE global exchange list, and its truth table was
        // re-evaluated against the child's own compiled criteria (AnalyzeTrace recursion).
        SimulatedStepRecord childStep = sub.Steps[0];
        childStep.StepId.ShouldBe("get-pet");
        childStep.ExchangeCount.ShouldBe(1);
        result.Exchanges[childStep.FirstExchange].Path.ShouldBe("/pets/42");
        childStep.SuccessCriteria.Count.ShouldBe(1);
        childStep.SuccessCriteria[0].Satisfied.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_child_fault_surfaces_on_the_parent_record_with_the_nested_fault_visible()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 404, default)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario);

        // The Camunda incident property: the child's fault presents as the parent step faulting,
        // with the child's own fault visible in the nested records.
        result.Outcome.ShouldBe(SimulationOutcome.Faulted);
        SimulatedStepRecord parent = result.Steps.Single(s => s.StepId == "call-child");
        parent.Faulted.ShouldBeTrue();
        parent.SubTrace.ShouldNotBeNull();
        parent.SubTrace!.Outcome.ShouldBe(SimulationOutcome.Faulted);
        parent.SubTrace!.Fault.ShouldNotBeNull();
        parent.SubTrace!.Fault!.Value.StepId.ShouldBe("get-pet");
        parent.SubTrace!.Steps.Single().Faulted.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Sub_sub_workflows_nest_recursively()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(ThreeLevelWorkflowJson), Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Completed);
        SimulatedSubTrace b = result.Steps.Single(s => s.StepId == "call-b").SubTrace.ShouldNotBeNull();
        b.WorkflowId.ShouldBe("b");
        SimulatedSubTrace c = b.Steps.Single(s => s.StepId == "call-c").SubTrace.ShouldNotBeNull();
        c.WorkflowId.ShouldBe("c");
        c.Steps.Single().StepId.ShouldBe("get-pet");
        c.Steps.Single().SuccessCriteria[0].Satisfied.ShouldBeTrue("recursion analyzes every depth");
        result.StepsExecuted.ShouldBe(3, "one step per level against the one global budget");
    }

    [TestMethod]
    public async Task Runaway_recursion_exhausts_at_the_depth_cap()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{}"""u8.ToArray());
        var scenario = new SimulationScenario { Inputs = inputs.RootElement };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(SelfRecursiveWorkflowJson), Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.BudgetExhausted, "the depth cap unwinds runaway recursion predictably");

        // The unwind materialized the in-flight ancestor chain: partial sub-traces all the way down.
        SimulatedStepRecord root = result.Steps.Single(s => s.StepId == "again");
        root.SubTrace.ShouldNotBeNull();
        root.SubTrace!.Outcome.ShouldBe(SimulationOutcome.BudgetExhausted);
    }

    [TestMethod]
    public async Task A_scoped_pause_leaves_partial_ancestor_records()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(
            Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario,
            new SimulationStop { Breakpoints = new HashSet<string> { "call-child/get-pet" } });

        // §3.5's partial-trace representation: the trace is complete up to the stop — the ancestor
        // record is present with a partial sub-trace (outcome paused, its own scope-LOCAL
        // pausedBefore), while the root's pausedBefore carries the full scoped path.
        result.Outcome.ShouldBe(SimulationOutcome.Paused);
        result.PausedBefore.ShouldBe("call-child/get-pet");
        SimulatedStepRecord parent = result.Steps.Single(s => s.StepId == "call-child");
        parent.Faulted.ShouldBeFalse();
        parent.SubTrace.ShouldNotBeNull();
        parent.SubTrace!.Outcome.ShouldBe(SimulationOutcome.Paused);
        parent.SubTrace!.PausedBefore.ShouldBe("get-pet");
        parent.SubTrace!.Steps.Count.ShouldBe(0, "the stop landed before the child's first step");
    }

    [TestMethod]
    public async Task A_suspended_child_leaves_a_suspended_parent_record()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 503, default)],
            AutoAdvanceClock = false,
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(NestedTimerWorkflowJson), Sources, scenario);

        result.Outcome.ShouldBe(SimulationOutcome.Suspended);
        result.Wait.ShouldNotBeNull();
        SimulatedStepRecord parent = result.Steps.Single(s => s.StepId == "call-child");
        parent.SubTrace.ShouldNotBeNull();
        parent.SubTrace!.Outcome.ShouldBe(SimulationOutcome.Suspended);
        parent.SubTrace!.Wait.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task The_emitted_subTrace_matches_the_kits_pinned_contract()
    {
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());
        var scenario = new SimulationScenario
        {
            Inputs = inputs.RootElement,
            Mocks = [new("get", "/pets/{petId}", 200, body.RootElement)],
        };

        using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(SubWorkflowJson), Sources, scenario);
        byte[] traceUtf8 = SerializeTrace(result);

        using ParsedJsonDocument<JsonElement> trace = ParsedJsonDocument<JsonElement>.Parse(traceUtf8.AsMemory());
        JsonElement root = trace.RootElement;

        // The kit's ascent-to-null depends on the root trace never carrying a workflowId.
        root.TryGetProperty("workflowId"u8, out _).ShouldBeFalse("the root trace must not carry a workflowId");

        JsonElement parentStep = root.GetProperty("steps"u8)[0];
        parentStep.GetProperty("status"u8).ValueEquals("completed"u8).ShouldBeTrue();
        JsonElement subTrace = parentStep.GetProperty("subTrace"u8);

        // The pinned contract shape: {workflowId, outcome, stepsExecuted, steps} (debug-tray.test.js:169).
        subTrace.GetProperty("workflowId"u8).ValueEquals("child"u8).ShouldBeTrue();
        subTrace.GetProperty("outcome"u8).ValueEquals("completed"u8).ShouldBeTrue();
        subTrace.GetProperty("stepsExecuted"u8).GetInt32().ShouldBe(1);
        subTrace.GetProperty("steps"u8).GetArrayLength().ShouldBe(1);
        subTrace.GetProperty("steps"u8)[0].GetProperty("stepId"u8).ValueEquals("get-pet"u8).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Repeated_nested_simulations_serialize_byte_identically()
    {
        // The §12 determinism byte-lock (decision §8.7), over a nested fixture with waits and
        // retries: the same command twice must produce byte-identical serialized traces.
        using var simulator = new WorkflowSimulator(new WorkflowExecutorProvider(durable: true));
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        using ParsedJsonDocument<JsonElement> body = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Fido"}"""u8.ToArray());

        byte[][] traces = new byte[2][];
        for (int i = 0; i < 2; i++)
        {
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
            using SimulationResult result = await simulator.SimulateAsync(Encoding.UTF8.GetBytes(NestedTimerWorkflowJson), Sources, scenario);
            result.Outcome.ShouldBe(SimulationOutcome.Completed);
            traces[i] = SerializeTrace(result);
        }

        traces[0].AsSpan().SequenceEqual(traces[1]).ShouldBeTrue("determinism: the same nested simulation must serialize byte-identically");
    }

    private static byte[] SerializeTrace(SimulationResult result)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            ScenarioSuite.WriteTrace(writer, result);
        }

        return buffer.WrittenSpan.ToArray();
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