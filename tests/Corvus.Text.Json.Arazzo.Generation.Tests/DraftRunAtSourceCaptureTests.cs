// <copyright file="DraftRunAtSourceCaptureTests.cs" company="Endjin Limited">
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
/// §15-8a/§3.4 at-source capture for durable debug runs: the runner attaches a step recorder to the
/// run, sub-workflow invocations record through recording-only child scopes (never checkpointing),
/// the assembler prefers captured records with per-step checkpoint merge, and production runs are
/// untouched by construction.
/// </summary>
[TestClass]
public sealed class DraftRunAtSourceCaptureTests
{
    // A parent whose only step invokes a sub-workflow that makes the real HTTP call: the captured
    // trace must nest the child's record (with its exchange) under the parent step's subTrace.
    private const string NestedWorkflowJson = """
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

    private static readonly IReadOnlyList<KeyValuePair<string, byte[]>> Sources =
        [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))];

    [TestMethod]
    public async Task A_nested_debug_run_trace_carries_the_captured_subTrace()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var traceStore = new InMemoryDraftRunTraceStore();

        var management = new DraftRunManagement(runStore, drafts);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":"42"}"""u8.ToArray());
        var start = new DraftRunStart(
            WorkingCopyId: "wc-1",
            WorkflowId: "parent",
            DocumentUtf8: Encoding.UTF8.GetBytes(NestedWorkflowJson),
            Sources: Sources,
            Environment: "development",
            DocumentEtag: "etag-1",
            StartedBy: "alice");
        WorkflowRunId id = await management.StartAsync(start, inputs.RootElement);

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        await using var runner = new InProcessDraftRunner(
            runStore, "runner-dev", "development", drafts, traceStore, new WorkflowExecutorProvider(durable: true), Binder(httpClient));

        int advanced = await runner.RunPendingAsync();
        advanced.ShouldBe(1);

        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);

        // The persisted trace's parent step carries the captured nested trace: the child's real HTTP
        // exchange sits on the CHILD record, not the parent's range, and the root trace has no
        // workflowId (the tray's ascent depends on that).
        ReadOnlyMemory<byte>? traceBytes = await traceStore.GetAsync(id, default);
        traceBytes.ShouldNotBeNull();
        using ParsedJsonDocument<JsonElement> trace = ParsedJsonDocument<JsonElement>.Parse(traceBytes!.Value);
        JsonElement root = trace.RootElement;
        root.GetProperty("outcome"u8).ValueEquals("completed"u8).ShouldBeTrue();
        root.TryGetProperty("workflowId"u8, out _).ShouldBeFalse();

        JsonElement steps = root.GetProperty("steps"u8);
        steps.GetArrayLength().ShouldBe(1);
        JsonElement parent = steps[0];
        parent.GetProperty("stepId"u8).ValueEquals("call-child"u8).ShouldBeTrue();
        parent.GetProperty("status"u8).ValueEquals("completed"u8).ShouldBeTrue();
        parent.TryGetProperty("requests"u8, out _).ShouldBeFalse("the child's exchange belongs to the child record");

        JsonElement subTrace = parent.GetProperty("subTrace"u8);
        subTrace.GetProperty("workflowId"u8).ValueEquals("child"u8).ShouldBeTrue();
        subTrace.GetProperty("outcome"u8).ValueEquals("completed"u8).ShouldBeTrue();
        JsonElement childStep = subTrace.GetProperty("steps"u8)[0];
        childStep.GetProperty("stepId"u8).ValueEquals("get-pet"u8).ShouldBeTrue();
        JsonElement request = childStep.GetProperty("requests"u8)[0];
        request.GetProperty("path"u8).ValueEquals("/pets/42"u8).ShouldBeTrue();
        request.TryGetProperty("requestBody"u8, out _).ShouldBeFalse("metadata-only fidelity holds in nested records");
    }

    [TestMethod]
    public void Captured_records_merge_per_step_with_checkpoint_derived_ones()
    {
        // The checkpoint knows steps a and b (with outputs); the recording captured only b (a ran in
        // an earlier segment on another runner instance). The trace emits a first, checkpoint-derived
        // without requests, then b's captured record enriched with the checkpoint outputs.
        byte[] checkpoint = """{"status":"Completed","stepOutputs":{"a":{"x":1},"b":{"y":2}},"retryCounters":{"a":1}}"""u8.ToArray();
        IReadOnlyList<RecordedStepRecord> captured = [new("b", 0, false, 0, 1)];
        IReadOnlyList<RecordedApiExchange> exchanges = [new(OperationMethod.Get, "/b", 200)];

        using ParsedJsonDocument<JsonElement> trace = Assemble(checkpoint, exchanges, captured);
        JsonElement steps = trace.RootElement.GetProperty("steps"u8);
        steps.GetArrayLength().ShouldBe(2);
        steps[0].GetProperty("stepId"u8).ValueEquals("a"u8).ShouldBeTrue();
        steps[0].GetProperty("attempt"u8).GetInt32().ShouldBe(1);
        steps[0].TryGetProperty("requests"u8, out _).ShouldBeFalse("its exchanges predate this recording");
        steps[1].GetProperty("stepId"u8).ValueEquals("b"u8).ShouldBeTrue();
        steps[1].GetProperty("outputs"u8).GetProperty("y"u8).GetInt32().ShouldBe(2);
        steps[1].GetProperty("requests"u8).GetArrayLength().ShouldBe(1);
    }

    [TestMethod]
    public void A_skip_resumed_step_derives_skipped_from_the_checkpoint_delta()
    {
        // The recording captured x FAULTING; the checkpoint now shows x with outputs — only the
        // durable Skip resume produces that delta (design §10 F3): the faulted attempt is emitted,
        // then a synthetic skipped record with the provided outputs.
        byte[] checkpoint = """{"status":"Completed","stepOutputs":{"x":{"provided":true}}}"""u8.ToArray();
        IReadOnlyList<RecordedStepRecord> captured = [new("x", 0, true, 0, 1)];
        IReadOnlyList<RecordedApiExchange> exchanges = [new(OperationMethod.Get, "/x", 503)];

        using ParsedJsonDocument<JsonElement> trace = Assemble(checkpoint, exchanges, captured);
        JsonElement steps = trace.RootElement.GetProperty("steps"u8);
        steps.GetArrayLength().ShouldBe(2);
        steps[0].GetProperty("status"u8).ValueEquals("faulted"u8).ShouldBeTrue();
        steps[1].GetProperty("skipped"u8).ValueKind.ShouldBe(JsonValueKind.True);
        steps[1].GetProperty("outputs"u8).GetProperty("provided"u8).ValueKind.ShouldBe(JsonValueKind.True);
        steps[1].TryGetProperty("requests"u8, out _).ShouldBeFalse("the skipped record never executed");
    }

    [TestMethod]
    public void An_empty_capture_falls_back_to_checkpoint_derivation_exactly()
    {
        // A recording made before at-source capture existed (or on an unshapeable package) assembles
        // exactly as it always did: the legacy checkpoint derivation is byte-for-byte the no-capture path.
        byte[] checkpoint = """{"status":"Completed","stepOutputs":{"a":{"x":1}}}"""u8.ToArray();
        IReadOnlyList<RecordedApiExchange> exchanges = [new(OperationMethod.Get, "/a", 200)];

        using ParsedJsonDocument<JsonElement> withNull = Assemble(checkpoint, exchanges, capturedSteps: null);
        using ParsedJsonDocument<JsonElement> withEmpty = Assemble(checkpoint, exchanges, capturedSteps: []);
        withNull.RootElement.ToString().ShouldBe(withEmpty.RootElement.ToString());
        withNull.RootElement.GetProperty("steps"u8)[0].GetProperty("requests"u8).GetArrayLength().ShouldBe(1);
    }

    [TestMethod]
    public async Task A_run_without_a_recorder_begins_no_sub_workflow_scope()
    {
        // Production pin: no recording sink means BeginSubWorkflow yields null — the sub-workflow
        // runs untracked and the run's checkpoints are untouched by construction.
        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{}"""u8.ToArray());
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", "wf-v1", inputs.RootElement, environment: "development");
        await run.EnqueueAsync(default);
        run.Recorder.ShouldBeNull();
        ((IWorkflowRun)run).BeginSubWorkflow("step", "sub").ShouldBeNull();
    }

    private static ParsedJsonDocument<JsonElement> Assemble(
        byte[] checkpoint,
        IReadOnlyList<RecordedApiExchange> exchanges,
        IReadOnlyList<RecordedStepRecord>? capturedSteps)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            MetadataTraceAssembler.WriteTrace(writer, checkpoint, exchanges, capturedSteps: capturedSteps);
        }

        return ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenSpan.ToArray().AsMemory());
    }

    private static WorkflowTransportBinder Binder(HttpClient client)
        => (WorkflowDescriptor descriptor, SecurityTagSet runTags) =>
        {
            var apiTransports = descriptor.Sources.ToDictionary(
                source => source,
                _ => (IApiTransport)new HttpClientTransport(client, disposeClient: false),
                StringComparer.Ordinal);
            return new WorkflowTransports(apiTransports, WorkflowTransports.NoMessageTransports);
        };

    // The same fixed-response loopback endpoint the other runner tests use (each keeps its own copy).
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