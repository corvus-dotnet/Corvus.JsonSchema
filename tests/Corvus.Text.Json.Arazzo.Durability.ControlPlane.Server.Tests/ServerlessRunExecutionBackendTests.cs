// <copyright file="ServerlessRunExecutionBackendTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Proves the runner-side <see cref="ServerlessRunExecutionBackend"/> advances a run by invoking its serverless
/// function over HTTP: it posts the run id and environment, returns the reported outcome, advertises Isolated
/// isolation, warms nothing, and throws on a failed invocation so the run stays claimable for the dispatcher's retry.
/// </summary>
[TestClass]
public class ServerlessRunExecutionBackendTests
{
    private static readonly Uri FunctionUrl = new("https://fn.example/invoke");
    private static readonly Uri CheckpointBaseUrl = new("https://runner.example/");

    [TestMethod]
    public async Task Advances_a_run_by_posting_its_id_and_environment_and_returns_the_reported_outcome()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"outcome":"Completed"}""");
        using var http = new HttpClient(handler);
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        WorkflowRunResultKind kind = await backend.AdvanceAsync(run, default);

        // The invocation POSTed the run id + environment to the resolved function URL and returned its outcome.
        kind.ShouldBe(WorkflowRunResultKind.Completed);
        handler.LastMethod.ShouldBe(HttpMethod.Post);
        handler.LastUrl.ShouldBe("https://fn.example/invoke");
        handler.LastBody.ShouldContain("run-1");
        handler.LastBody.ShouldContain("production");

        // Model B: the invocation advertises the checkpoint base URL the function checkpoints back to (§6b).
        handler.LastBody.ShouldContain("https://runner.example/");

        // The token the issuer minted for this run rides the invocation (ADR 0062); the function checkpoints with nothing else.
        handler.LastBody.ShouldContain("\"checkpointToken\":\"token-for-run-1\"");
    }

    [TestMethod]
    public async Task Every_invocation_is_authenticated_over_its_final_url_and_exact_body()
    {
        // V-36 and V-37 of the 2026-08-07 audit: the invocation left the runner with no credential, so an Azure trigger
        // had to be anonymous and an AWS_IAM Function URL could not be called at all.
        var handler = new StubHandler(HttpStatusCode.OK, """{"outcome":"Completed"}""");
        using var http = new HttpClient(handler);
        var authenticator = new StampingAuthenticator();
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, authenticator, Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        await backend.AdvanceAsync(run, default);

        // The credential the authenticator added is on the wire, and what it was shown is what was sent.
        handler.LastInvokeCredential.ShouldBe("stamped");
        authenticator.SeenUrl.ShouldBe("https://fn.example/invoke");
        authenticator.SeenBody.ShouldBe(handler.LastBody);
    }

    [TestMethod]
    public async Task An_invocation_the_authenticator_refuses_is_never_sent()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"outcome":"Completed"}""");
        using var http = new HttpClient(handler);
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, LoopbackServerlessInvokeAuthenticator.Instance, Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        // The loopback authenticator adds no credential, so it refuses a function that is not on this machine.
        await Should.ThrowAsync<InvalidOperationException>(async () => await backend.AdvanceAsync(run, default));
        handler.LastUrl.ShouldBeNull();
    }

    [TestMethod]
    public async Task The_loopback_authenticator_admits_a_function_on_this_machine()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"outcome":"Completed"}""");
        using var http = new HttpClient(handler);
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(new Uri("http://127.0.0.1:7071/api/invoke")), CheckpointBaseUrl, LoopbackServerlessInvokeAuthenticator.Instance, Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        (await backend.AdvanceAsync(run, default)).ShouldBe(WorkflowRunResultKind.Completed);
    }

    [TestMethod]
    public async Task Maps_a_faulted_outcome()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"outcome":"Faulted"}""");
        using var http = new HttpClient(handler);
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        (await backend.AdvanceAsync(run, default)).ShouldBe(WorkflowRunResultKind.Faulted);
    }

    [TestMethod]
    public async Task A_null_outcome_maps_to_a_benign_suspended()
    {
        // The function reports a null/absent outcome when it found the run not dispatchable (a duplicate of a settled
        // run). The checkpoint is authoritative, so the informational return is a benign Suspended, not a made-up terminal.
        var handler = new StubHandler(HttpStatusCode.OK, """{"outcome":null}""");
        using var http = new HttpClient(handler);
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        (await backend.AdvanceAsync(run, default)).ShouldBe(WorkflowRunResultKind.Suspended);
    }

    [TestMethod]
    public async Task A_failed_invocation_throws_so_the_run_stays_claimable()
    {
        var handler = new StubHandler(HttpStatusCode.InternalServerError, null);
        using var http = new HttpClient(handler);
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), Token);
        using WorkflowRun run = NewRun("run-1", "adopt-v1", "production");

        // A 5xx from the function throws; the dispatcher never released the lease on a completed advance, so its
        // lease and retry re-claim and re-invoke the run rather than dropping it.
        await Should.ThrowAsync<HttpRequestException>(async () => await backend.AdvanceAsync(run, default));
    }

    [TestMethod]
    public void Advertises_isolated_isolation_and_warms_nothing()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, """{"outcome":"Completed"}"""));
        var backend = new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), Token);

        backend.IsolationModel.ShouldBe(RunIsolationModel.Isolated);

        // The baked function is kept warm by the platform, so PrepareAsync has nothing to do.
        backend.PrepareAsync("adopt", 1, default).IsCompletedSuccessfully.ShouldBeTrue();
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        using var http = new HttpClient(new StubHandler(HttpStatusCode.OK, null));

        Should.Throw<ArgumentNullException>(() => new ServerlessRunExecutionBackend(null!, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), Token));
        Should.Throw<ArgumentNullException>(() => new ServerlessRunExecutionBackend(http, null!, CheckpointBaseUrl, new StampingAuthenticator(), Token));
        Should.Throw<ArgumentNullException>(() => new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), null!, new StampingAuthenticator(), Token));

        // The invoke authenticator is required (ADR 0059 decision 4): there is no anonymous invocation to fall back to.
        Should.Throw<ArgumentNullException>(() => new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, null!, Token));

        // The token issuer is required too (ADR 0062): a backend that minted no token would dispatch runs that could never checkpoint.
        Should.Throw<ArgumentNullException>(() => new ServerlessRunExecutionBackend(http, (_, _) => new ValueTask<Uri>(FunctionUrl), CheckpointBaseUrl, new StampingAuthenticator(), null!));
    }

    // Stands in for CheckpointToken.Issue: the backend carries whatever the issuer mints, keyed by the run it is for.
    private static string Token(WorkflowRunAddress address) => "token-for-" + address.RunId.Value;

    private static WorkflowRun NewRun(string runId, string workflowId, string environment)
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        return WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, environment);
    }

    // A one-shot stub: it records the request it saw and returns a fixed status with an optional JSON body.
    private sealed class StubHandler(HttpStatusCode status, string? json) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public string? LastUrl { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        public string? LastInvokeCredential { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.LastMethod = request.Method;
            this.LastUrl = request.RequestUri?.ToString();
            this.LastInvokeCredential = request.Headers.TryGetValues("x-test-invoke-credential", out IEnumerable<string>? values) ? values.Single() : null;
            this.LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(status)
            {
                Content = json is null ? new StringContent(string.Empty) : new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
    }

    // Stands in for a platform's authenticator: it stamps a header and records what it was shown.
    private sealed class StampingAuthenticator : IServerlessInvokeAuthenticator
    {
        public string? SeenUrl { get; private set; }

        public string? SeenBody { get; private set; }

        public ValueTask AuthenticateAsync(HttpRequestMessage request, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
        {
            this.SeenUrl = request.RequestUri?.ToString();
            this.SeenBody = Encoding.UTF8.GetString(body.Span);
            request.Headers.Add("x-test-invoke-credential", "stamped");
            return ValueTask.CompletedTask;
        }
    }
}