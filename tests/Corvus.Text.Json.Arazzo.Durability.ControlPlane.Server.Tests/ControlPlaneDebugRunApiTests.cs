// <copyright file="ControlPlaneDebugRunApiTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
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
/// Tests the §18 debug-run lifecycle over the API (workflow-designer design §18, interim
/// executor): the three start gates (the environment's <c>allowsDraftRuns</c> flag → 403,
/// per-source credential readiness → 409 naming the gaps, unknown workflow → 400), single-step
/// pause semantics (start paused after step 1, resume to completion), the resume actions through
/// the simulator's §15 8b override seam (Skip records a skipped step whose provided outputs stand
/// downstream, Rewind replays forward from the target, RetryFaultedStep re-advances a faulted run,
/// StatePatch mutates inputs and pins step outputs), terminal-state conflicts, cancel, and the
/// fail-closed 400 when the deployment wires no executor.
/// </summary>
[TestClass]
public sealed class ControlPlaneDebugRunApiTests
{
    private const string StartScopes = "workspace:write runs:write";

    private const string WorkflowDoc = """
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
                  "successCriteria": [ { "condition": "$statusCode == 200" } ] },
                { "stepId": "adopt-pet", "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ] }
              ]
            }
          ]
        }
        """;

    // Three steps whose LAST references the middle step's outputs — the §15 8b assertions: when
    // adopt-pet is stepped over, its PROVIDED nextPetId must resolve check-pet's path parameter.
    // adopt-pet's outputs deliberately read $inputs (not the response body): the spec-derived
    // interim mocks answer with empty bodies, so a body extraction would fault if it ever executed.
    private const string ChainDoc = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Chain", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "chain",
              "steps": [
                { "stepId": "get-pet", "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ] },
                { "stepId": "adopt-pet", "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "nextPetId": "$inputs.nextPetId" } },
                { "stepId": "check-pet", "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$steps.adopt-pet.outputs.nextPetId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ] }
              ]
            }
          ]
        }
        """;

    // The first step demands a 202 the spec-derived mock (first declared 2xx = 200) never answers,
    // and declares no failure action: the run faults immediately — the retry/skip recovery corpus.
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
                  "successCriteria": [ { "condition": "$statusCode == 202" } ] },
                { "stepId": "adopt-pet", "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ] }
              ]
            }
          ]
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
                "responses": { "200": { "description": "ok" }, "default": { "description": "unexpected" } } }
            },
            "/pets/{petId}/adopt": {
              "post": { "operationId": "adoptPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "adopted" } } }
            }
          }
        }
        """;

    private static readonly WorkflowSimulator SharedSimulator = new(new WorkflowExecutorProvider(durable: true));

    [TestMethod]
    public async Task The_start_gates_answer_honestly_before_any_execution()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        // An unknown environment is 404 (non-disclosing).
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"nowhere"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // An environment whose administrators have NOT set allowsDraftRuns refuses drafts: 403.
        await host.CreateEnvironmentAsync("""{"name":"production"}""");
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"production"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A development-class environment that allows drafts but has no credential bound for the
        // declared source: 409 naming the gap.
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
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        await host.BindCredentialAsync("petstore", "development");

        // Start paused after each step: the run advances exactly one step and reports paused,
        // carrying the audit tuple and the simulation-shaped trace.
        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development","inputs":{"petId":"42"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
            run.RootElement.GetProperty("environment").GetString().ShouldBe("development");
            run.RootElement.GetProperty("documentEtag").GetString().ShouldNotBeNullOrEmpty();
            run.RootElement.GetProperty("startedBy").GetString().ShouldNotBeNullOrEmpty();
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            steps.GetArrayLength().ShouldBe(1);
            steps[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
            steps[0].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/42");
        }

        // GET reads the same state back.
        HttpResponseMessage fetched = await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/debug-runs/{debugRunId}", "{}", "workspace:read");
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await fetched.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
        }

        // A plain resume carries the single-step pause forward: step 2 of 2 completes the run.
        HttpResponseMessage resumed = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes);
        resumed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await resumed.Content.ReadAsStringAsync()))
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
    public async Task Skip_steps_over_the_next_step_and_its_provided_outputs_stand_downstream()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(ChainDoc, PetstoreDoc);
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        await host.BindCredentialAsync("petstore", "development");

        // Paused after step 1: the cursor names adopt-pet as the next step — the step-over target.
        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"chain","environment":"development","inputs":{"petId":"42","nextPetId":"13"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
        }

        // Step over with provided outputs (§15 8b): adopt-pet is recorded skipped — no exchange —
        // and its PROVIDED nextPetId is the value downstream references must see.
        HttpResponseMessage skipped = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"Skip","skipOutputs":{"nextPetId":"77"}}}""", StartScopes);
        skipped.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await skipped.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(2);
            Stj.JsonElement record = run.RootElement.GetProperty("trace").GetProperty("steps")[1];
            record.GetProperty("stepId").GetString().ShouldBe("adopt-pet");
            record.GetProperty("status").GetString().ShouldBe("completed");
            record.GetProperty("attempt").GetInt32().ShouldBe(0);
            record.GetProperty("skipped").GetBoolean().ShouldBeTrue();
            record.GetProperty("outputs").GetProperty("nextPetId").GetString().ShouldBe("77");
            record.TryGetProperty("requests", out _).ShouldBeFalse("the skipped step never touched the transport");
        }

        // The completing advance proves the override stands: check-pet's path parameter resolves
        // $steps.adopt-pet.outputs.nextPetId to the PROVIDED 77 through the real emitted executor.
        HttpResponseMessage completed = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes);
        completed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await completed.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(3);
            Stj.JsonElement record = run.RootElement.GetProperty("trace").GetProperty("steps")[2];
            record.GetProperty("stepId").GetString().ShouldBe("check-pet");
            record.GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/77");
        }
    }

    [TestMethod]
    public async Task Rewind_resets_the_position_and_the_forward_replay_reexecutes()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(ChainDoc, PetstoreDoc);
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        await host.BindCredentialAsync("petstore", "development");

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"chain","environment":"development","inputs":{"petId":"42","nextPetId":"13"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
        }

        // Advance to position 2 first, so the rewind observably moves backwards.
        HttpResponseMessage advanced = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes);
        advanced.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await advanced.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(2);
        }

        // Rewind to the start: the forward replay re-executes step 1 (its exchange is in the fresh
        // trace) and the single-step pause halts after it — position 1, one recorded step.
        HttpResponseMessage rewound = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"Rewind","targetCursor":0}}""", StartScopes);
        rewound.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await rewound.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(1);
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            steps.GetArrayLength().ShouldBe(1);
            steps[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
            steps[0].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/42");
        }
    }

    [TestMethod]
    public async Task Retry_re_advances_a_faulted_run_and_skip_steps_over_the_faulted_step()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(FaultingDoc, PetstoreDoc);
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        await host.BindCredentialAsync("petstore", "development");

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"faulting","environment":"development","inputs":{"petId":"42"}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
            run.RootElement.GetProperty("status").GetString().ShouldBe("faulted");
            run.RootElement.GetProperty("trace").GetProperty("fault").GetProperty("stepId").GetString().ShouldBe("get-pet");
        }

        // RetryFaultedStep re-advances; the deterministic replay meets the same 200 and faults
        // again — honestly reported, exactly as the durable engine's retry of an unchanged world.
        HttpResponseMessage retried = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"RetryFaultedStep"}}""", StartScopes);
        retried.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await retried.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("faulted");
        }

        // Skip past the faulted step (targetCursor = the cursor to resume AT, so 1 names step 0):
        // the replay records get-pet skipped and the run completes through adopt-pet.
        HttpResponseMessage skipped = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"Skip","targetCursor":1,"skipOutputs":{}}}""", StartScopes);
        skipped.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await skipped.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
            run.RootElement.GetProperty("cursor").GetInt32().ShouldBe(2);
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            steps[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
            steps[0].GetProperty("skipped").GetBoolean().ShouldBeTrue();
            steps[1].GetProperty("stepId").GetString().ShouldBe("adopt-pet");
            steps[1].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/42/adopt");
        }
    }

    [TestMethod]
    public async Task State_patch_mutates_inputs_and_pins_step_outputs_and_a_bad_patch_conflicts()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(ChainDoc, PetstoreDoc);
        await host.CreateEnvironmentAsync("""{"name":"development","allowsDraftRuns":true}""");
        await host.BindCredentialAsync("petstore", "development");

        HttpResponseMessage started = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"chain","environment":"development","inputs":{"petId":"42","nextPetId":"13"},"pause":{"afterEachStep":true}}""", StartScopes);
        started.StatusCode.ShouldBe(HttpStatusCode.Created);
        string debugRunId;
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await started.Content.ReadAsStringAsync()))
        {
            debugRunId = run.RootElement.GetProperty("debugRunId").GetString()!;
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
        }

        // A patch that cannot apply (replace on a missing path) is a 409 and mutates nothing.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"StatePatch","patch":[{"op":"replace","path":"/stepOutputs/nope","value":1}]}}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Patch the context { inputs, stepOutputs }: the replaced input re-aims get-pet's replay,
        // and the added stepOutputs entry pins adopt-pet as an override (it skips; 88 stands).
        HttpResponseMessage patched = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume",
            """{"action":{"mode":"StatePatch","patch":[{"op":"replace","path":"/inputs/petId","value":"43"},{"op":"add","path":"/stepOutputs/adopt-pet","value":{"nextPetId":"88"}}]}}""", StartScopes);
        patched.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await patched.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("paused");
            Stj.JsonElement steps = run.RootElement.GetProperty("trace").GetProperty("steps");
            steps[0].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/43", "the patched input drives the replay");
            steps[1].GetProperty("stepId").GetString().ShouldBe("adopt-pet");
            steps[1].GetProperty("skipped").GetBoolean().ShouldBeTrue("the patched stepOutputs entry became an override");
        }

        HttpResponseMessage completed = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs/{debugRunId}/resume", "{}", StartScopes);
        completed.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument run = Stj.JsonDocument.Parse(await completed.Content.ReadAsStringAsync()))
        {
            run.RootElement.GetProperty("status").GetString().ShouldBe("completed");
            Stj.JsonElement record = run.RootElement.GetProperty("trace").GetProperty("steps")[2];
            record.GetProperty("stepId").GetString().ShouldBe("check-pet");
            record.GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/88", "the pinned outputs stand downstream");
        }
    }

    [TestMethod]
    public async Task Debug_runs_fail_closed_when_the_deployment_wires_no_executor()
    {
        await using Scoped host = await StartAsync(withSimulator: false);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/debug-runs",
            """{"workflowId":"adopt","environment":"development"}""", StartScopes))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/debug-runs/dbg-x", "{}", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<Scoped> StartAsync(bool withSimulator)
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");
        var workspaceStore = new Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows.InMemoryWorkspaceWorkflowStore();

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
            workflowSimulator: withSimulator ? SharedSimulator : null);
        await app.StartAsync();
        return new Scoped(app, app.GetTestClient());
    }

    private sealed class Scoped(WebApplication app, HttpClient client) : IAsyncDisposable
    {
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