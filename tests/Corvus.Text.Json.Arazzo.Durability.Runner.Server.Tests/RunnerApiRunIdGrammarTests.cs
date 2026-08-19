// <copyright file="RunnerApiRunIdGrammarTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Stj = System.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

using Corvus.Text.Json.Arazzo.Durability.Availability;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// Pins ADR 0065 §9's run-id grammar at the runner-API ingress: a run id is exactly 32 lowercase hex characters,
/// validated at every ingress before any store touch. Each run-addressed operation must refuse a non-conforming id
/// with 400 at the generated validation layer — never carry it into the lease or store machinery, whose refusals
/// (401/404/409) would otherwise become an oracle over a key space the grammar excludes.
/// </summary>
[TestClass]
public sealed class RunnerApiRunIdGrammarTests
{
    private const string Runner = "runner-a";
    private const string Production = "production";
    private const string Version = "adopt-v3";
    private const string LeaseHeader = "X-Arazzo-Lease";
    private const string SequenceHeader = "X-Arazzo-Checkpoint-Seq";

    // Exactly 32 lowercase hex — the native mint (Guid "n") and the grammar ADR 0065 §9 pins.
    private const string ConformingId = "0123456789abcdef0123456789abcdef";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow("run-1", DisplayName = "non-hex (the pre-grammar fixture idiom)")]
    [DataRow("0123456789abcdef0123456789abcdef0", DisplayName = "33 hex characters")]
    [DataRow("0123456789abcdef0123456789abcde", DisplayName = "31 hex characters")]
    [DataRow("0123456789ABCDEF0123456789ABCDEF", DisplayName = "uppercase hex")]
    public async Task A_run_id_outside_the_grammar_is_refused_at_every_run_addressed_ingress(string runId)
    {
        await using Host host = await Host.StartAsync();

        // The lease token is syntactically valid, so the only thing wrong with each request is the id: a refusal
        // must be the grammar's 400, not the lease machinery's answer for an unknown run.
        const string lease = "lease-token";

        (await host.LoadCheckpointAsync(Runner, runId, lease)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.SaveCheckpointAsync(Runner, runId, lease, Checkpoint(ConformingId, WorkflowRunStatus.Running, sequence: 2), 2)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.RenewLeaseAsync(Runner, runId, lease, 300)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.ReleaseLeaseAsync(Runner, runId, lease)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_conforming_run_id_traverses_the_ingress_to_the_handler()
    {
        await using Host host = await Host.StartAsync();
        await host.SeedAsync(ConformingId, WorkflowRunStatus.Pending);

        using HttpResponseMessage claim = await host.ClaimAsync(Runner);
        claim.StatusCode.ShouldBe(HttpStatusCode.OK);
        using Stj.JsonDocument body = Stj.JsonDocument.Parse(await claim.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("runId").GetString().ShouldBe(ConformingId);
        string lease = body.RootElement.GetProperty("lease").GetProperty("token").GetString()!;

        (await host.LoadCheckpointAsync(Runner, ConformingId, lease)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static byte[] Checkpoint(string runId, WorkflowRunStatus status, long sequence)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new WorkflowRunId(runId),
            Version,
            status,
            cursor: 0,
            sequence,
            T0,
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputs: default,
            stepOutputs,
            outputs: default,
            environment: Production,
            updatedAt: T0);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store) : IAsyncDisposable
    {
        public static async Task<Host> StartAsync()
        {
            var clock = new TestClock(T0);
            var store = new InMemoryWorkflowStateStore(clock);
            var bindings = new DeclaredRunnerEnvironmentBindings(new Dictionary<string, IReadOnlyList<string>>
            {
                [Runner] = [Production],
            });

            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddHttpContextAccessor();

            WebApplication app = builder.Build();

            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Principal", out Microsoft.Extensions.Primitives.StringValues principal))
                {
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", principal.ToString())], "test"));
                }

                await next(context);
            });

            app.MapArazzoRunnerApi(store, new InMemoryWorkflowCatalogStore(), new InMemoryAvailabilityStore(), bindings, requireAuthorization: false, timeProvider: clock);
            await app.StartAsync();

            return new Host(app, app.GetTestClient(), store);
        }

        public async ValueTask SeedAsync(string runId, WorkflowRunStatus status)
        {
            byte[] checkpoint = Checkpoint(runId, status, sequence: 1);
            await store.SaveAsync(
                new WorkflowRunId(runId),
                checkpoint,
                WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                WorkflowEtag.None,
                default);
        }

        public Task<HttpResponseMessage> ClaimAsync(string principal)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/claims")
            {
                Content = JsonContent.Create(new { hostedVersions = new[] { Version } }),
            };

            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> LoadCheckpointAsync(string principal, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/environments/{Production}/runs/{runId}/checkpoint");
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> SaveCheckpointAsync(string principal, string runId, string lease, byte[] body, long sequence)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/environments/{Production}/runs/{runId}/checkpoint")
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(LeaseHeader, lease);
            request.Headers.Add(SequenceHeader, sequence.ToString(CultureInfo.InvariantCulture));
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> RenewLeaseAsync(string principal, string runId, string lease, int leaseSeconds)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/environments/{Production}/runs/{runId}/lease/renewal")
            {
                Content = JsonContent.Create(new { leaseSeconds }),
            };
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> ReleaseLeaseAsync(string principal, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/environments/{Production}/runs/{runId}/lease");
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string principal)
        {
            request.Headers.Add("X-Test-Principal", principal);
            return client.SendAsync(request);
        }
    }
}
