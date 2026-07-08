// <copyright file="InProcessDraftRunnerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Sockets;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves §18 slice 3e-2b container-free: the composable in-process <see cref="InProcessDraftRunner"/> claims
/// enqueued <c>$draft</c> runs pinned to its environment, drives each forward-only against a <em>real</em> local
/// HTTP endpoint through the <em>real</em> <see cref="HttpClientTransport"/>, and assembles+caches the
/// <c>SimulationTrace</c>-shaped metadata trace (method/path/status only, no bodies) each run's recorded exchanges
/// produce — reachable through <see cref="InProcessDraftRunner.TryGetTrace"/>. Covers a completed run, a batch of
/// runs pumped together, a faulted (5xx) run, and the environment-pinning boundary.
/// </summary>
[TestClass]
public sealed class InProcessDraftRunnerTests
{
    // A minimal single-step Arazzo 1.1 draft (verbatim shape from DraftRunHostEndToEndTests): getPet against the
    // petstore source, keyed on $statusCode == 200, projecting a petName output and a workflow name output.
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

    private static readonly byte[] WorkflowUtf8 = Encoding.UTF8.GetBytes(WorkflowJson);

    private static readonly IReadOnlyList<KeyValuePair<string, byte[]>> Sources =
        [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))];

    [TestMethod]
    public async Task Pumping_a_pending_draft_run_advances_completes_it_and_caches_a_matching_metadata_trace()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var traceStore = new InMemoryDraftRunTraceStore();
        WorkflowRunId id = await StartDraftAsync(runStore, drafts, environment: "development");

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        await using var runner = new InProcessDraftRunner(
            runStore, "runner-dev", "development", drafts, traceStore, new WorkflowExecutorProvider(durable: true), Binder(httpClient));

        int advanced = await runner.RunPendingAsync();

        // The runner claimed the draft, compiled it from the captured bytes, and drove it to completion.
        advanced.ShouldBe(1);
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);

        // Proof of the real transport: the local endpoint received the resolved getPet request.
        endpoint.Requests.Count.ShouldBe(1);
        endpoint.Requests[0].ShouldBe(("GET", "/pets/42"));

        // The ephemeral trace cache holds the run's metadata trace: outcome, outputs, the executed step, and its
        // single metadata-only exchange (method/path/status), with no request or response body anywhere.
        ReadOnlyMemory<byte>? traceUtf8 = await traceStore.GetAsync(id, default);
        traceUtf8.HasValue.ShouldBeTrue();
        using ParsedJsonDocument<JsonElement> traceDoc = ParsedJsonDocument<JsonElement>.Parse(traceUtf8.Value);
        JsonElement trace = traceDoc.RootElement;

        trace.GetProperty("outcome"u8).GetString().ShouldBe("completed");
        trace.GetProperty("outputs"u8).GetProperty("name"u8).GetString().ShouldBe("Fido");
        trace.GetProperty("stepsExecuted"u8).GetInt32().ShouldBe(1);

        JsonElement step = At(trace.GetProperty("steps"u8), 0);
        step.GetProperty("stepId"u8).GetString().ShouldBe("getPet");
        step.GetProperty("status"u8).GetString().ShouldBe("completed");
        step.GetProperty("outputs"u8).GetProperty("petName"u8).GetString().ShouldBe("Fido");

        JsonElement request = At(step.GetProperty("requests"u8), 0);
        request.GetProperty("method"u8).GetString().ShouldBe("get");
        request.GetProperty("path"u8).GetString().ShouldBe("/pets/42");
        request.GetProperty("status"u8).GetInt32().ShouldBe(200);

        AssertNoBodies(trace);
    }

    [TestMethod]
    public async Task Pumping_advances_every_pending_draft_run_in_the_batch_and_caches_a_trace_for_each()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var traceStore = new InMemoryDraftRunTraceStore();

        // Two independent draft runs enqueued for the same environment; one pump must advance both and cache a
        // distinct trace per run id (the correlation of each run with its own recorded exchanges).
        WorkflowRunId first = await StartDraftAsync(runStore, drafts, environment: "development");
        WorkflowRunId second = await StartDraftAsync(runStore, drafts, environment: "development");

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        await using var runner = new InProcessDraftRunner(
            runStore, "runner-dev", "development", drafts, traceStore, new WorkflowExecutorProvider(durable: true), Binder(httpClient));

        int advanced = await runner.RunPendingAsync();

        advanced.ShouldBe(2);
        foreach (WorkflowRunId id in new[] { first, second })
        {
            using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
            completed!.Status.ShouldBe(WorkflowRunStatus.Completed);

            ReadOnlyMemory<byte>? traceUtf8 = await traceStore.GetAsync(id, default);
            traceUtf8.HasValue.ShouldBeTrue();
            using ParsedJsonDocument<JsonElement> traceDoc = ParsedJsonDocument<JsonElement>.Parse(traceUtf8.Value);
            traceDoc.RootElement.GetProperty("outcome"u8).GetString().ShouldBe("completed");
            traceDoc.RootElement.GetProperty("stepsExecuted"u8).GetInt32().ShouldBe(1);
        }
    }

    [TestMethod]
    public async Task An_uncompilable_draft_run_is_faulted_without_aborting_the_rest_of_the_batch()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var traceStore = new InMemoryDraftRunTraceStore();

        // One draft whose captured document does NOT compile (it declares a source that is not attached, so the
        // operation cannot bind), then one valid draft. A single pump must FAULT the bad one (terminal — never
        // re-claimed) and still advance the good one: a bad run must not abort the batch or block every other run.
        WorkflowRunId bad = await StartUncompilableDraftAsync(runStore, drafts, environment: "development");
        WorkflowRunId good = await StartDraftAsync(runStore, drafts, environment: "development");

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        await using var runner = new InProcessDraftRunner(
            runStore, "runner-dev", "development", drafts, traceStore, new WorkflowExecutorProvider(durable: true), Binder(httpClient));

        // The pump does not throw — the failure is isolated to the bad run — and both runs are advanced.
        int advanced = await runner.RunPendingAsync();
        advanced.ShouldBe(2);

        using WorkflowRun? badRun = await WorkflowRun.ResumeAsync(runStore, bad);
        badRun!.Status.ShouldBe(WorkflowRunStatus.Faulted);
        using WorkflowRun? goodRun = await WorkflowRun.ResumeAsync(runStore, good);
        goodRun!.Status.ShouldBe(WorkflowRunStatus.Completed);

        // A second pump does not re-claim the faulted run (it is terminal), so it never blocks the pump again.
        (await runner.RunPendingAsync()).ShouldBe(0);
    }

    [TestMethod]
    public async Task A_draft_run_whose_endpoint_returns_5xx_is_advanced_and_caches_a_faulted_trace()
    {
        using var endpoint = new LocalHttpEndpoint(500, """{"error":"boom"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var traceStore = new InMemoryDraftRunTraceStore();
        WorkflowRunId id = await StartDraftAsync(runStore, drafts, environment: "development");

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        await using var runner = new InProcessDraftRunner(
            runStore, "runner-dev", "development", drafts, traceStore, new WorkflowExecutorProvider(durable: true), Binder(httpClient));

        int advanced = await runner.RunPendingAsync();

        // The runner drove the draft; the real 5xx failed the step's $statusCode == 200 criterion, so the durable
        // executor faulted the run (a resumable terminal), and the cached trace reflects the fault + the exchange.
        advanced.ShouldBe(1);
        using WorkflowRun? faulted = await WorkflowRun.ResumeAsync(runStore, id);
        faulted!.Status.ShouldBe(WorkflowRunStatus.Faulted);

        ReadOnlyMemory<byte>? traceUtf8 = await traceStore.GetAsync(id, default);
        traceUtf8.HasValue.ShouldBeTrue();
        using ParsedJsonDocument<JsonElement> traceDoc = ParsedJsonDocument<JsonElement>.Parse(traceUtf8.Value);
        JsonElement trace = traceDoc.RootElement;

        trace.GetProperty("outcome"u8).GetString().ShouldBe("faulted");
        JsonElement fault = trace.GetProperty("fault"u8);
        fault.GetProperty("stepId"u8).GetString().ShouldBe("getPet");
        fault.GetProperty("error"u8).GetString().ShouldNotBeNullOrEmpty();

        JsonElement request = At(At(trace.GetProperty("steps"u8), 0).GetProperty("requests"u8), 0);
        request.GetProperty("path"u8).GetString().ShouldBe("/pets/42");
        request.GetProperty("status"u8).GetInt32().ShouldBe(500);

        AssertNoBodies(trace);
    }

    [TestMethod]
    public async Task A_draft_run_pinned_to_another_environment_is_not_advanced_by_a_development_runner()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var traceStore = new InMemoryDraftRunTraceStore();

        // The run is pinned to "staging"; the runner serves "development" only.
        WorkflowRunId id = await StartDraftAsync(runStore, drafts, environment: "staging");

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        await using var runner = new InProcessDraftRunner(
            runStore, "runner-dev", "development", drafts, traceStore, new WorkflowExecutorProvider(durable: true), Binder(httpClient));

        int advanced = await runner.RunPendingAsync();

        // The §5.5 environment pin excluded the staging run: nothing advanced, no endpoint call, no trace cached, and
        // the run is left Pending for a runner that serves its environment.
        advanced.ShouldBe(0);
        endpoint.Requests.Count.ShouldBe(0);
        (await traceStore.GetAsync(id, default)).HasValue.ShouldBeFalse();
        using WorkflowRun? pending = await WorkflowRun.ResumeAsync(runStore, id);
        pending!.Status.ShouldBe(WorkflowRunStatus.Pending);
    }

    private static async Task<WorkflowRunId> StartDraftAsync(InMemoryWorkflowStateStore runStore, InMemoryDraftRunStore drafts, string environment)
    {
        var management = new DraftRunManagement(runStore, drafts);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        return await management.StartAsync(DraftStart(environment), inputs.RootElement);
    }

    private static DraftRunStart DraftStart(string environment) => new(
        WorkingCopyId: "wc-1",
        WorkflowId: "adopt",
        DocumentUtf8: WorkflowUtf8,
        Sources: Sources,
        Environment: environment,
        DocumentEtag: "etag-1",
        StartedBy: "alice");

    private static async Task<WorkflowRunId> StartUncompilableDraftAsync(InMemoryWorkflowStateStore runStore, InMemoryDraftRunStore drafts, string environment)
    {
        var management = new DraftRunManagement(runStore, drafts);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("{}"));

        // Declares a source ('missing') but attaches none, so the step's operation cannot bind — the durable compile fails.
        var start = new DraftRunStart(
            WorkingCopyId: "wc-bad",
            WorkflowId: "broken",
            DocumentUtf8: Encoding.UTF8.GetBytes("""{"arazzo":"1.1.0","info":{"title":"Broken","version":"1.0.0"},"sourceDescriptions":[{"name":"missing","type":"openapi"}],"workflows":[{"workflowId":"broken","steps":[{"stepId":"s1","operationId":"nope"}]}]}"""),
            Sources: [],
            Environment: environment,
            DocumentEtag: "etag-bad",
            StartedBy: "alice");
        return await management.StartAsync(start, inputs.RootElement);
    }

    // Binds each declared source to a plain, unauthenticated HttpClientTransport over the shared, test-owned client
    // (disposeClient: false — the resumer disposes the transport per run; the test owns the client). The runner itself
    // wraps each of these in a RecordingApiTransport, so the test supplies no recording layer — mirroring how the
    // production SourceCredentialTransports.CreateBinder binder is wrapped by the runner.
    private static WorkflowTransportBinder Binder(HttpClient client)
        => (WorkflowDescriptor descriptor, SecurityTagSet runTags) =>
        {
            var apiTransports = descriptor.Sources.ToDictionary(
                source => source,
                _ => (IApiTransport)new HttpClientTransport(client, disposeClient: false),
                StringComparer.Ordinal);
            return new WorkflowTransports(apiTransports, null);
        };

    private static JsonElement At(JsonElement array, int index)
    {
        int i = 0;
        foreach (JsonElement element in array.EnumerateArray())
        {
            if (i++ == index)
            {
                return element;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(index));
    }

    // The no-bodies invariant, asserted explicitly: no request or response body property appears anywhere in the trace.
    private static void AssertNoBodies(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty<JsonElement> property in element.EnumerateObject())
                {
                    property.Name.ShouldNotBe("requestBody");
                    property.Name.ShouldNotBe("responseBody");
                    AssertNoBodies(property.Value);
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in element.EnumerateArray())
                {
                    AssertNoBodies(item);
                }

                break;
        }
    }

    // A real HTTP server on a loopback port (verbatim from DraftRunHostEndToEndTests): accepts requests over a real
    // socket, records each one, and returns a fixed JSON status + body, so the real HttpClientTransport genuinely
    // reaches the wire and the tests can assert exactly what was called.
    private sealed class LocalHttpEndpoint : IDisposable
    {
        private readonly HttpListener listener = new();
        private readonly List<(string Method, string Path)> requests = [];
        private readonly Lock gate = new();
        private readonly int statusCode;
        private readonly byte[] body;

        public LocalHttpEndpoint(int statusCode, string body)
        {
            this.statusCode = statusCode;
            this.body = Encoding.UTF8.GetBytes(body);

            int port = FreeLoopbackPort();
            this.BaseAddress = new Uri($"http://127.0.0.1:{port}/");
            this.listener.Prefixes.Add(this.BaseAddress.AbsoluteUri);
            this.listener.Start();
            _ = Task.Run(this.AcceptLoopAsync);
        }

        public Uri BaseAddress { get; }

        public IReadOnlyList<(string Method, string Path)> Requests
        {
            get
            {
                lock (this.gate)
                {
                    return this.requests.ToArray();
                }
            }
        }

        public void Dispose() => this.listener.Close();

        private static int FreeLoopbackPort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (true)
            {
                HttpListenerContext context;
                try
                {
                    context = await this.listener.GetContextAsync();
                }
                catch (HttpListenerException)
                {
                    return; // the listener was closed by Dispose.
                }
                catch (ObjectDisposedException)
                {
                    return; // the listener was closed by Dispose.
                }

                lock (this.gate)
                {
                    this.requests.Add((context.Request.HttpMethod, context.Request.Url!.AbsolutePath));
                }

                HttpListenerResponse response = context.Response;
                response.StatusCode = this.statusCode;
                response.ContentType = "application/json";
                response.ContentLength64 = this.body.Length;
                await response.OutputStream.WriteAsync(this.body);
                response.Close();
            }
        }
    }
}