// <copyright file="WorkflowCheckpointEndpointsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Serverless;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of the runner's serverless checkpoint HTTP surface: the octet-stream <c>GET</c>/<c>POST
/// /runs/{runId}/checkpoint</c> endpoints, the write-sequence + ETag headers, the malformed-body and missing-header
/// rejections, the authenticated-principal gate, and — the capstone — the function-side <c>HttpWorkflowStateStore</c>
/// round-tripping a run through the live surface, proving both halves of the wire contract agree.
/// </summary>
[TestClass]
public sealed class WorkflowCheckpointEndpointsTests
{
    private const string SeqHeader = "X-Arazzo-Checkpoint-Seq";
    private static readonly WorkflowRunId Run = new("run-1");

    [TestMethod]
    public async Task Get_of_an_unknown_run_is_404()
    {
        await using Host host = await Host.StartAsync();

        HttpResponseMessage response = await host.GetCheckpointAsync("missing");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Get_returns_the_checkpoint_bytes_etag_and_sequence()
    {
        await using Host host = await Host.StartAsync();
        byte[] checkpoint = RealCheckpoint(WorkflowRunStatus.Running);
        await host.Store.SaveAsync(Run, checkpoint, ProjectIndex(checkpoint), WorkflowEtag.None, default);

        HttpResponseMessage response = await host.GetCheckpointAsync(Run.Value);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(checkpoint);
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.GetValues(SeqHeader).Single().ShouldBe("0");
    }

    [TestMethod]
    public async Task Post_applies_a_checkpoint_and_a_later_get_returns_it()
    {
        await using Host host = await Host.StartAsync();
        byte[] checkpoint = RealCheckpoint(WorkflowRunStatus.Completed);

        HttpResponseMessage posted = await host.PostCheckpointAsync(Run.Value, checkpoint, sequence: 1);

        posted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(checkpoint);

        HttpResponseMessage got = await host.GetCheckpointAsync(Run.Value);
        got.Headers.GetValues(SeqHeader).Single().ShouldBe("1");
    }

    [TestMethod]
    public async Task Post_of_a_stale_sequence_is_a_benign_no_op()
    {
        await using Host host = await Host.StartAsync();
        byte[] newer = RealCheckpoint(WorkflowRunStatus.Running, cursor: 2);
        byte[] older = RealCheckpoint(WorkflowRunStatus.Running, cursor: 1);

        (await host.PostCheckpointAsync(Run.Value, newer, sequence: 2)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A late, lower-sequenced arrival still returns success (the caller only cares that the terminal committed) but
        // does not regress the stored state.
        (await host.PostCheckpointAsync(Run.Value, older, sequence: 1)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await host.Store.LoadAsync(Run, default))!.Value.Utf8.ToArray().ShouldBe(newer);
    }

    [TestMethod]
    public async Task Post_without_a_sequence_header_is_400()
    {
        await using Host host = await Host.StartAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/runs/{Run.Value}/checkpoint")
        {
            Content = OctetStream(RealCheckpoint(WorkflowRunStatus.Running)),
        };
        HttpResponseMessage response = await host.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Post_of_a_non_checkpoint_body_is_400()
    {
        await using Host host = await Host.StartAsync();

        HttpResponseMessage response = await host.PostCheckpointAsync(Run.Value, [1, 2, 3], sequence: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.Store.LoadAsync(Run, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_secured_mode_requires_an_authenticated_caller()
    {
        await using Host host = await Host.StartAsync(ControlPlaneSecurityMode.ScopesOnly);

        // No principal: the endpoint's RequireAuthorization rejects the request.
        (await host.GetCheckpointAsync("missing")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await host.PostCheckpointAsync(Run.Value, RealCheckpoint(WorkflowRunStatus.Running), 1)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // An authenticated principal is admitted (the unknown run then 404s).
        (await host.GetCheckpointAsync("missing", scope: "runs:read")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_function_side_store_round_trips_a_run_through_the_runner_surface()
    {
        // The capstone: drive the real function-side HttpWorkflowStateStore (6a) against the live runner surface, so the
        // whole wire contract — load, fire-and-forget save with a monotonic sequence, and the flush barrier — is proven
        // end to end rather than against a stub.
        await using Host host = await Host.StartAsync();
        byte[] initial = RealCheckpoint(WorkflowRunStatus.Running, cursor: 1);
        await host.Store.SaveAsync(Run, initial, ProjectIndex(initial), WorkflowEtag.None, default);

        await using var functionStore = new HttpWorkflowStateStore(host.Client);

        // The function loads the run, advances it, and checks a new checkpoint in.
        WorkflowCheckpoint? loaded = await functionStore.LoadAsync(Run, default);
        loaded.ShouldNotBeNull();
        loaded!.Value.Utf8.ToArray().ShouldBe(initial);

        byte[] advanced = RealCheckpoint(WorkflowRunStatus.Completed, cursor: 2);
        await functionStore.SaveAsync(Run, advanced, ProjectIndex(advanced), default, default);
        await functionStore.FlushAsync(default);

        // The runner terminated the fire-and-forget save into the store, re-projecting the index from the bytes.
        WorkflowCheckpoint stored = (await host.Store.LoadAsync(Run, default))!.Value;
        stored.Utf8.ToArray().ShouldBe(advanced);
        WorkflowCheckpointSerializer.ProjectIndex(stored.Utf8).Status.ShouldBe(WorkflowRunStatus.Completed);
    }

    private static byte[] RealCheckpoint(WorkflowRunStatus status, int cursor = 0, long sequence = 1)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            Run,
            "petWorkflow",
            status,
            cursor,
            sequence,
            new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            updatedAt: new DateTimeOffset(2026, 3, 4, 5, 10, 0, TimeSpan.Zero));
    }

    private static WorkflowRunIndexEntry ProjectIndex(byte[] checkpoint) => WorkflowCheckpointSerializer.ProjectIndex(checkpoint);

    private static ByteArrayContent OctetStream(byte[] bytes)
        => new(bytes) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } };

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public InMemoryWorkflowStateStore Store { get; } = store;

        public static async Task<Host> StartAsync(ControlPlaneSecurityMode securityMode = ControlPlaneSecurityMode.Open)
        {
            var store = new InMemoryWorkflowStateStore();
            var management = new SecuredWorkflowManagement(store, "ops");
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), store, "ops");

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
            app.MapArazzoControlPlane(management, catalog, new InMemoryRunnerRegistry(), securityMode, workflowStateStore: store);
            await app.StartAsync();

            return new Host(app, app.GetTestClient(), store);
        }

        public Task<HttpResponseMessage> GetCheckpointAsync(string runId, string? scope = null)
            => this.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/runs/{runId}/checkpoint"), scope);

        public Task<HttpResponseMessage> PostCheckpointAsync(string runId, byte[] body, long sequence, string? scope = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/runs/{runId}/checkpoint") { Content = OctetStream(body) };
            request.Headers.Add(SeqHeader, sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return this.SendAsync(request, scope);
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await app.DisposeAsync();
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string? scope)
        {
            if (scope is not null)
            {
                request.Headers.Add(ScopeAuthHandler.ScopeHeader, scope);
            }

            return this.Client.SendAsync(request);
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