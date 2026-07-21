// <copyright file="ControlPlaneDebugRunApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Tests the §18 debug-run lifecycle over the API on the DURABLE host (workflow-designer design §18 slice 3e-2c):
/// a debug run IS the durable <c>$draft</c> run the in-process <see cref="InProcessDraftRunner"/> executes for real
/// (real <see cref="WorkflowExecutorProvider"/>(durable: true), in-memory stores, a scripted transport), stepped by
/// the handler driving the runner's recording+tracing resumer directly. Covers the three start gates
/// (<c>allowsDraftRuns</c> → 403, credential readiness → 409, unknown workflow → 400), start → completed, start with
/// <c>pause.afterEachStep</c> → paused at step 1, step → advances, a breakpoint stops there, the native fault-remediation
/// resume verbs (retry / skip / rewind / state-patch on a FAULTED run), terminal-state conflicts, cancel, the trace's
/// step records (no bodies), and the fail-closed 400 when the deployment wires no in-process draft-run host.
/// </summary>
[TestClass]
public sealed class ControlPlaneDebugRunApiTests
{
    private const string StartScopes = "workspace:write runs:write";

    // A 2-step workflow whose steps DECLARE outputs, so each executed step leaves checkpoint evidence and appears in
    // the metadata trace's step records (a step that produced nothing is not individually represented).
    private const string TwoStepDoc = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                { "stepId": "get-pet", "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" } },
                { "stepId": "adopt-pet", "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "status": "$response.body#/status" } }
              ]
            }
          ]
        }
        """;

    // get-pet's criterion fails against a 500, and it declares no failure action: the run faults on get-pet. adopt-pet
    // uses only $inputs (never get-pet's outputs), so a skip past the faulted step completes the run.
    private const string FaultingDoc = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Fault", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "faulting",
              "steps": [
                { "stepId": "get-pet", "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" } },
                { "stepId": "adopt-pet", "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "status": "$response.body#/status" } }
              ]
            }
          ]
        }
        """;

    // A workflow that SUSPENDS on an AsyncAPI receive after one HTTP step — the §18 inject-message
    // target: the endpoint is the debug stand-in for the real publisher, delivering the verdict
    // straight to the awaiting run (nothing is published to a broker).
    private const string ReceiveDoc = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Await", "version": "1.0.0" },
          "sourceDescriptions": [
            { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" },
            { "name": "events", "url": "./events.asyncapi.json", "type": "asyncapi" }
          ],
          "workflows": [
            {
              "workflowId": "await-verdict",
              "steps": [
                { "stepId": "get-pet", "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" } },
                { "stepId": "verdict", "channelPath": "kyc.verdict", "action": "receive",
                  "outputs": { "approved": "$message.payload#/approved" } }
              ]
            }
          ]
        }
        """;

    private const string EventsDoc = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Events", "version": "1.0.0" },
          "channels": {
            "kycResults": {
              "address": "kyc.verdict",
              "messages": { "kycVerdict": { "payload": { "type": "object", "properties": { "approved": { "type": "boolean" } } } } }
            }
          },
          "operations": {
            "receiveKycVerdict": { "action": "receive", "channel": { "$ref": "#/channels/kycResults" }, "messages": [ { "$ref": "#/channels/kycResults/messages/kycVerdict" } ] }
          }
        }
        """;

    private const string PetstoreDoc = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": { "operationId": "getPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } }, "default": { "description": "unexpected" } } }
            },
            "/pets/{petId}/adopt": {
              "post": { "operationId": "adoptPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "adopted", "content": { "application/json": { "schema": { "type": "object", "properties": { "status": { "type": "string" } } } } } } } }
            }
          }
        }
        """;

    // One durable-mode executor provider is shared across tests: the runner's compile cache keys by content hash, so
    // a document compiles once however many runs replay it (each test still builds its own runner + stores).
    private static readonly WorkflowExecutorProvider SharedProvider = new(durable: true);
    private static readonly WorkflowSimulator SharedSimulator = new(SharedProvider);

    [TestMethod]
    public async Task The_start_gates_answer_honestly_before_any_execution()
    {
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateWorkingCopyAsync(TwoStepDoc, PetstoreDoc);

        // An unknown environment is 404 (non-disclosing).
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"nowhere"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // An environment whose administrators have NOT set allowsDraftRuns refuses drafts: 403.
        await host.CreateEnvironmentAsync("""{"name":"production"}""");
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"production"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A development-class environment that allows drafts but has no credential bound for the declared source: 409.
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        HttpResponseMessage notReady = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development"}""", StartScopes);
        notReady.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await notReady.Content.ReadAsStringAsync()).ShouldContain("petstore");

        // With the credential bound, an unknown workflow id is the remaining 400.
        await host.BindCredentialAsync("petstore", "development");
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"nope","environment":"development"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_debug_run_single_steps_to_completion_and_terminal_states_conflict()
    {
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        // Start paused after each step: the run advances exactly one step and reports paused, carrying the audit
        // tuple and the host-executed metadata trace (its one step record with a metadata-only exchange).
        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
            run.RootElement.GetProperty("environment").GetString().ShouldBe("development");
            run.RootElement.GetProperty("workflowId").GetString().ShouldBe("adopt");
            run.RootElement.GetProperty("documentEtag").GetString().ShouldNotBeNullOrEmpty();
            run.RootElement.GetProperty("startedBy").GetString().ShouldNotBeNullOrEmpty();
        }

        // §18 R5: the control plane enqueued the run paused-after-each-step; the runner advances it exactly one step
        // and reports paused, carrying the audit tuple and the host-executed metadata trace (one step + its exchange).
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
            Stj.JsonElement trace = run.RootElement.GetProperty("trace");
            trace.GetProperty("outcome").GetString().ShouldBe("paused");
            trace.GetProperty("pausedBefore").GetString().ShouldBe("adopt-pet");
            Stj.JsonElement steps = trace.GetProperty("steps");
            steps.GetArrayLength().ShouldBe(1);
            steps[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
            steps[0].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/42");
            steps[0].GetProperty("requests")[0].GetProperty("method").GetString().ShouldBe("get");
        }

        // A plain resume carries the single-step pause off (bare resume); the runner then advances step 2 of 2 to
        // completion.
        HttpResponseMessage resumed = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes);
        resumed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(2);
            run.RootElement.GetProperty("trace").GetProperty("steps").GetArrayLength().ShouldBe(2);
        }

        // A completed run is terminal.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Cancel is terminal too: a fresh run cancels, then refuses to resume.
        HttpResponseMessage second = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"afterEachStep":true}}""", StartScopes);
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        string secondId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await second.Content.ReadAsStringAsync()))
        {
            secondId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        HttpResponseMessage cancelled = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{secondId}/cancel", "{}", StartScopes);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await cancelled.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("cancelled");
        }

        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{secondId}/resume", "{}", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // An unknown run id under a real working copy is 404.
        (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/debug-runs/dbg-nope", "{}", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Starting_and_cancelling_a_debug_run_emits_governance_audit_spans()
    {
        // §850 (item 8): a debug run is a real execution against real sources, so its lifecycle is traceable — a
        // developer can pivot a debug session into the deployment's telemetry.
        using GovernanceAuditProbe audit = GovernanceAuditProbe.Capture();
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/cancel", "{}", StartScopes)).StatusCode.ShouldBe(HttpStatusCode.OK);

        audit.Outcomes(debugRunId).ShouldBe(["started", "cancelled"]);
    }

    [TestMethod]
    public async Task Resuming_a_debug_run_emits_a_resumed_governance_audit_span()
    {
        // §850: continuing a paused debug run is a real re-execution, so it is audited alongside start and cancel.
        using GovernanceAuditProbe audit = GovernanceAuditProbe.Capture();
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // Advance to the after-first-step pause, then a bare resume carries the pause off (200, audited).
        (await host.PumpAndGetAsync(id, debugRunId)).Dispose();
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes)).StatusCode.ShouldBe(HttpStatusCode.OK);

        audit.Outcomes(debugRunId).ShouldBe(["started", "resumed"]);
    }

    [TestMethod]
    public async Task A_run_with_no_pause_advances_straight_to_completion()
    {
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument s = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = s.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // §18 R5: the control plane enqueued the run; the runner advances it. Pump, then poll the completed state.
        using Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId);
        run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
        run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(2);

        Stj.JsonElement trace = run.RootElement.GetProperty("trace");
        trace.GetProperty("outcome").GetString().ShouldBe("completed");
        trace.GetProperty("stepsExecuted").GetInt32().ShouldBe(2);
        trace.GetProperty("steps")[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
        trace.GetProperty("steps")[1].GetProperty("stepId").GetString().ShouldBe("adopt-pet");
        trace.GetProperty("steps")[1].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/42/adopt");
        AssertNoBodies(trace);
    }

    [TestMethod]
    public async Task A_breakpoint_stops_before_the_named_step()
    {
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        // No afterEachStep, just a breakpoint before adopt-pet: the run executes get-pet and stops before adopt-pet.
        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"beforeSteps":["adopt-pet"]}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // §18 R5: the runner executes get-pet and stops before adopt-pet at the breakpoint.
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            steps.GetArrayLength().ShouldBe(1);
            steps[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
            run.RootElement.GetProperty("trace").GetProperty("pausedBefore").GetString().ShouldBe("adopt-pet");
        }

        // Bare resume (no breakpoint carried forward) runs off the end to completion.
        HttpResponseMessage completed = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes);
        completed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(2);
        }
    }

    [TestMethod]
    public async Task Native_resume_verbs_remediate_a_faulted_run()
    {
        await using Scoped host = await StartAsync(withRunner: true, FaultingMock());
        string id = await host.CreateReadyWorkingCopyAsync(FaultingDoc);

        // Start: get-pet's criterion fails against the 500, so the run faults immediately (a resumable terminal).
        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"faulting","environment":"development","inputs":{"petId":"42"}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // §18 R5: the runner advances the run; get-pet's criterion fails against the 500 so it faults (a resumable
        // terminal). The fault-remediation resume verbs below are also mark-claimable (R5b) — each marks the run and
        // the runner re-executes it, so each is followed by a pump + poll.
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("faulted");
            run.RootElement.GetProperty("trace").GetProperty("fault").GetProperty("stepId").GetString().ShouldBe("get-pet");
        }

        // RetryFaultedStep re-advances; the deterministic 500 faults again — honestly reported (native retry path).
        HttpResponseMessage retried = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"RetryFaultedStep"}}""", StartScopes);
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("faulted");
        }

        // A StatePatch that cannot apply (replace on a missing path) is a 409 and mutates nothing.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"StatePatch","patch":[{"op":"replace","path":"/stepOutputs/nope","value":1}]}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // A StatePatch that DOES apply re-enters the faulted step (the 500 world is unchanged, so it re-faults) — the
        // 200 proves the native state-patch path applied the RFC 6902 patch over { inputs, stepOutputs }.
        HttpResponseMessage patched = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"StatePatch","patch":[{"op":"replace","path":"/inputs/petId","value":"99"}]}}""", StartScopes);
        patched.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("faulted");
        }

        // Rewind to the start re-runs get-pet against the same 500 — still faulted (native rewind path).
        HttpResponseMessage rewound = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"Rewind","targetCursor":0}}""", StartScopes);
        rewound.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("faulted");
        }

        // Skip past the faulted step (targetCursor = the cursor to resume at, so 1 skips step 0): the run completes
        // through adopt-pet (which the FaultingMock answers 200) — native skip path.
        HttpResponseMessage skipped = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"Skip","targetCursor":1,"skipOutputs":{}}}""", StartScopes);
        skipped.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");

            // Skipping the faulted get-pet advances into and completes adopt-pet (proved by the run completing and
            // adopt-pet being recorded completed). The exact per-exchange attribution across a skipped step is a
            // slice-3f trace-parity concern (the assembler pairs ordered exchanges to executed steps one-to-one), so
            // this asserts the step outcome, not which step the lone recorded exchange landed on.
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            bool adoptCompleted = false;
            foreach (Stj.JsonElement step in steps.EnumerateArray())
            {
                if (step.GetProperty("stepId").GetString() == "adopt-pet")
                {
                    step.GetProperty("status").GetString().ShouldBe("completed");
                    adoptCompleted = true;
                }
            }

            adoptCompleted.ShouldBeTrue("skipping the faulted get-pet advances into and completes adopt-pet");
        }

        // Terminal: a completed run refuses a further resume verb.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"RetryFaultedStep"}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task A_resume_verb_on_a_paused_non_faulted_run_does_not_apply()
    {
        // Step-over of a NON-faulted paused step is a replay-debugger operation (slice 3e-3), not a live-run one, so a
        // native resume verb on a paused run does not apply (409).
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // §18 R5: the runner advances one step and pauses.
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
        }

        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"RetryFaultedStep"}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Debug_runs_fail_closed_when_no_in_process_draft_run_host_is_wired()
    {
        await using Scoped host = await StartAsync(withRunner: false, null);
        string id = await host.CreateWorkingCopyAsync(TwoStepDoc, PetstoreDoc);
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/debug-runs/dbg-x", "{}", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Deleting_a_debug_run_purges_its_capture_and_trace()
    {
        await using Scoped host = await StartAsync(withRunner: true, HappyMock());
        string id = await host.CreateReadyWorkingCopyAsync(TwoStepDoc);

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // The runner advances it to completion and persists a trace; get-debug-run then returns it.
        await host.PumpAsync();
        (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/debug-runs/{debugRunId}", "{}", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // §18 R5c: DELETE purges the run's captured draft, metadata trace, and durable run (204).
        (await host.SendJsonAsync(HttpMethod.Delete, $"/workspace/workflows/{id}/debug-runs/{debugRunId}", "{}", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // It is gone: get-debug-run is now 404 (the purged capture is what the reach check reads).
        (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/debug-runs/{debugRunId}", "{}", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Injecting_a_message_resumes_a_run_suspended_on_a_receive()
    {
        using GovernanceAuditProbe audit = GovernanceAuditProbe.Capture();
        await using var messageTransport = new Corvus.Text.Json.AsyncApi.Testing.InMemoryMessageTransport();
        await using Scoped host = await StartAsync(withRunner: true, HappyMock(), messageTransport);
        string id = await host.CreateWorkingCopyAsync(ReceiveDoc, PetstoreDoc);
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/events",
            $$"""{"document":{{EventsDoc}}}""", "workspace:write")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        await host.BindCredentialAsync("petstore", "development");
        await host.BindCredentialAsync("events", "development");

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"await-verdict","environment":"development","inputs":{"petId":"42"}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // A message cannot be injected while the run is still pending/running: 409, not-awaiting.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/inject-message",
            """{"channel":"kyc.verdict","payload":{"approved":true}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The pump executes the HTTP step, then the run SUSPENDS on the kyc.verdict receive.
        using (Stj.JsonDocument run = await host.PumpAndGetAsync(id, debugRunId))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("suspended");
            run.RootElement.GetProperty("trace").GetProperty("steps")[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
        }

        // A message on the WRONG channel is 409 — the run is not awaiting it, and stays suspended.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/inject-message",
            """{"channel":"other.events","payload":{"approved":true}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // The matching channel delivers straight to the awaiting run: the response IS the resumed run,
        // advanced through the receive to completion, its verdict step carrying the message-derived outputs.
        HttpResponseMessage injected = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/inject-message",
            """{"channel":"kyc.verdict","payload":{"approved":true}}""", StartScopes);
        injected.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await injected.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            steps.GetArrayLength().ShouldBe(2);
            steps[1].GetProperty("stepId").GetString().ShouldBe("verdict");
            AssertNoBodies(run.RootElement); // the §18 posture holds for message payloads too
        }

        // A terminal run cannot take a message: 409.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/inject-message",
            """{"channel":"kyc.verdict","payload":{"approved":true}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // §850: only the successful message injection is a governed action (the wrong-channel and terminal 409s are not).
        audit.Outcomes(debugRunId).ShouldBe(["started", "injected"]);
    }

    private static MockApiTransport HappyMock()
    {
        var mock = new MockApiTransport();
        mock.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        mock.SetResponse(OperationMethod.Post, "/pets/{petId}/adopt", 200, """{"status":"adopted"}""");
        return mock;
    }

    private static MockApiTransport FaultingMock()
    {
        var mock = new MockApiTransport();
        mock.SetResponse(OperationMethod.Get, "/pets/{petId}", 500, """{"error":"boom"}""");
        mock.SetResponse(OperationMethod.Post, "/pets/{petId}/adopt", 200, """{"status":"adopted"}""");
        return mock;
    }

    private static async Task<Scoped> StartAsync(bool withRunner, MockApiTransport? transport, Corvus.Text.Json.AsyncApi.IMessageTransport? messageTransport = null)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");
        var workspaceStore = new Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows.InMemoryWorkspaceWorkflowStore();

        InMemoryDraftRunStore? drafts = null;
        InMemoryDraftRunTraceStore? traceStore = null;
        InProcessDraftRunner? runner = null;
        if (withRunner)
        {
            drafts = new InMemoryDraftRunStore();
            traceStore = new InMemoryDraftRunTraceStore();
            MockApiTransport mock = transport!;
            WorkflowTransportBinder binder = (WorkflowDescriptor descriptor, SecurityTagSet runTags) =>
                new WorkflowTransports(
                    descriptor.Sources.ToDictionary(s => s, _ => (IApiTransport)mock, StringComparer.Ordinal),
                    messageTransport);
            runner = new InProcessDraftRunner(store, "runner-dev", "development", drafts, traceStore, SharedProvider, binder);
        }

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(ScopeAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ScopeAuthHandler>(ScopeAuthHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(
            management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.ScopesOnly,
            workspaceWorkflowStore: workspaceStore,
            workflowSimulator: SharedSimulator,
            workflowStateStore: withRunner ? store : null,
            draftRunStore: drafts,
            draftRunner: runner,
            draftRunTraceStore: traceStore);
        await app.StartAsync();
        return new Scoped(app, app.GetTestClient(), runner);
    }

    // The no-bodies invariant (the ratified §18 posture): no request or response body property appears in the trace.
    private static void AssertNoBodies(Stj.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case Stj.JsonValueKind.Object:
                foreach (Stj.JsonProperty property in element.EnumerateObject())
                {
                    property.Name.ShouldNotBe("requestBody");
                    property.Name.ShouldNotBe("responseBody");
                    AssertNoBodies(property.Value);
                }

                break;

            case Stj.JsonValueKind.Array:
                foreach (Stj.JsonElement item in element.EnumerateArray())
                {
                    AssertNoBodies(item);
                }

                break;
        }
    }

    private sealed class Scoped(WebApplication app, HttpClient client, InProcessDraftRunner? runner) : IAsyncDisposable
    {
        // §18 R5: the control plane only marks a debug run claimable; a runner advances it out-of-band. In these
        // single-process tests THIS is that runner — pump it to advance every marked run (start / resume) to its next
        // pause, completion, or fault before asserting the state the UI would poll get-debug-run for.
        public async Task PumpAsync()
        {
            if (runner is not null)
            {
                await runner.RunPendingAsync();
            }
        }

        // §18 R5: pump the runner (advancing every marked run to its next pause / completion / fault), then read the
        // debug run back exactly as the UI would poll get-debug-run for the new state and trace.
        public async Task<Stj.JsonDocument> PumpAndGetAsync(string workingCopyId, string debugRunId)
        {
            await this.PumpAsync();
            HttpResponseMessage got = await this.SendJsonAsync(
                HttpMethod.Get, $"/workspace/workflows/{workingCopyId}/debug-runs/{debugRunId}", "{}", "workspace:read");
            got.StatusCode.ShouldBe(HttpStatusCode.OK);
            return Stj.JsonDocument.Parse(await got.Content.ReadAsStringAsync());
        }

        public async Task<string> CreateWorkingCopyAsync(string workflowDoc, string? sourceDoc)
        {
            HttpResponseMessage created = await this.SendJsonAsync(
                HttpMethod.Post, "/workspace/workflows", $$"""{"name":"debug-run-test","document":{{workflowDoc}}}""", "workspace:write");
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
            using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            string id = doc.RootElement.GetProperty("id").GetString()!;

            if (sourceDoc is not null)
            {
                HttpResponseMessage attached = await this.SendJsonAsync(
                    HttpMethod.Put, $"/workspace/workflows/{id}/sources/petstore", $$"""{"document":{{sourceDoc}}}""", "workspace:write");
                attached.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            return id;
        }

        // A working copy plus its petstore source, a development environment that allows drafts, and a bound
        // credential for the source — the ready-to-run state the gates require before a debug run may start.
        public async Task<string> CreateReadyWorkingCopyAsync(string workflowDoc)
        {
            string id = await this.CreateWorkingCopyAsync(workflowDoc, PetstoreDoc);
            await this.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
            await this.BindCredentialAsync("petstore", "development");
            return id;
        }

        public async Task CreateEnvironmentAsync(string body)
        {
            (await this.SendJsonAsync(HttpMethod.Post, "/environments", body, "environments:write"))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        public async Task BindCredentialAsync(string sourceName, string environment)
        {
            (await this.SendJsonAsync(
                HttpMethod.Post,
                "/credentials",
                $$"""{"sourceName":"{{sourceName}}","environment":"{{environment}}","authKind":"apiKey","secretRefs":[{"name":"value","ref":"keyvault://{{sourceName}}-key#1"}]}""",
                "credentials:write"))
                .StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
        {
            using var request = new HttpRequestMessage(method, path);
            if (method != HttpMethod.Get)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            if (scope is not null)
            {
                request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
            }

            return await client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            if (runner is not null)
            {
                await runner.DisposeAsync();
            }

            await app.DisposeAsync();
        }
    }

    private sealed class ScopeAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Scopes";
        public const string ScopeHeader = "X-Scopes";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!this.Request.Headers.TryGetValue(ScopeHeader, out Microsoft.Extensions.Primitives.StringValues values))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", values.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
