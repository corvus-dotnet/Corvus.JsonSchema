// <copyright file="RunnerApiQuotaEndToEndTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Tests;

/// <summary>
/// The quota refusals on the wire (ADR 0065 decision 3). The guard's own arithmetic is covered by
/// <see cref="TokenBucketRunnerQuotaGuardTests"/>; what is proved here is that the refusal reaches a caller as the
/// contract's <c>429</c>, carrying the quota, the counter, and a <c>Retry-After</c> — and that the operations which
/// must not be metered are not.
/// </summary>
[TestClass]
public sealed class RunnerApiQuotaEndToEndTests
{
    private const string Runner = "runner-a";
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Production = "production";
    private const string Version = "adopt-v3";
    private const string LeaseHeader = "X-Arazzo-Lease";
    private const string SequenceHeader = "X-Arazzo-Checkpoint-Seq";
    private const string ProblemType = "https://corvus-oss.org/arazzo/runner/problems/quota-exceeded";

    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task An_exhausted_claim_quota_answers_429_naming_the_quota_and_counter()
    {
        // The contract has declared this response since it was written. This is the first caller ever to receive one.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerClaims = new RunnerQuotaLimit(1, 1);
            o.TenantClaims = RunnerQuotaLimit.None;
        });

        (await host.ClaimAsync(Runner)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using HttpResponseMessage refused = await host.ClaimAsync(Runner);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        using Stj.JsonDocument body = Stj.JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().ShouldBe(ProblemType);
        body.RootElement.GetProperty("status").GetInt32().ShouldBe(429);
        body.RootElement.GetProperty("quota").GetString().ShouldBe("claim-rate/runner");
        body.RootElement.GetProperty("counter").GetString().ShouldBe(Runner);
    }

    [TestMethod]
    public async Task A_refusal_carries_a_retry_after_header()
    {
        // Without it the runner has nothing to hold for, and a bounded hold degenerates into a guess.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerClaims = new RunnerQuotaLimit(1, 1);
            o.TenantClaims = RunnerQuotaLimit.None;
        });

        await host.ClaimAsync(Runner);
        using HttpResponseMessage refused = await host.ClaimAsync(Runner);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.ShouldNotBeNull();
        refused.Headers.RetryAfter!.Delta.ShouldNotBeNull();
        refused.Headers.RetryAfter.Delta!.Value.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(1));
    }

    [TestMethod]
    public async Task The_checkpoint_quota_refuses_before_the_store_is_touched()
    {
        // Metering runs ahead of the lease check on purpose. A caller refused after it had already driven a store read
        // would be costing the deployment exactly what the quota exists to stop.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(1, 1);
            o.TenantCheckpoints = RunnerQuotaLimit.None;
        });

        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        (await host.LoadCheckpointAsync(Runner, Run1, lease)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Refused even though the lease is perfectly good, which is what shows the meter is reached first.
        using HttpResponseMessage refused = await host.LoadCheckpointAsync(Runner, Run1, lease);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using Stj.JsonDocument body = Stj.JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("quota").GetString().ShouldBe("checkpoint-rate/runner");
    }

    [TestMethod]
    public async Task An_exhausted_volume_quota_refuses_the_save_and_persists_nothing()
    {
        // The volume charge lands after the body is measured and before anything is written, so a refusal must leave the
        // stored sequence exactly where it was.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerCheckpointBytes = new RunnerQuotaLimit(64, 64);
            o.TenantCheckpointBytes = RunnerQuotaLimit.None;
        });

        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        byte[] checkpoint = Checkpoint(Run1, WorkflowRunStatus.Running, sequence: 2);
        checkpoint.Length.ShouldBeGreaterThan(64, "the test needs a body larger than the whole volume allowance");

        using HttpResponseMessage refused = await host.SaveCheckpointAsync(Runner, Run1, lease, checkpoint, 2);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using Stj.JsonDocument body = Stj.JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("quota").GetString().ShouldBe("checkpoint-bytes/runner");

        // Nothing was written: the run is still at the sequence the seed left it on.
        (await host.StoredSequenceAsync(Run1)).ShouldBe(1);
    }

    [TestMethod]
    public async Task Releasing_a_lease_is_never_metered()
    {
        // Refusing a release would strand a lease on a runner trying to hand work back, which makes an overload worse
        // rather than better. Every other dimension is set to refuse everything, and release still succeeds.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerCheckpoints = new RunnerQuotaLimit(0.001, 0.001);
            o.RunnerLeaseRenewals = new RunnerQuotaLimit(0.001, 0.001);
            o.RunnerCatalog = new RunnerQuotaLimit(0.001, 0.001);
        });

        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        (await host.ReleaseLeaseAsync(Runner, Run1, lease)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [TestMethod]
    public async Task Exhausting_one_dimension_leaves_the_others_alone()
    {
        // A runner that may not take new work must still be able to finish the run it holds, or the quota converts a
        // load problem into lost work.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerClaims = new RunnerQuotaLimit(1, 1);
            o.TenantClaims = RunnerQuotaLimit.None;
        });

        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        (await host.ClaimAsync(Runner)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        (await host.LoadCheckpointAsync(Runner, Run1, lease)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await host.RenewLeaseAsync(Runner, Run1, lease, 60)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task A_deployment_can_enforce_no_quotas_at_all()
    {
        // Opting out is something a deployment states rather than something it gets by omission, so the opt-out itself
        // is worth a test.
        await using Host host = await Host.StartAsync(configure: null, guard: NoRunnerQuotaGuard.Instance);
        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);

        for (int i = 0; i < 200; ++i)
        {
            (await host.ClaimAsync(Runner)).StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        }
    }

    [TestMethod]
    public async Task Reading_checkpoints_is_metered_by_volume_one_request_late()
    {
        // A read has no size until it has been read, so the volume cannot refuse the read that incurs it. What it must
        // do is CARRY the cost: the first read succeeds and takes the counter into deficit, and the next one is refused.
        // Without that the read path would be entirely unmetered while appearing configured.
        await using Host host = await Host.StartAsync(o =>
        {
            o.RunnerCheckpointBytes = new RunnerQuotaLimit(1, 8);
            o.TenantCheckpointBytes = RunnerQuotaLimit.None;
            o.RunnerCheckpoints = RunnerQuotaLimit.None;
        });

        await host.SeedAsync(Run1, WorkflowRunStatus.Pending);
        string lease = await host.ClaimLeaseAsync(Runner);

        // The seeded checkpoint is far larger than the 8-byte allowance, so this read overshoots it.
        using HttpResponseMessage first = await host.LoadCheckpointAsync(Runner, Run1, lease);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(8, "the read has to exceed the allowance for the deficit to be the thing under test");

        // The debt is carried, so the next read is refused by the volume quota rather than the request quota.
        using HttpResponseMessage second = await host.LoadCheckpointAsync(Runner, Run1, lease);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using Stj.JsonDocument body = Stj.JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("quota").GetString().ShouldBe("checkpoint-bytes/runner");
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
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;
    }

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;

        public static async Task<Host> StartAsync(Action<RunnerQuotaOptions>? configure = null, IRunnerQuotaGuard? guard = null)
        {
            var clock = new TestClock(T0);
            var store = new InMemoryWorkflowStateStore(clock);
            var bindings = new DeclaredRunnerEnvironmentBindings(new Dictionary<string, IReadOnlyList<string>>
            {
                [Runner] = [Production],
            });

            var quotaOptions = new RunnerQuotaOptions();
            configure?.Invoke(quotaOptions);

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

            app.MapArazzoRunnerApi(
                store,
                new InMemoryWorkflowCatalogStore(),
                new InMemoryAvailabilityStore(),
                bindings,
                requireAuthorization: false,
                timeProvider: clock,
                quotas: guard,
                quotaOptions: quotaOptions);

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

        public async ValueTask<long> StoredSequenceAsync(string runId)
        {
            WorkflowCheckpoint? stored = await store.LoadAsync(new WorkflowRunId(runId), default);
            stored.ShouldNotBeNull();
            WorkflowCheckpointSerializer.TryReadSequence(stored!.Value.Utf8, out long sequence).ShouldBeTrue();
            return sequence;
        }

        public Task<HttpResponseMessage> ClaimAsync(string principal)
            => this.SendAsync(
                new HttpRequestMessage(HttpMethod.Post, "/claims")
                {
                    Content = JsonContent.Create(new { hostedVersions = new[] { Version } }),
                },
                principal);

        public async Task<string> ClaimLeaseAsync(string principal)
        {
            using HttpResponseMessage response = await this.ClaimAsync(principal);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            using Stj.JsonDocument body = Stj.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return body.RootElement.GetProperty("lease").GetProperty("token").GetString()!;
        }

        public Task<HttpResponseMessage> LoadCheckpointAsync(string principal, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/runs/{runId}/checkpoint");
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> SaveCheckpointAsync(string principal, string runId, string lease, byte[] body, long sequence)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"/runs/{runId}/checkpoint")
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add(LeaseHeader, lease);
            request.Headers.Add(SequenceHeader, sequence.ToString(CultureInfo.InvariantCulture));
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> RenewLeaseAsync(string principal, string runId, string lease, int leaseSeconds)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/runs/{runId}/lease/renewal")
            {
                Content = JsonContent.Create(new { leaseSeconds }),
            };
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public Task<HttpResponseMessage> ReleaseLeaseAsync(string principal, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/runs/{runId}/lease");
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        public async ValueTask DisposeAsync()
        {
            this.Client.Dispose();
            await app.DisposeAsync();
        }

        private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string principal)
        {
            request.Headers.Add("X-Test-Principal", principal);
            return this.Client.SendAsync(request);
        }
    }
}