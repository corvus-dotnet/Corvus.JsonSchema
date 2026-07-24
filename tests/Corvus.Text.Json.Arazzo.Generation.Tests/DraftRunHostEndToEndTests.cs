// <copyright file="DraftRunHostEndToEndTests.cs" company="Endjin Limited">
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
/// Proves §18 slice 3d container-free and end to end: a control plane captures a working-copy draft and enqueues a
/// Pending <c>$draft</c> run (<see cref="DraftRunManagement.StartAsync"/>); an in-process, environment-pinned runner
/// (<see cref="WorkflowDispatcher"/> + <see cref="DraftWorkflowResumer"/>) claims it, compiles the captured document
/// bytes, and drives it forward-only against a <em>real</em> local HTTP endpoint through the <em>real</em>
/// <see cref="HttpClientTransport"/> — no mock transport anywhere on the path. The endpoint records every request it
/// serves, so a green run also proves the transport really reached the wire.
/// </summary>
[TestClass]
public sealed class DraftRunHostEndToEndTests
{
    // A minimal single-step Arazzo 1.1 draft: getPet against the petstore source, keyed on $statusCode == 200. The
    // shape mirrors the HostedWorkflowResumer/WorkflowSimulator fixtures so it compiles and runs through the same
    // provider — the only difference here is the transport is real HTTP, not a mock.
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
    public async Task Claims_a_pending_draft_run_and_executes_it_against_a_real_endpoint_to_completion()
    {
        using var endpoint = new LocalHttpEndpoint(200, """{"name":"Fido"}""");

        // ── Control plane: capture the working-copy draft + enqueue the Pending $draft run. ──
        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var management = new DraftRunManagement(runStore, drafts);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        WorkflowRunId id = await management.StartAsync(DraftStart(), inputs.RootElement);

        // ── Runner: a REAL HttpClientTransport bound to the local endpoint, driven through the draft resumer +
        // dispatcher exactly as a production draft-hosting runner is (the reserved $draft id, pinned to the
        // development environment). ──
        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        using var resumer = new DraftWorkflowResumer(drafts, new WorkflowExecutorProvider(durable: true), Binder(httpClient));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-dev", runnerEnvironment: "development");

        int dispatched = await dispatcher.DispatchClaimableAsync([DraftRuns.RunWorkflowId], resumer.AsResumer(), default);

        // The run was claimed, compiled from the captured bytes, and driven to completion.
        dispatched.ShouldBe(1);
        using WorkflowRun? completed = await WorkflowRun.ResumeAsync(runStore, id);
        completed.ShouldNotBeNull();
        completed!.Status.ShouldBe(WorkflowRunStatus.Completed);

        // Proof of the REAL transport: the local endpoint actually received the getPet request with the resolved
        // path parameter. A mock transport would have left the endpoint untouched.
        endpoint.Requests.Count.ShouldBe(1);
        endpoint.Requests[0].Method.ShouldBe("GET");
        endpoint.Requests[0].Path.ShouldBe("/pets/42");
    }

    [TestMethod]
    public async Task A_draft_run_whose_endpoint_returns_5xx_reaches_faulted()
    {
        using var endpoint = new LocalHttpEndpoint(500, """{"error":"boom"}""");

        var runStore = new InMemoryWorkflowStateStore();
        var drafts = new InMemoryDraftRunStore();
        var management = new DraftRunManagement(runStore, drafts);
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        WorkflowRunId id = await management.StartAsync(DraftStart(), inputs.RootElement);

        using var httpClient = new HttpClient { BaseAddress = endpoint.BaseAddress };
        using var resumer = new DraftWorkflowResumer(drafts, new WorkflowExecutorProvider(durable: true), Binder(httpClient));
        var dispatcher = new WorkflowDispatcher(runStore, "runner-dev", runnerEnvironment: "development");

        int dispatched = await dispatcher.DispatchClaimableAsync([DraftRuns.RunWorkflowId], resumer.AsResumer(), default);

        // The runner claimed and drove the draft; the real 5xx failed the step's $statusCode == 200 criterion, so the
        // durable executor faulted the run (a resumable terminal, not a thrown exception through the dispatcher).
        dispatched.ShouldBe(1);
        endpoint.Requests.Count.ShouldBe(1, "the real transport reached the endpoint before the fault");
        using WorkflowRun? faulted = await WorkflowRun.ResumeAsync(runStore, id);
        faulted.ShouldNotBeNull();
        faulted!.Status.ShouldBe(WorkflowRunStatus.Faulted);
    }

    // TODO(3d): message-wait resume. A durable message wait needs an AsyncAPI channel step, a real IMessageTransport,
    // and a WorkflowWorker delivery — none of which the §18 draft fixtures (or the shared HostedWorkflowResumer /
    // WorkflowSimulator fixtures this file models on, whose only suspend path is a retry TIMER, not a message)
    // currently supply. Rather than force a bespoke, potentially-flaky channel fixture, the message-wait Suspend →
    // deliver → Complete case is deferred; happy + fault above prove the container-free real-transport draft-run host.

    private static DraftRunStart DraftStart() => new(
        WorkingCopyId: "wc-1",
        WorkflowId: "adopt",
        DocumentUtf8: WorkflowUtf8,
        Sources: Sources,
        Environment: "development",
        DocumentEtag: "etag-1",
        StartedBy: "alice");

    // Binds each of the compiled draft's declared API sources to a fresh HttpClientTransport over the shared,
    // test-owned client (disposeClient: false — the resumer disposes the transport per run; the test owns the
    // client). This is the same seam SourceCredentialTransports.CreateBinder fills in production, here with a plain
    // unauthenticated transport because the local endpoint needs no credentials.
    private static WorkflowTransportBinder Binder(HttpClient client)
        => (WorkflowDescriptor descriptor, SecurityTagSet runTags) =>
        {
            var apiTransports = descriptor.Sources.ToDictionary(
                source => source,
                _ => (IApiTransport)new HttpClientTransport(client, disposeClient: false),
                StringComparer.Ordinal);
            return new WorkflowTransports(apiTransports, WorkflowTransports.NoMessageTransports);
        };

    // A real HTTP server on a loopback port: it accepts requests over a real socket, records each one, and returns a
    // fixed JSON status + body. Container-free and standing in for the dev endpoint a draft run calls, it lets the
    // tests assert the real HttpClientTransport actually reached the wire.
    private sealed class LocalHttpEndpoint : IDisposable
    {
        private readonly HttpListener listener = new();
        private readonly List<CapturedRequest> requests = [];
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

        public IReadOnlyList<CapturedRequest> Requests
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
                    this.requests.Add(new CapturedRequest(context.Request.HttpMethod, context.Request.Url!.AbsolutePath));
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

    private readonly record struct CapturedRequest(string Method, string Path);
}