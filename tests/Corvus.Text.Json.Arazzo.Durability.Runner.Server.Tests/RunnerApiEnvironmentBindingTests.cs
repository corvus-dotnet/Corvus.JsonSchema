// <copyright file="RunnerApiEnvironmentBindingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// Pins the environment half of the runner API's run addressing (ADR 0065 §9): every run-addressed operation
/// names the run's environment, and an environment outside the machine principal's bindings refuses exactly
/// like a lease that is not held — the non-disclosure rule the checkpoint surface documents. Holding a valid
/// lease must not be enough: a principal whose binding to the run's environment is gone (revoked, or never
/// present) is out, whatever token it presents. This is the per-environment half of the revocation fence —
/// the store-side lease expiry is by owner, and on a store without lease administration the binding
/// re-resolution here is the only thing that stops a partially revoked runner riding a still-live lease.
/// </summary>
[TestClass]
public sealed class RunnerApiEnvironmentBindingTests
{
    private const string Runner = "runner-a";
    private const string Development = "development";
    private const string Production = "production";
    private const string Version = "adopt-v3";
    private const string LeaseHeader = "X-Arazzo-Lease";
    private const string SequenceHeader = "X-Arazzo-Checkpoint-Seq";

    private const string ProdRunId = "00000000000000000000000000000b0b";
    private const string DevRunId = "00000000000000000000000000000a0a";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task A_lease_on_a_run_outside_the_principals_bindings_does_not_grant_checkpoint_access()
    {
        // The principal is bound ONLY to development. The run is pinned to production, and the principal holds a
        // genuinely valid lease on it (planted at the store: the shape a leaked token, or a revoked production
        // binding outliving its leases on a store without lease administration, produces). Every checkpoint-lane
        // operation must refuse, indistinguishably from a lease that is not held.
        await using Host host = await Host.StartAsync(boundEnvironments: [Development]);
        await host.SeedAsync(ProdRunId, Production, WorkflowRunStatus.Running);
        string lease = await host.PlantLeaseAsync(ProdRunId, Production);

        (await host.LoadCheckpointAsync(Runner, Production, ProdRunId, lease)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.SaveCheckpointAsync(Runner, Production, ProdRunId, lease, Checkpoint(ProdRunId, Production, WorkflowRunStatus.Running, sequence: 2), 2)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await host.RenewLeaseAsync(Runner, Production, ProdRunId, lease, 300)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [TestMethod]
    [DataRow("Production", DisplayName = "uppercase")]
    [DataRow("prod_1", DisplayName = "underscore")]
    [DataRow("-prod", DisplayName = "leading hyphen")]
    public async Task An_environment_outside_the_grammar_is_refused_at_the_ingress(string environment)
    {
        // The environment half of the address is under the same grammar the control plane enforces (ADR 0065 §9),
        // validated by the generated ingress before any handler code. The lease header is syntactically valid, so
        // the only thing wrong with the request is the environment: the refusal must be the grammar's 400, not the
        // lease machinery's 409.
        await using Host host = await Host.StartAsync(boundEnvironments: [Development]);

        (await host.LoadCheckpointAsync(Runner, environment, DevRunId, "1.lease-token")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await host.RenewLeaseAsync(Runner, environment, DevRunId, "1.lease-token", 300)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task A_lease_on_a_run_inside_the_principals_bindings_grants_checkpoint_access()
    {
        // The positive control with the identical plant shape: same principal, same lease mechanics, the one
        // difference is that the run's environment is inside the bindings.
        await using Host host = await Host.StartAsync(boundEnvironments: [Development]);
        await host.SeedAsync(DevRunId, Development, WorkflowRunStatus.Running);
        string lease = await host.PlantLeaseAsync(DevRunId, Development);

        (await host.LoadCheckpointAsync(Runner, Development, DevRunId, lease)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.RenewLeaseAsync(Runner, Development, DevRunId, lease, 300)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static byte[] Checkpoint(string runId, string environment, WorkflowRunStatus status, long sequence)
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
            environment: environment,
            updatedAt: T0);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store) : IAsyncDisposable
    {
        public static async Task<Host> StartAsync(IReadOnlyList<string> boundEnvironments)
        {
            var clock = new TestClock(T0);
            var store = new InMemoryWorkflowStateStore(clock);
            var bindings = new DeclaredRunnerEnvironmentBindings(new Dictionary<string, IReadOnlyList<string>>
            {
                [Runner] = boundEnvironments,
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

        public async ValueTask SeedAsync(string runId, string environment, WorkflowRunStatus status)
        {
            byte[] checkpoint = Checkpoint(runId, environment, status, sequence: 1);
            await store.SaveAsync(
                new WorkflowRunAddress(environment, new WorkflowRunId(runId)),
                checkpoint,
                WorkflowCheckpointSerializer.ProjectIndex(checkpoint),
                WorkflowEtag.None,
                default);
        }

        public async ValueTask<string> PlantLeaseAsync(string runId, string environment)
        {
            // The wire header is the server-minted composite (epoch + store token), exactly what a claim
            // response would carry; the store token alone is not a presentable lease.
            WorkflowLease? lease = await store.AcquireLeaseAsync(new WorkflowRunAddress(environment, new WorkflowRunId(runId)), Runner, TimeSpan.FromMinutes(5), default);
            return RunnerLeaseToken.Issue(lease!.Value.Epoch, lease.Value.Token);
        }

        public Task<HttpResponseMessage> LoadCheckpointAsync(string principal, string environment, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, CheckpointRoute(environment, runId));
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> SaveCheckpointAsync(string principal, string environment, string runId, string lease, byte[] body, long sequence)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, CheckpointRoute(environment, runId))
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(LeaseHeader, lease);
            request.Headers.Add(SequenceHeader, sequence.ToString(CultureInfo.InvariantCulture));
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> RenewLeaseAsync(string principal, string environment, string runId, string lease, int leaseSeconds)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{LeaseRoute(environment, runId)}/renewal")
            {
                Content = JsonContent.Create(new { leaseSeconds }),
            };
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }

        // The run-addressed routes under the composite (environment, runId) addressing (ADR 0065 §9). Kept in one
        // place so the assertion suite above is the stable part and the route shape changes here alone.
        private static string CheckpointRoute(string environment, string runId) => $"/environments/{environment}/runs/{runId}/checkpoint";

        private static string LeaseRoute(string environment, string runId) => $"/environments/{environment}/runs/{runId}/lease";

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string principal)
        {
            request.Headers.Add("X-Test-Principal", principal);
            return client.SendAsync(request);
        }
    }
}