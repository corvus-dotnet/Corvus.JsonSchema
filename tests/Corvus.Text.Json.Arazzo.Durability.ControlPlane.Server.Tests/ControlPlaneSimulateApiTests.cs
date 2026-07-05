// <copyright file="ControlPlaneSimulateApiTests.cs" company="Endjin Limited">
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
/// Tests <c>simulateWorkingCopy</c> (workflow-designer design §4.3/§8) end to end over the API:
/// compile the working copy through the real executor path, replay against the request's scripted
/// mocks, and return the structured trace — plus stateless stepping (pause before a step), the
/// workflow selector, and the failure modes (fails-closed 400, unknown workflow 400, absent copy
/// 404, non-executable document 422).
/// </summary>
[TestClass]
public sealed class ControlPlaneSimulateApiTests
{
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
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" } },
                { "stepId": "adopt-pet", "operationId": "adoptPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ] }
              ],
              "outputs": { "name": "$steps.get-pet.outputs.petName" }
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
                "responses": {
                  "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } },
                  "default": { "description": "unexpected" } } }
            },
            "/pets/{petId}/adopt": {
              "post": { "operationId": "adoptPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "adopted" } } }
            }
          }
        }
        """;

    private const string SimulateBody = """
        {
          "scenario": {
            "inputs": { "petId": "42" },
            "mocks": [
              { "method": "get", "path": "/pets/{petId}", "status": 200, "body": { "name": "Fido" } },
              { "method": "post", "path": "/pets/{petId}/adopt", "status": 200 }
            ]
          }
        }
        """;

    private static readonly WorkflowSimulator SharedSimulator = new(new WorkflowExecutorProvider(durable: true));

    [TestMethod]
    public async Task A_working_copy_simulates_to_a_structured_trace()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        HttpResponseMessage response = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/simulate", SimulateBody, "workspace:read");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument trace = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        trace.RootElement.GetProperty("outcome").GetString().ShouldBe("completed");
        trace.RootElement.GetProperty("stepsExecuted").GetInt32().ShouldBe(2);
        trace.RootElement.GetProperty("outputs").GetProperty("name").GetString().ShouldBe("Fido");

        Stj.JsonElement steps = trace.RootElement.GetProperty("steps");
        steps.GetArrayLength().ShouldBe(2);
        steps[0].GetProperty("stepId").GetString().ShouldBe("get-pet");
        steps[0].GetProperty("requests")[0].GetProperty("path").GetString().ShouldBe("/pets/42");
        steps[0].GetProperty("requests")[0].GetProperty("responseBody").GetProperty("name").GetString().ShouldBe("Fido");
        steps[0].GetProperty("successCriteria")[0].GetProperty("satisfied").GetBoolean().ShouldBeTrue();
        steps[0].GetProperty("actionTaken").GetProperty("type").GetString().ShouldBe("fallThrough");
    }

    [TestMethod]
    public async Task Stateless_stepping_pauses_before_the_named_step()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        string body = """{"scenario":{"inputs":{"petId":"42"},"mocks":[{"method":"get","path":"/pets/{petId}","status":200,"body":{"name":"Fido"}}]},"until":{"beforeStepId":"adopt-pet"}}""";
        HttpResponseMessage response = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/simulate", body, "workspace:read");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument trace = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        trace.RootElement.GetProperty("outcome").GetString().ShouldBe("paused");
        trace.RootElement.GetProperty("pausedBefore").GetString().ShouldBe("adopt-pet");
        trace.RootElement.GetProperty("steps").GetArrayLength().ShouldBe(1);
    }

    [TestMethod]
    public async Task The_failure_modes_answer_honestly()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        // Unknown workflow selector.
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/simulate", """{"workflowId":"nope"}""", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Absent working copy.
        (await host.SendJsonAsync(HttpMethod.Post, "/workspace/workflows/wc-9999999999/simulate", SimulateBody, "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // A document with no workflows cannot compile.
        string emptyId = await host.CreateWorkingCopyAsync("""{"arazzo":"1.1.0","info":{"title":"x","version":"1"},"workflows":[]}""", sourceDoc: null);
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{emptyId}/simulate", """{}""", "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [TestMethod]
    public async Task Scenarios_have_a_full_lifecycle_and_run_with_judged_expectations()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        // Upsert: mocks address operations by (source, operationId); expectations cover outcome,
        // path, output criteria, and per-step execution counts.
        const string passing = """
            {"name":"happy-path","inputs":{"petId":"42"},
             "mocks":[{"source":"petstore","operationId":"getPet","responses":[{"status":200,"body":{"name":"Fido"}}]},
                      {"source":"petstore","operationId":"adoptPet","responses":[{"status":200}]}],
             "expect":{"outcome":"completed","path":["get-pet","adopt-pet"],"pathMode":"exact",
                       "outputs":[{"condition":"$outputs.name == 'Fido'"}],
                       "steps":{"get-pet":{"attempts":1},"adopt-pet":{"reached":true}}}}
            """;
        HttpResponseMessage put = await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/happy-path", passing, "workspace:write");
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument stored = Stj.JsonDocument.Parse(await put.Content.ReadAsStringAsync()))
        {
            stored.RootElement.GetProperty("etag").GetString().ShouldNotBeNullOrEmpty();
            stored.RootElement.GetProperty("scenario").GetProperty("name").GetString().ShouldBe("happy-path");
        }

        // A name mismatch between path and body is rejected.
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/other", passing, "workspace:write"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A deliberately failing scenario: the mock 404s, so the run cannot complete.
        const string failing = """
            {"name":"declined","inputs":{"petId":"42"},
             "mocks":[{"source":"petstore","operationId":"getPet","responses":[{"status":404}]}],
             "expect":{"outcome":"completed"}}
            """;
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/declined", failing, "workspace:write"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        using (Stj.JsonDocument list = Stj.JsonDocument.Parse(await (await host.SendJsonAsync(HttpMethod.Get, $"/workspace/workflows/{id}/scenarios", "{}", "workspace:read")).Content.ReadAsStringAsync()))
        {
            list.RootElement.GetProperty("scenarios").GetArrayLength().ShouldBe(2);
        }

        // Run-one: every expectation holds and the full trace rides along.
        HttpResponseMessage runOne = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/scenarios/happy-path/run", "{}", "workspace:read");
        runOne.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument result = Stj.JsonDocument.Parse(await runOne.Content.ReadAsStringAsync()))
        {
            result.RootElement.GetProperty("passed").GetBoolean().ShouldBeTrue();
            result.RootElement.GetProperty("outcome").GetString().ShouldBe("completed");
            result.RootElement.GetProperty("expectations").GetArrayLength().ShouldBe(5, "outcome, path, one output, two step assertions");
            result.RootElement.GetProperty("trace").GetProperty("steps").GetArrayLength().ShouldBe(2);
        }

        // Run-all: the suite report counts the failing scenario honestly, with its verdict detail.
        HttpResponseMessage suiteResponse = await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/scenarios", "{}", "workspace:read");
        suiteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument suite = Stj.JsonDocument.Parse(await suiteResponse.Content.ReadAsStringAsync()))
        {
            suite.RootElement.GetProperty("total").GetInt32().ShouldBe(2);
            suite.RootElement.GetProperty("passed").GetInt32().ShouldBe(1);
            suite.RootElement.GetProperty("failed").GetInt32().ShouldBe(1);
            Stj.JsonElement failed = suite.RootElement.GetProperty("results").EnumerateArray().Single(r => !r.GetProperty("passed").GetBoolean());
            failed.GetProperty("scenario").GetString().ShouldBe("declined");
            failed.GetProperty("expectations")[0].GetProperty("detail").GetString()!.ShouldContain("expected completed");
        }

        // Delete → gone; deleting again → 404.
        (await host.SendJsonAsync(HttpMethod.Delete, $"/workspace/workflows/{id}/scenarios/declined", "{}", "workspace:write"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.SendJsonAsync(HttpMethod.Delete, $"/workspace/workflows/{id}/scenarios/declined", "{}", "workspace:write"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Publish_attests_the_suite_server_side_and_embeds_the_evidence()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        const string passing = """
            {"name":"happy","inputs":{"petId":"42"},
             "mocks":[{"source":"petstore","operationId":"getPet","responses":[{"status":200,"body":{"name":"Fido"}}]},
                      {"source":"petstore","operationId":"adoptPet","responses":[{"status":200}]}],
             "expect":{"outcome":"completed"}}
            """;
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/happy", passing, "workspace:write"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Publish: validation gate + server-attested suite + the package embedding both entries.
        HttpResponseMessage published = await host.SendJsonAsync(
            HttpMethod.Post, $"/workspace/workflows/{id}/publish",
            """{"owner":{"name":"Team","email":"team@example.com"},"tags":["designer"]}""", "catalog:write");
        published.StatusCode.ShouldBe(HttpStatusCode.Created);
        string baseWorkflowId;
        int versionNumber;
        using (Stj.JsonDocument version = Stj.JsonDocument.Parse(await published.Content.ReadAsStringAsync()))
        {
            baseWorkflowId = version.RootElement.GetProperty("baseWorkflowId").GetString()!;
            versionNumber = version.RootElement.GetProperty("versionNumber").GetInt32();
            baseWorkflowId.ShouldBe("adopt");
        }

        // The evidence is served from the stored package, server-attested.
        HttpResponseMessage evidenceResponse = await host.SendJsonAsync(
            HttpMethod.Get, $"/catalog/{baseWorkflowId}/versions/{versionNumber}/evidence", "{}", "catalog:read");
        evidenceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using (Stj.JsonDocument evidence = Stj.JsonDocument.Parse(await evidenceResponse.Content.ReadAsStringAsync()))
        {
            evidence.RootElement.GetProperty("packageHash").GetString()!.Length.ShouldBe(64);
            evidence.RootElement.GetProperty("suite").GetProperty("total").GetInt32().ShouldBe(1);
            evidence.RootElement.GetProperty("suite").GetProperty("passed").GetInt32().ShouldBe(1);
            Stj.JsonElement entry = evidence.RootElement.GetProperty("scenarios")[0];
            entry.GetProperty("name").GetString().ShouldBe("happy");
            entry.GetProperty("passed").GetBoolean().ShouldBeTrue();
            entry.GetProperty("pathSummary").GetString()!.ShouldContain("get-pet");
        }
    }

    [TestMethod]
    public async Task Publish_refuses_failing_suites_and_invalid_documents_with_422()
    {
        await using Scoped host = await StartAsync(withSimulator: true);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);

        // A failing scenario refuses the publish with the suite report; requireScenarios:false overrides.
        const string failing = """
            {"name":"sad","inputs":{"petId":"42"},
             "mocks":[{"source":"petstore","operationId":"getPet","responses":[{"status":404}]}],
             "expect":{"outcome":"completed"}}
            """;
        (await host.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/sad", failing, "workspace:write"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage refused = await host.SendJsonAsync(
            HttpMethod.Post, $"/workspace/workflows/{id}/publish",
            """{"owner":{"name":"Team","email":"team@example.com"}}""", "catalog:write");
        refused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using (Stj.JsonDocument refusal = Stj.JsonDocument.Parse(await refused.Content.ReadAsStringAsync()))
        {
            refusal.RootElement.GetProperty("reason").GetString().ShouldBe("scenarios");
            refusal.RootElement.GetProperty("suite").GetProperty("failed").GetInt32().ShouldBe(1);
        }

        (await host.SendJsonAsync(
            HttpMethod.Post, $"/workspace/workflows/{id}/publish",
            """{"owner":{"name":"Team","email":"team@example.com"},"requireScenarios":false}""", "catalog:write"))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        // An invalid document refuses with the diagnostics.
        string invalid = await host.CreateWorkingCopyAsync(
            """{"arazzo":"1.1.0","info":{"title":"x","version":"1"},"workflows":[{"workflowId":"wf","steps":[{"stepId":"a","operationId":"op","onSuccess":[{"name":"jump","type":"goto","stepId":"ghost"}]}]}]}""",
            sourceDoc: null);
        HttpResponseMessage invalidRefused = await host.SendJsonAsync(
            HttpMethod.Post, $"/workspace/workflows/{invalid}/publish",
            """{"owner":{"name":"Team","email":"team@example.com"}}""", "catalog:write");
        invalidRefused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using Stj.JsonDocument invalidRefusal = Stj.JsonDocument.Parse(await invalidRefused.Content.ReadAsStringAsync());
        invalidRefusal.RootElement.GetProperty("reason").GetString().ShouldBe("validation");
        invalidRefusal.RootElement.GetProperty("diagnostics").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public async Task Simulation_fails_closed_when_the_deployment_wires_no_simulator()
    {
        await using Scoped host = await StartAsync(withSimulator: false);
        string id = await host.CreateWorkingCopyAsync(WorkflowDoc, PetstoreDoc);
        (await host.SendJsonAsync(HttpMethod.Post, $"/workspace/workflows/{id}/simulate", SimulateBody, "workspace:read"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
                HttpMethod.Post, "/workspace/workflows", $$"""{"name":"sim-test","document":{{workflowDoc}}}""", "workspace:write");
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

        public async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body, string? scope)
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
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