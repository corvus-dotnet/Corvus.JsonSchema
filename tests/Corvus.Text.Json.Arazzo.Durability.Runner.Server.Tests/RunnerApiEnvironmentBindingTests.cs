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
        (await host.SaveCheckpointAsync(Runner, Production, ProdRunId, lease, Checkpoint(ProdRunId, Production, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(lease)), 2)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
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

    [TestMethod]
    public async Task A_sealed_environment_takes_only_a_submission_sealed_under_an_active_generation()
    {
        // ADR 0065 decision 10: the runner API holds no key, so it cannot verify a MAC or open a payload; it requires
        // an encrypted, MAC'd submission under an active generation. A clear submission, one under a generation the
        // record does not hold as active, and a MAC'd one whose payload is still clear are each refused before the
        // store sees them. The genuine one is accepted, and the same posture never touches an environment that is not
        // sealed.
        await using Host host = await Host.StartAsync(boundEnvironments: [Development, Production], sealedGenerations: new Dictionary<string, IReadOnlySet<string>>
        {
            [Production] = new HashSet<string>(["k2"]),
        });
        await host.SeedAsync(ProdRunId, Production, WorkflowRunStatus.Running);
        await host.SeedAsync(DevRunId, Development, WorkflowRunStatus.Running);
        string prodLease = await host.PlantLeaseAsync(ProdRunId, Production);
        string devLease = await host.PlantLeaseAsync(DevRunId, Development);
        byte[] payloadKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var prodAddress = new WorkflowRunAddress(Production, new WorkflowRunId(ProdRunId));

        byte[] clear = Checkpoint(ProdRunId, Production, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(prodLease));
        (await host.SaveCheckpointAsync(Runner, Production, ProdRunId, prodLease, clear, 2)).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "clear");
        byte[] retired = SealingCheckpointStore.Seal(clear, prodAddress, Keys("k1", payloadKey));
        (await host.SaveCheckpointAsync(Runner, Production, ProdRunId, prodLease, retired, 2)).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a generation the record does not hold as active");
        byte[] macdClear = CheckpointIntegrity.Seal(clear, "k2", Keys("k2", payloadKey).EnvelopeMac);
        (await host.SaveCheckpointAsync(Runner, Production, ProdRunId, prodLease, macdClear, 2)).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a MAC over a clear payload is still a clear write");
        byte[] sealedRow = SealingCheckpointStore.Seal(clear, prodAddress, Keys("k2", payloadKey));
        (await host.SaveCheckpointAsync(Runner, Production, ProdRunId, prodLease, sealedRow, 2)).StatusCode.ShouldBe(HttpStatusCode.NoContent, "encrypted and sealed under an active generation");

        byte[] devClear = Checkpoint(DevRunId, Development, WorkflowRunStatus.Running, sequence: 2, epoch: LeaseEpoch(devLease));
        (await host.SaveCheckpointAsync(Runner, Development, DevRunId, devLease, devClear, 2)).StatusCode.ShouldBe(HttpStatusCode.NoContent, "development is not sealed");

        // The persisted row is the encrypted submission joined with the control-plane region, MAC intact.
        WorkflowCheckpoint? stored = await host.LoadStoredAsync(Production, ProdRunId);
        CheckpointRow.Parse(stored!.Value.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm);
        CheckpointIntegrity.KeyIdOf(stored.Value.Row.Span).ShouldBe("k2");
        CheckpointIntegrity.Verify(stored.Value.Row.Span, Keys("k2", payloadKey).EnvelopeMac).ShouldBeTrue();
        CheckpointRow.Parse(SealingCheckpointStore.Open(stored.Value.Row, prodAddress, Keys("k2", payloadKey))).Algorithm.ShouldBe(CheckpointAlgorithm.Clear, "the runner that holds the key opens it");
    }

    [TestMethod]
    public async Task A_message_is_claimed_by_channel_or_by_blind_index_and_a_sealed_environment_is_swept_by_index_only()
    {
        // ADR 0065 decision 4: a message claim names the message by its channel (an environment served clear) or by
        // its blind wait index (a sealed environment), exactly one of the two. A sealed environment's rows carry the
        // index in the channel column, and a channel claim never looks at them, so a runner without the key cannot
        // sweep a sealed environment's waits by naming channels.
        await using Host host = await Host.StartAsync(boundEnvironments: [Development, Production], sealedGenerations: new Dictionary<string, IReadOnlySet<string>>
        {
            [Production] = new HashSet<string>(["k2"]),
        });
        const string Index = "k2.29gDQKWEKLZynGM0-j3zWc80lR9N3Be7YeB6Q_ogdeI";
        await host.SeedWaitingAsync(ProdRunId, Production, WorkflowWait.BlindMessage(Index));
        await host.SeedWaitingAsync(DevRunId, Development, WorkflowWait.Message("kyc.verdict", "acct-42"));

        (await host.ClaimMessageAsync(Runner, """{"channel":"kyc.verdict","index":"k2.abc","hostedVersions":["adopt-v3"]}""")).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "both");
        (await host.ClaimMessageAsync(Runner, """{"hostedVersions":["adopt-v3"]}""")).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "neither");
        (await host.ClaimMessageAsync(Runner, """{"index":"not an index","hostedVersions":["adopt-v3"]}""")).StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an index outside its grammar");

        HttpResponseMessage byIndex = await host.ClaimMessageAsync(Runner, $$"""{"index":"{{Index}}","hostedVersions":["adopt-v3"]}""");
        byIndex.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await byIndex.Content.ReadAsStringAsync()).Contains(ProdRunId, StringComparison.Ordinal).ShouldBeTrue("the sealed run is claimed by its index");

        HttpResponseMessage byChannel = await host.ClaimMessageAsync(Runner, """{"channel":"kyc.verdict","correlationId":"acct-42","hostedVersions":["adopt-v3"]}""");
        byChannel.StatusCode.ShouldBe(HttpStatusCode.OK);
        string claimed = await byChannel.Content.ReadAsStringAsync();
        claimed.Contains(DevRunId, StringComparison.Ordinal).ShouldBeTrue("the clear run is claimed by its channel");
        claimed.Contains(ProdRunId, StringComparison.Ordinal).ShouldBeFalse();

        // A row in the sealed environment whose channel column holds a plaintext channel (a rewrite, or a row that
        // predates its key) is still never offered to a channel claim.
        const string ClearProdRunId = "00000000000000000000000000000c0c";
        await host.SeedWaitingAsync(ClearProdRunId, Production, WorkflowWait.Message("kyc.verdict", "acct-42"));
        (await (await host.ClaimMessageAsync(Runner, """{"channel":"kyc.verdict","correlationId":"acct-42","hostedVersions":["adopt-v3"]}""")).Content.ReadAsStringAsync()).Contains(ClearProdRunId, StringComparison.Ordinal).ShouldBeFalse("a sealed environment is never swept by channel");
    }

    // The keys a runner holds for production under one generation (ADR 0065 decision 5).
    private static RunnerEnvironmentKeys Keys(string keyId, byte[] payloadKey)
    {
        byte[] envelopeMac = new byte[32];
        Corvus.Text.Json.Arazzo.Durability.Anchoring.CheckpointDerivation.DeriveSubkey(payloadKey, Corvus.Text.Json.Arazzo.Durability.Anchoring.CheckpointSubkey.EnvelopeMac, Production, keyId, envelopeMac);
        return new RunnerEnvironmentKeys(keyId, payloadKey, envelopeMac, Sealed: true);
    }

    // The epoch the lease was granted with: the runner writes it into its region, and the API checks it against the
    // grant (ADR 0065 decision 6).
    private static long LeaseEpoch(string lease)
        => RunnerLeaseToken.TryParse(lease, out long epoch, out _) ? epoch : 0;

    private static byte[] Checkpoint(string runId, string environment, WorkflowRunStatus status, long sequence, long? epoch = null, WorkflowWait? wait = null)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                new WorkflowRunId(runId),
                environment,
                Version,
                status,
                0,
                sequence,
                Epoch: epoch,
                T0,
                T0,
                null,
                null,
                default,
                default,
                [],
                false,
                wait,
                null),
            retryCounters,
            new Dictionary<string, byte[]>(),
            default,
            stepOutputs,
            default,
            []);
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Host(WebApplication app, HttpClient client, InMemoryWorkflowStateStore store) : IAsyncDisposable
    {
        public static async Task<Host> StartAsync(IReadOnlyList<string> boundEnvironments, IReadOnlyDictionary<string, IReadOnlySet<string>>? sealedGenerations = null)
        {
            var clock = new TestClock(T0);
            var store = new InMemoryWorkflowStateStore(clock);
            var bindings = new DeclaredRunnerEnvironmentBindings(
                new Dictionary<string, IReadOnlyList<string>>
                {
                    [Runner] = boundEnvironments,
                },
                sealedGenerations: sealedGenerations);

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

        // Seeds, or re-seeds over whatever is there, a run suspended on a wait.
        public async ValueTask SeedWaitingAsync(string runId, string environment, WorkflowWait wait)
        {
            var address = new WorkflowRunAddress(environment, new WorkflowRunId(runId));
            byte[] checkpoint = Checkpoint(runId, environment, WorkflowRunStatus.Suspended, sequence: 1, wait: wait);
            WorkflowCheckpoint? current = await store.LoadAsync(address, default);
            await store.SaveAsync(address, checkpoint, WorkflowCheckpointSerializer.ProjectIndex(checkpoint), current?.Etag ?? WorkflowEtag.None, default);
        }

        public Task<HttpResponseMessage> ClaimMessageAsync(string principal, string body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/messageClaims")
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
            };
            return this.SendAsync(request, principal);
        }

        public async ValueTask<string> PlantLeaseAsync(string runId, string environment)
        {
            // The wire header is the server-minted composite (epoch + store token), exactly what a claim
            // response would carry; the store token alone is not a presentable lease.
            WorkflowLease? lease = await store.AcquireLeaseAsync(new WorkflowRunAddress(environment, new WorkflowRunId(runId)), Runner, TimeSpan.FromMinutes(5), default);
            return RunnerLeaseToken.Issue(lease!.Value.Epoch, lease.Value.Token);
        }

        public ValueTask<WorkflowCheckpoint?> LoadStoredAsync(string environment, string runId)
            => store.LoadAsync(new WorkflowRunAddress(environment, new WorkflowRunId(runId)), default);

        public Task<HttpResponseMessage> LoadCheckpointAsync(string principal, string environment, string runId, string lease)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, CheckpointRoute(environment, runId));
            request.Headers.Add(LeaseHeader, lease);
            return this.SendAsync(request, principal);
        }

        // A test posts the rows its fixtures build; the surface takes a submission (ADR 0065 decision 7), so a row is
        // sliced to its submitted bytes here and anything else goes as it is.
        private static byte[] Submitted(byte[] body)
            => CheckpointRow.TryParse(body, out CheckpointRowLayout layout) ? body[..layout.SubmittedLength] : body;

        public Task<HttpResponseMessage> SaveCheckpointAsync(string principal, string environment, string runId, string lease, byte[] body, long sequence)
        {
            body = Submitted(body);

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