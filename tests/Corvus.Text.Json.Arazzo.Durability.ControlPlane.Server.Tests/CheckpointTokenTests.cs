// <copyright file="CheckpointTokenTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of the run-scoped checkpoint bearer token (ADR 0062): the token's own issue/validate semantics (run binding,
/// expiry, tamper resistance) and the checkpoint endpoint's optional token authenticator — a request with no token, or a
/// token for another run, is a 401, and a valid token is admitted to the load/save logic.
/// </summary>
[TestClass]
public sealed class CheckpointTokenTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Run2 = "fedcba9876543210fedcba9876543210";
    private static readonly byte[] Secret = Encoding.UTF8.GetBytes("a-shared-checkpoint-secret-of-sufficient-length");
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_fresh_token_validates_for_its_run()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));

        CheckpointToken.TryValidate(Secret, token, Run1, Now).ShouldBeTrue();
    }

    [TestMethod]
    public void A_token_does_not_validate_for_another_run()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));

        CheckpointToken.TryValidate(Secret, token, Run2, Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_token_does_not_validate_under_another_secret()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));

        CheckpointToken.TryValidate(Encoding.UTF8.GetBytes("a-completely-different-secret-value"), token, Run1, Now).ShouldBeFalse();
    }

    [TestMethod]
    public void An_expired_token_does_not_validate()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(-1));

        CheckpointToken.TryValidate(Secret, token, Run1, Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_tampered_token_does_not_validate()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));
        string tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        CheckpointToken.TryValidate(Secret, tampered, Run1, Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_malformed_or_missing_token_does_not_validate()
    {
        CheckpointToken.TryValidate(Secret, null, Run1, Now).ShouldBeFalse();
        CheckpointToken.TryValidate(Secret, string.Empty, Run1, Now).ShouldBeFalse();
        CheckpointToken.TryValidate(Secret, "no-separator", Run1, Now).ShouldBeFalse();
        CheckpointToken.TryValidate(Secret, "not-a-number.signature", Run1, Now).ShouldBeFalse();
    }

    [TestMethod]
    public void Issue_rejects_a_secret_shorter_than_the_minimum()
    {
        Should.Throw<ArgumentException>(() => CheckpointToken.Issue(Encoding.UTF8.GetBytes("too-short-secret"), Run1, Now.AddMinutes(10)));
    }

    [TestMethod]
    public void Validation_fails_under_a_secret_shorter_than_the_minimum()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));

        CheckpointToken.TryValidate(Encoding.UTF8.GetBytes("too-short-secret"), token, Run1, Now).ShouldBeFalse();
    }

    [TestMethod]
    public void A_non_canonical_expiry_does_not_validate()
    {
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));
        int separator = token.IndexOf('.');
        string expiry = token[..separator];
        string signature = token[(separator + 1)..];

        // A signed, or zero-padded, expiry carries the same signature but is not the one canonical token for the run.
        CheckpointToken.TryValidate(Secret, $"+{expiry}.{signature}", Run1, Now).ShouldBeFalse();
        CheckpointToken.TryValidate(Secret, $"0{expiry}.{signature}", Run1, Now).ShouldBeFalse();

        // Sanity: the canonical token still validates.
        CheckpointToken.TryValidate(Secret, token, Run1, Now).ShouldBeTrue();
    }

    [TestMethod]
    public void A_run_spelled_with_the_separator_cannot_borrow_anothers_token()
    {
        // The signed message is unframed, so this is the collision to rule out: a run id containing the separator must
        // not be able to produce the same signed message as a different pair of run id and expiry. It cannot, because
        // the expiry is a digits-only suffix after the final colon — asserted here rather than left as an assumption.
        long expiry = Now.AddMinutes(10).ToUnixTimeSeconds();
        string token = CheckpointToken.Issue(Secret, $"run-1:{expiry}", Now.AddMinutes(10));

        CheckpointToken.TryValidate(Secret, token, Run1, Now).ShouldBeFalse();
        CheckpointToken.TryValidate(Secret, token, $"run-1:{expiry}", Now).ShouldBeTrue();
    }

    [TestMethod]
    public void Validating_a_token_allocates_nothing()
    {
        // This runs on every checkpoint callback from a deployed function, before the caller has proved anything, so
        // its cost is what someone presenting a wrong token can spend on the runner's behalf. The first shape allocated
        // 576 bytes a call, transcoding both sides of the comparison and materialising the expiry twice.
        string token = CheckpointToken.Issue(Secret, Run1, Now.AddMinutes(10));

        // Warm up, so the measurement covers the work rather than the JIT.
        for (int i = 0; i < 1_000; ++i)
        {
            CheckpointToken.TryValidate(Secret, token, Run1, Now);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        const int Iterations = 10_000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool accepted = true;
        for (int i = 0; i < Iterations; ++i)
        {
            accepted &= CheckpointToken.TryValidate(Secret, token, Run1, Now);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        accepted.ShouldBeTrue("the measured calls must be doing the real work, not failing early");
        allocated.ShouldBe(0, $"validation must allocate nothing; it allocated {allocated} bytes over {Iterations} calls");
    }

    [TestMethod]
    public async Task The_endpoint_rejects_a_checkpoint_request_with_no_token()
    {
        await using TokenHost host = await TokenHost.StartAsync();

        HttpResponseMessage response = await host.GetCheckpointAsync(Run1, token: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task The_endpoint_rejects_a_token_minted_for_another_run()
    {
        await using TokenHost host = await TokenHost.StartAsync();
        string tokenForRun2 = CheckpointToken.Issue(Secret, Run2, DateTimeOffset.UtcNow.AddMinutes(10));

        HttpResponseMessage response = await host.GetCheckpointAsync(Run1, tokenForRun2);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task The_endpoint_admits_a_valid_token_and_proceeds_to_the_load()
    {
        await using TokenHost host = await TokenHost.StartAsync();
        string token = CheckpointToken.Issue(Secret, Run1, DateTimeOffset.UtcNow.AddMinutes(10));

        HttpResponseMessage response = await host.GetCheckpointAsync(Run1, token);

        // Authenticated, so it proceeds to the load — which is a 404 for an unknown run, not a 401.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task The_endpoint_rejects_a_checkpoint_save_with_no_token()
    {
        await using TokenHost host = await TokenHost.StartAsync();

        HttpResponseMessage response = await host.PostCheckpointAsync(Run1, [1, 2, 3], sequence: 1, token: null);

        // The save route gates on the token before it reads the sequence header or the body.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed class TokenHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public static async Task<TokenHost> StartAsync()
        {
            var store = new InMemoryWorkflowStateStore();
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            WebApplication app = builder.Build();
            app.MapWorkflowCheckpointEndpoints(
                store,
                requireAuthorization: false,
                authenticateCheckpointToken: (id, token) => CheckpointToken.TryValidate(Secret, token, id.Value, DateTimeOffset.UtcNow));
            await app.StartAsync();

            return new TokenHost(app, app.GetTestClient());
        }

        public Task<HttpResponseMessage> GetCheckpointAsync(string runId, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/runs/{runId}/checkpoint");
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PostCheckpointAsync(string runId, byte[] body, long sequence, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/runs/{runId}/checkpoint")
            {
                Content = new ByteArrayContent(body) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add("X-Arazzo-Checkpoint-Seq", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }
}