// <copyright file="CliScenariosTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Tests;

/// <summary>
/// Tests for <c>scenarios run</c> (workflow-designer design §4.5) — the CI story. Standalone runs
/// host the simulator in-process from files on disk (no control plane); remote runs execute a
/// working copy's stored suite over loopback HTTP. CI grade: deterministic ordering, JSON/JUnit
/// reports (the JSON report is the suite-report shape publish embeds as evidence), and non-zero
/// exit on any failed expectation.
/// </summary>
public sealed partial class CliIntegrationTests
{
    private const string ScenarioWorkflowDoc = """
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

    private const string ScenarioPetstoreDoc = """
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
                "responses": { "200": { "description": "adopted" }, "default": { "description": "unexpected" } } }
            }
          }
        }
        """;

    private const string HappyScenario = """
        {"name":"happy","inputs":{"petId":"42"},
         "mocks":[{"source":"petstore","operationId":"getPet","responses":[{"status":200,"body":{"name":"Fido"}}]},
                  {"source":"petstore","operationId":"adoptPet","responses":[{"status":200}]}],
         "expect":{"outcome":"completed","outputs":[{"condition":"$outputs.name == 'Fido'"}]}}
        """;

    private const string SadScenario = """
        {"name":"sad","inputs":{"petId":"42"},
         "mocks":[{"source":"petstore","operationId":"getPet","responses":[{"status":404}]}],
         "expect":{"outcome":"completed"}}
        """;

    [TestMethod]
    public async Task Scenarios_run_standalone_passes_a_green_suite_and_writes_the_reports()
    {
        using TempSuite suite = TempSuite.Create(("happy", HappyScenario));
        string jsonReport = Path.Combine(suite.Root, "out", "suite.json");
        string junitReport = Path.Combine(suite.Root, "out", "junit.xml");

        (int exit, string stdout, _) = await RunCliAsync(
            "scenarios", "run", "--workflow", suite.WorkflowPath,
            "--scenarios", Path.Combine(suite.Root, "scenarios", "**", "*.scenario.json"),
            "--report", $"json={jsonReport}", "--report", $"junit={junitReport}");

        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("✓ happy (completed)");
        stdout.ShouldContain("1/1 scenarios passed");

        // The JSON report is the suite-report shape publish embeds as evidence.
        using (Stj.JsonDocument report = Stj.JsonDocument.Parse(await File.ReadAllTextAsync(jsonReport)))
        {
            report.RootElement.GetProperty("total").GetInt32().ShouldBe(1);
            report.RootElement.GetProperty("passed").GetInt32().ShouldBe(1);
            Stj.JsonElement entry = report.RootElement.GetProperty("results")[0];
            entry.GetProperty("scenario").GetString().ShouldBe("happy");
            entry.GetProperty("passed").GetBoolean().ShouldBeTrue();
            entry.GetProperty("trace").GetProperty("steps").GetArrayLength().ShouldBe(2);
        }

        string junit = await File.ReadAllTextAsync(junitReport);
        junit.ShouldContain("<testsuite name=\"arazzo-scenarios\" tests=\"1\" failures=\"0\"");
        junit.ShouldContain("<testcase name=\"happy\"");
    }

    [TestMethod]
    public async Task Scenarios_run_standalone_fails_the_build_on_a_failed_expectation()
    {
        using TempSuite suite = TempSuite.Create(("happy", HappyScenario), ("sad", SadScenario));
        string junitReport = Path.Combine(suite.Root, "out", "junit.xml");

        (int exit, string stdout, _) = await RunCliAsync(
            "scenarios", "run", "--workflow", suite.WorkflowPath,
            "--scenarios", Path.Combine(suite.Root, "scenarios", "**", "*.scenario.json"),
            "--report", $"junit={junitReport}", "--github-annotations");

        exit.ShouldBe(1, stdout);
        stdout.ShouldContain("✓ happy (completed)");
        stdout.ShouldContain("✗ sad (faulted)");
        stdout.ShouldContain("expected completed; was faulted");
        stdout.ShouldContain("::error title=Scenario 'sad'::outcome:");
        stdout.ShouldContain("1/2 scenarios passed, 1 failed.");

        string junit = await File.ReadAllTextAsync(junitReport);
        junit.ShouldContain("failures=\"1\"");
        junit.ShouldContain("<failure message=\"outcome: faulted\"");
    }

    [TestMethod]
    public async Task Scenarios_run_standalone_filters_by_name_and_validates_its_flags()
    {
        using TempSuite suite = TempSuite.Create(("happy", HappyScenario), ("sad", SadScenario));

        // --filter narrows the matched set: only 'happy' runs, so the suite is green.
        (int exit, string stdout, _) = await RunCliAsync(
            "scenarios", "run", "--workflow", suite.WorkflowPath,
            "--scenarios", Path.Combine(suite.Root, "scenarios", "**", "*.scenario.json"),
            "--filter", "hap*");
        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("1/1 scenarios passed");

        // Exactly one mode: neither / both refuse; standalone without --scenarios refuses.
        (await RunCliAsync("scenarios", "run")).Exit.ShouldNotBe(0);
        (await RunCliAsync("scenarios", "run", "--workflow", suite.WorkflowPath, "--working-copy", "x", "--scenarios", "y")).Exit.ShouldNotBe(0);
        (await RunCliAsync("scenarios", "run", "--workflow", suite.WorkflowPath)).Exit.ShouldNotBe(0);

        // No matching files is an infrastructure failure (2), distinct from a failed suite (1).
        (int noMatch, _, string stderr) = await RunCliAsync(
            "scenarios", "run", "--workflow", suite.WorkflowPath, "--scenarios", Path.Combine(suite.Root, "nowhere", "*.scenario.json"));
        noMatch.ShouldBe(2, stderr);
    }

    [TestMethod]
    public async Task Scenarios_run_remote_executes_the_working_copys_stored_suite()
    {
        await using WorkspaceHost host = await StartWorkspaceAsync();
        string id = await host.CreateWorkingCopyAsync(ScenarioWorkflowDoc, ScenarioPetstoreDoc);
        await host.PutScenarioAsync(id, "happy", HappyScenario);

        (int exit, string stdout, _) = await RunCliAsync(
            "scenarios", "run", "--working-copy", id, "--server", host.Url, "--token", "tester");

        exit.ShouldBe(0, stdout);
        stdout.ShouldContain("✓ happy (completed)");
        stdout.ShouldContain("1/1 scenarios passed");
    }

    private static async Task<(int Exit, string Stdout, string Stderr)> RunCliAsync(params string[] args)
    {
        var outWriter = new StringWriter();
        var errWriter = new StringWriter();
        TextWriter previousOut = Console.Out;
        TextWriter previousError = Console.Error;
        Console.SetOut(outWriter);
        Console.SetError(errWriter);
        try
        {
            int exit = await CliApp.Create().RunAsync(args);
            return (exit, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
        }
    }

    private static async Task<WorkspaceHost> StartWorkspaceAsync()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, "ops");
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");
        var workspaceStore = new Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows.InMemoryWorkspaceWorkflowStore();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services
            .AddAuthentication(WorkspaceBearerHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, WorkspaceBearerHandler>(WorkspaceBearerHandler.SchemeName, _ => { });
        builder.Services.AddArazzoControlPlaneAuthorization();
        builder.Services.AddHttpContextAccessor();

        WebApplication app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapArazzoControlPlane(
            management, catalog, new InMemoryRunnerRegistry(), ControlPlaneSecurityMode.ScopesOnly,
            workspaceWorkflowStore: workspaceStore,
            workflowSimulator: new WorkflowSimulator(new WorkflowExecutorProvider(durable: true)));
        await app.StartAsync();
        return new WorkspaceHost(app, app.Urls.First());
    }

    private sealed class WorkspaceHost(WebApplication app, string url) : IAsyncDisposable
    {
        private readonly HttpClient client = new() { BaseAddress = new Uri(url) };

        public string Url => url;

        public async Task<string> CreateWorkingCopyAsync(string workflowDoc, string sourceDoc)
        {
            HttpResponseMessage created = await this.SendJsonAsync(HttpMethod.Post, "/workspace/workflows", $$"""{"name":"cli-test","document":{{workflowDoc}}}""");
            created.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
            using Stj.JsonDocument doc = Stj.JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            string id = doc.RootElement.GetProperty("id").GetString()!;
            (await this.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/sources/petstore", $$"""{"document":{{sourceDoc}}}""")).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
            return id;
        }

        public async Task PutScenarioAsync(string id, string name, string scenario)
            => (await this.SendJsonAsync(HttpMethod.Put, $"/workspace/workflows/{id}/scenarios/{name}", scenario)).StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);

        public async ValueTask DisposeAsync()
        {
            this.client.Dispose();
            await app.DisposeAsync();
        }

        private async Task<HttpResponseMessage> SendJsonAsync(HttpMethod method, string path, string body)
        {
            using var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "tester");
            return await this.client.SendAsync(request);
        }
    }

    private sealed class WorkspaceBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "WorkspaceBearer";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? header = this.Request.Headers.Authorization;
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(SchemeName);
            identity.AddClaim(new Claim("scope", "workspace:read workspace:write"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    /// <summary>A disposable on-disk suite layout: workflow + source next to it, scenarios under ./scenarios.</summary>
    private sealed class TempSuite : IDisposable
    {
        private TempSuite(string root)
        {
            this.Root = root;
        }

        public string Root { get; }

        public string WorkflowPath => Path.Combine(this.Root, "adopt.arazzo.json");

        public static TempSuite Create(params (string Name, string Json)[] scenarios)
        {
            string root = Path.Combine(Path.GetTempPath(), $"arazzo-cli-scenarios-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "scenarios"));
            File.WriteAllText(Path.Combine(root, "adopt.arazzo.json"), ScenarioWorkflowDoc);
            File.WriteAllText(Path.Combine(root, "petstore.openapi.json"), ScenarioPetstoreDoc);
            foreach ((string name, string json) in scenarios)
            {
                File.WriteAllText(Path.Combine(root, "scenarios", $"{name}.scenario.json"), json);
            }

            return new TempSuite(root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this.Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}