// <copyright file="ControlPlaneRunStarterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text;

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.OpenApi.HttpTransport;

using Shouldly;

namespace Corvus.Text.Json.Arazzo.Runner.Demo.Tests;

/// <summary>
/// The governed run-starter (#896): the durable scheduler fires each due occurrence through the control plane's
/// authenticated run endpoint. These tests pin the request it builds — the versioned URL, the pinned environment, the
/// occurrence idempotency key, the machine-principal bearer token, and the inputs body — and how it reads the run id
/// back, without a live control plane.
/// </summary>
[TestClass]
public sealed class ControlPlaneRunStarterTests
{
    [TestMethod]
    public async Task StartAsync_posts_to_the_governed_run_endpoint_with_the_idempotency_key_and_bearer_token()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted, """{"runId":"run-xyz","status":"Pending","workflowId":"flow-v1"}""");
        using var http = new HttpClient(handler);
        var starter = new ControlPlaneRunStarter(http, new StubAuthentication("tok-1"), "http://cp.example/", "development");

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("""{"petId":5}""");
        WorkflowRunId id = await starter.StartAsync(new WorkflowStartRequest("flow-v1", inputs.RootElement, "sched-a:2026-07-20T09:00:00.0000000Z"), default);

        // The run id is read from the 202 WorkflowRunAccepted body.
        id.Value.ShouldBe("run-xyz");

        // The target versioned id resolves to the catalog run endpoint, pinned to the runner's environment.
        handler.Method.ShouldBe(HttpMethod.Post);
        handler.RequestUri.ShouldBe(new Uri("http://cp.example/arazzo/v1/catalog/flow/versions/1/runs?environment=development"));

        // The occurrence's idempotency key rides an Idempotency-Key header; the machine principal's token rides Bearer.
        handler.IdempotencyKey.ShouldBe("sched-a:2026-07-20T09:00:00.0000000Z");
        handler.Authorization.ShouldBe("Bearer tok-1");

        // The inputs are the request body verbatim.
        handler.Body.ShouldBe("""{"petId":5}""");
    }

    [TestMethod]
    public async Task StartAsync_throws_when_the_control_plane_refuses_the_start()
    {
        var handler = new CapturingHandler(HttpStatusCode.Conflict, """{"title":"No hosting runner"}""");
        using var http = new HttpClient(handler);
        var starter = new ControlPlaneRunStarter(http, new StubAuthentication("tok-1"), "http://cp.example", "development");

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}");
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await starter.StartAsync(new WorkflowStartRequest("flow-v1", inputs.RootElement, "k1"), default));
        ex.Message.ShouldContain("409");
    }

    [TestMethod]
    public async Task StartAsync_throws_for_a_target_id_that_is_not_a_versioned_id()
    {
        var handler = new CapturingHandler(HttpStatusCode.Accepted, """{"runId":"r"}""");
        using var http = new HttpClient(handler);
        var starter = new ControlPlaneRunStarter(http, new StubAuthentication("tok-1"), "http://cp.example", "development");

        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse("{}");
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await starter.StartAsync(new WorkflowStartRequest("flow", inputs.RootElement, "k1"), default));

        // No request was sent for an unparseable target id.
        handler.RequestUri.ShouldBeNull();
    }

    private sealed class StubAuthentication(string token) : IHttpAuthenticationProvider
    {
        public ValueTask AuthenticateAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public string? Authorization { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Method = request.Method;
            this.RequestUri = request.RequestUri;
            this.IdempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? values) ? values.FirstOrDefault() : null;
            this.Authorization = request.Headers.Authorization?.ToString();
            this.Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}