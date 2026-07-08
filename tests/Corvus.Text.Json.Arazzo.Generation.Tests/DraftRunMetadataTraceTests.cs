// <copyright file="DraftRunMetadataTraceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves §18 slice 3e-2a container-free: a <em>real</em> host-executed run, driven through a
/// <see cref="RecordingApiTransport"/> wrapped around the real <see cref="HttpClientTransport"/>, yields a
/// <c>SimulationTrace</c>-shaped metadata trace (<see cref="MetadataTraceAssembler"/>) whose outcome, outputs,
/// executed steps, and per-step exchanges (method/path/status only) match the run — and which carries
/// <em>no</em> request or response body anywhere. Covers a completed run, a faulted (5xx) run, and a paused
/// (§18 debugger stop) run.
/// </summary>
[TestClass]
public sealed class DraftRunMetadataTraceTests
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

    // A two-step Arazzo 1.0 workflow (verbatim from DurableRunPauseTests): createAccount is state index 0,
    // verifyIdentity is state index 1, so an after-each-step pause fires at cursor 1 having run only createAccount.
    private const string TwoStepWorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Onboard", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "api", "url": "./api.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "onboard",
              "inputs": { "type": "object", "properties": { "email": { "type": "string" } } },
              "steps": [
                {
                  "stepId": "createAccount",
                  "operationId": "createAccount",
                  "outputs": { "accountId": "$response.body#/accountId" }
                },
                {
                  "stepId": "verifyIdentity",
                  "operationId": "verifyIdentity",
                  "parameters": [ { "name": "accountId", "in": "path", "value": "$steps.createAccount.outputs.accountId" } ],
                  "requestBody": { "contentType": "application/json", "payload": { "fullName": "Ada Lovelace" } },
                  "outputs": { "score": "$response.body#/score" }
                }
              ]
            }
          ]
        }
        """;

    private const string TwoStepOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Api", "version": "1.0.0" },
          "paths": {
            "/accounts": {
              "post": {
                "operationId": "createAccount",
                "responses": { "201": { "description": "created", "content": { "application/json": { "schema": { "type": "object", "properties": { "accountId": { "type": "string" } } } } } } }
              }
            },
            "/accounts/{accountId}/identity": {
              "post": {
                "operationId": "verifyIdentity",
                "parameters": [ { "name": "accountId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "requestBody": { "content": { "application/json": { "schema": { "type": "object", "properties": { "fullName": { "type": "string" } } } } } },
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "score": { "type": "number" } } } } } } }
              }
            }
          }
        }
        """;

    private static readonly byte[] WorkflowUtf8 = Encoding.UTF8.GetBytes(WorkflowJson);

    private static readonly IReadOnlyList<KeyValuePair<string, byte[]>> Sources =
        [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))];

    [TestMethod]
    public async Task A_completed_draft_run_yields_a_metadata_trace_matching_the_run_with_no_bodies()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var recording = new DraftRunRecording();

        WorkflowRunId id = await RunDraftAsync(runStore, endpoint, recording);

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);

        // The recording decorator captured exactly the one metadata-only exchange the run made.
        IReadOnlyList<RecordedApiExchange> exchanges = recording.Exchanges;
        exchanges.Count.ShouldBe(1);
        exchanges[0].Method.ShouldBe(OperationMethod.Get);
        exchanges[0].Path.ShouldBe("/pets/42");
        exchanges[0].StatusCode.ShouldBe(200);

        using ParsedJsonDocument<JsonElement> traceDoc = await AssembleAsync(runStore, id, exchanges);
        JsonElement trace = traceDoc.RootElement;

        trace.GetProperty("outcome"u8).GetString().ShouldBe("completed");
        trace.GetProperty("outputs"u8).GetProperty("name"u8).GetString().ShouldBe("Fido");
        trace.GetProperty("stepsExecuted"u8).GetInt32().ShouldBe(1);

        JsonElement steps = trace.GetProperty("steps"u8);
        Count(steps).ShouldBe(1);
        JsonElement step = At(steps, 0);
        step.GetProperty("stepId"u8).GetString().ShouldBe("getPet");
        step.GetProperty("status"u8).GetString().ShouldBe("completed");
        step.GetProperty("outputs"u8).GetProperty("petName"u8).GetString().ShouldBe("Fido");

        JsonElement requests = step.GetProperty("requests"u8);
        Count(requests).ShouldBe(1);
        JsonElement request = At(requests, 0);
        request.GetProperty("method"u8).GetString().ShouldBe("get");
        request.GetProperty("path"u8).GetString().ShouldBe("/pets/42");
        request.GetProperty("status"u8).GetInt32().ShouldBe(200);

        AssertNoBodies(trace);
    }

    [TestMethod]
    public async Task A_faulted_draft_run_yields_a_faulted_trace_with_a_fault_and_no_bodies()
    {
        using var endpoint = new LocalHttpEndpoint(500, """{"error":"boom"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var recording = new DraftRunRecording();

        WorkflowRunId id = await RunDraftAsync(runStore, endpoint, recording);

        using WorkflowRun? faulted = await WorkflowRun.ResumeAsync(runStore, id);
        faulted!.Status.ShouldBe(WorkflowRunStatus.Faulted);

        IReadOnlyList<RecordedApiExchange> exchanges = recording.Exchanges;
        exchanges.Count.ShouldBe(1);
        exchanges[0].StatusCode.ShouldBe(500);

        using ParsedJsonDocument<JsonElement> traceDoc = await AssembleAsync(runStore, id, exchanges);
        JsonElement trace = traceDoc.RootElement;

        trace.GetProperty("outcome"u8).GetString().ShouldBe("faulted");

        JsonElement fault = trace.GetProperty("fault"u8);
        fault.GetProperty("stepId"u8).GetString().ShouldBe("getPet");
        fault.GetProperty("error"u8).GetString().ShouldNotBeNullOrEmpty();
        fault.GetProperty("attempt"u8).ValueKind.ShouldBe(JsonValueKind.Number);

        JsonElement steps = trace.GetProperty("steps"u8);
        Count(steps).ShouldBe(1);
        JsonElement step = At(steps, 0);
        step.GetProperty("stepId"u8).GetString().ShouldBe("getPet");
        step.GetProperty("status"u8).GetString().ShouldBe("faulted");

        JsonElement request = At(step.GetProperty("requests"u8), 0);
        request.GetProperty("method"u8).GetString().ShouldBe("get");
        request.GetProperty("path"u8).GetString().ShouldBe("/pets/42");
        request.GetProperty("status"u8).GetInt32().ShouldBe(500);

        AssertNoBodies(trace);
    }

    [TestMethod]
    public async Task A_paused_run_yields_a_paused_trace_with_pausedBefore_and_no_bodies()
    {
        // The paused trace rides the 3e-1 durable pause seam: a two-step run given an after-each-step pause config
        // suspends at cursor 1 (having run only createAccount) as a Suspended run carrying a Pause wait. Its API call
        // is captured through the SAME recording decorator, here wrapping a mock transport — the assembler reads the
        // checkpoint + the recorded exchanges regardless of whether the inner transport is real HTTP or a mock.
        (InMemoryWorkflowCatalogStore catalog, string versionId) = await CatalogAsync();
        var runStore = new InMemoryWorkflowStateStore();

        var mock = new MockApiTransport();
        mock.SetResponse(OperationMethod.Post, "/accounts", 201, """{"accountId":"acc-42"}""");
        mock.SetResponse(OperationMethod.Post, "/accounts/{accountId}/identity", 200, """{"score":0.92}""");
        var recording = new DraftRunRecording();
        var recorder = new RecordingApiTransport(mock, recording);

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(
            catalog,
            loader,
            (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)recorder, StringComparer.Ordinal), null));

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"email":"ada@example.com"}"""));
        using (WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-paused", versionId, inputs.RootElement))
        {
            await run.EnqueueAsync(default);
        }

        // Advance once with an after-each-step pause: createAccount runs, then the pause fires before verifyIdentity.
        using (WorkflowRun? toAdvance = await WorkflowRun.ResumeAsync(runStore, "run-paused"))
        {
            toAdvance!.SetPause(new WorkflowPauseConfig(AfterEachStep: true, new HashSet<int>()));
            (await resumer.AsResumer()(toAdvance, default)).ShouldBe(WorkflowRunResultKind.Suspended);
        }

        using (WorkflowRun? paused = await WorkflowRun.ResumeAsync(runStore, "run-paused"))
        {
            paused!.Status.ShouldBe(WorkflowRunStatus.Suspended);
            paused.Wait!.Value.Kind.ShouldBe(WorkflowWaitKind.Pause);
        }

        IReadOnlyList<RecordedApiExchange> exchanges = recording.Exchanges;
        exchanges.Count.ShouldBe(1);

        // The runner that set the breakpoint knows the step it paused before; the assembler renders it verbatim.
        using ParsedJsonDocument<JsonElement> traceDoc = await AssembleAsync(runStore, "run-paused", exchanges, pausedBefore: "verifyIdentity");
        JsonElement trace = traceDoc.RootElement;

        trace.GetProperty("outcome"u8).GetString().ShouldBe("paused");
        trace.GetProperty("pausedBefore"u8).GetString().ShouldBe("verifyIdentity");

        // A Pause is represented as outcome=paused + pausedBefore, never as a wait object.
        trace.TryGetProperty("wait"u8, out _).ShouldBeFalse();

        JsonElement steps = trace.GetProperty("steps"u8);
        Count(steps).ShouldBe(1);
        JsonElement step = At(steps, 0);
        step.GetProperty("stepId"u8).GetString().ShouldBe("createAccount");
        step.GetProperty("status"u8).GetString().ShouldBe("completed");
        step.GetProperty("outputs"u8).GetProperty("accountId"u8).GetString().ShouldBe("acc-42");

        JsonElement request = At(step.GetProperty("requests"u8), 0);
        request.GetProperty("method"u8).GetString().ShouldBe("post");
        request.GetProperty("path"u8).GetString().ShouldBe("/accounts");
        request.GetProperty("status"u8).GetInt32().ShouldBe(201);

        AssertNoBodies(trace);
    }

    // ── Composition: run the single-step $draft through the recording decorator over a real HttpClientTransport. ──
    private static async Task<WorkflowRunId> RunDraftAsync(InMemoryWorkflowStateStore runStore, LocalHttpEndpoint endpoint, DraftRunRecording recording)
    {
        var drafts = new InMemoryDraftRunStore();
        var management = new DraftRunManagement(runStore, drafts);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        WorkflowRunId id = await management.StartAsync(DraftStart(), inputs.RootElement);

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        using var resumer = new DraftWorkflowResumer(drafts, new WorkflowExecutorProvider(durable: true), Binder(httpClient, recording));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-dev", runnerEnvironment: "development");

        int dispatched = await dispatcher.DispatchClaimableAsync([DraftRuns.RunWorkflowId], resumer.AsResumer(), default);
        dispatched.ShouldBe(1);
        return id;
    }

    private static DraftRunStart DraftStart() => new(
        WorkingCopyId: "wc-1",
        WorkflowId: "adopt",
        DocumentUtf8: WorkflowUtf8,
        Sources: Sources,
        Environment: "development",
        DocumentEtag: "etag-1",
        StartedBy: "alice");

    // Binds each declared source to a RecordingApiTransport wrapping a fresh HttpClientTransport over the shared,
    // test-owned client, all appending to one shared recording so the test can read the run's metadata-only exchanges
    // after the run (the resumer disposes the transports per run; the shared recording outlives disposal).
    private static WorkflowTransportBinder Binder(HttpClient client, DraftRunRecording recording)
        => (WorkflowDescriptor descriptor, SecurityTagSet runTags) =>
        {
            var apiTransports = new Dictionary<string, IApiTransport>(StringComparer.Ordinal);
            foreach (string source in descriptor.Sources)
            {
                apiTransports[source] = new RecordingApiTransport(new HttpClientTransport(client, disposeClient: false), recording);
            }

            return new WorkflowTransports(apiTransports, null);
        };

    private static async Task<(InMemoryWorkflowCatalogStore Catalog, string VersionId)> CatalogAsync()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        byte[] package = WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(TwoStepWorkflowJson),
            [new("api", Encoding.UTF8.GetBytes(TwoStepOpenApi))]);
        using ParsedJsonDocument<CatalogVersion> versionDoc = await catalog.AddAsync("onboard", package, Meta(), default);
        CatalogVersion version = versionDoc.RootElement;
        ((bool)version.Runnable).ShouldBeTrue();
        return (catalog, (string)version.Ref.WorkflowId);
    }

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");


    private static async Task<ParsedJsonDocument<JsonElement>> AssembleAsync(IWorkflowStateStore store, WorkflowRunId id, IReadOnlyList<RecordedApiExchange> exchanges, string? pausedBefore = null)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            await MetadataTraceAssembler.WriteTraceAsync(writer, store, id, exchanges, pausedBefore);
            writer.Flush();
        }

        return ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenSpan.ToArray());
    }

    // The no-bodies invariant, asserted explicitly: no request or response body property appears anywhere in the
    // trace (the trace shape reserves "requestBody"/"responseBody" for bodies, which this seam never records).
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

    private static int Count(JsonElement array)
    {
        int n = 0;
        foreach (JsonElement _ in array.EnumerateArray())
        {
            n++;
        }

        return n;
    }

    // A real HTTP server on a loopback port (verbatim from DraftRunHostEndToEndTests): accepts requests over a real
    // socket and returns a fixed JSON status + body, so the real HttpClientTransport genuinely reaches the wire.
    private sealed class LocalHttpEndpoint : IDisposable
    {
        private readonly HttpListener listener = new();
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