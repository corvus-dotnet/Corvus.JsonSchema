// <copyright file="SealedCheckpointSurfaceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The checkpoint listener of ADR 0065 decision 11: a host that terminates a function's plaintext checkpoint holds
/// the environment payload key, so its surface runs over a <see cref="SealingCheckpointStore"/>. What the function
/// posts is clear and what it loads is clear; what the store behind the listener holds is encrypted and MAC'd, and
/// the function never sees a key.
/// </summary>
[TestClass]
public sealed class SealedCheckpointSurfaceTests
{
    private const string Env = "production";
    private const string RunId = "0123456789abcdef0123456789abcdef";
    private static readonly WorkflowRunAddress Address = new(Env, new WorkflowRunId(RunId));
    private static readonly byte[] Secret = RandomNumberGenerator.GetBytes(CheckpointToken.MinimumSecretBytes);
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 11)).ToArray();

    [TestMethod]
    public async Task The_listener_encrypts_what_the_function_posts_and_opens_what_it_loads()
    {
        var inner = new InMemoryWorkflowStateStore();
        await using ListenerHost host = await ListenerHost.StartAsync(inner, Ring());
        string token = CheckpointToken.Issue(Secret, Address, DateTimeOffset.UtcNow.AddMinutes(10));
        byte[] clear = Checkpoint(sequence: 1, inputs: "{\"petId\":7}");

        (await host.PostCheckpointAsync(clear, sequence: 1, token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        WorkflowCheckpoint stored = (await inner.LoadAsync(Address, default))!.Value;
        CheckpointRow.Parse(stored.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm, "the store holds ciphertext");
        CheckpointIntegrity.KeyIdOf(stored.Row.Span).ShouldBe("k1");
        System.Text.Encoding.Latin1.GetString(stored.Row.Span).ShouldNotContain("petId");

        HttpResponseMessage loaded = await host.GetCheckpointAsync(token);
        loaded.StatusCode.ShouldBe(HttpStatusCode.OK);
        byte[] served = await loaded.Content.ReadAsByteArrayAsync();
        CheckpointRow.Parse(served).Algorithm.ShouldBe(CheckpointAlgorithm.Clear, "the function gets plaintext");
        CheckpointRow.SubmittedBytes(served).ToArray().ShouldBe(CheckpointRow.SubmittedBytes(clear).ToArray(), "byte for byte what it posted");

        // The next save is conditioned on the stored row and encrypted afresh: two ciphertexts for two saves.
        (await host.PostCheckpointAsync(Checkpoint(sequence: 2, inputs: "{\"petId\":7}"), sequence: 2, token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        WorkflowCheckpoint next = (await inner.LoadAsync(Address, default))!.Value;
        next.Row.Span[CheckpointRow.Parse(next.Row.Span).Salt].ToArray().ShouldNotBe(stored.Row.Span[CheckpointRow.Parse(stored.Row.Span).Salt].ToArray());
    }

    [TestMethod]
    public async Task A_row_rewritten_beneath_the_listener_is_not_served()
    {
        // The store, or whoever holds its credential, replaces the row with a clear one: the listener refuses to serve
        // it rather than hand the function a row nobody sealed.
        var inner = new InMemoryWorkflowStateStore();
        await using ListenerHost host = await ListenerHost.StartAsync(inner, Ring());
        string token = CheckpointToken.Issue(Secret, Address, DateTimeOffset.UtcNow.AddMinutes(10));
        (await host.PostCheckpointAsync(Checkpoint(sequence: 1), sequence: 1, token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        WorkflowCheckpoint stored = (await inner.LoadAsync(Address, default))!.Value;
        byte[] rewritten = Checkpoint(sequence: 1, epoch: 4);
        await inner.SaveAsync(Address, rewritten, WorkflowCheckpointSerializer.ProjectIndex(rewritten), stored.Etag, default);

        await Should.ThrowAsync<CryptographicException>(async () => await host.GetCheckpointAsync(token));
    }

    private static RunnerKeyRing Ring()
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Env, "k1", envelopeMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys> { [Env] = new("k1", PayloadKey, envelopeMac, Sealed: true) });
    }

    private static byte[] Checkpoint(long sequence, long? epoch = null, string? inputs = null)
    {
        using PooledUtf8Map<int> retryCounters = PooledUtf8Map<int>.Rent(0);
        using PooledUtf8Map<JsonElement> stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        using ParsedJsonDocument<JsonElement>? inputsDocument = inputs is null ? null : ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(inputs));
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                Address.RunId,
                Env,
                "petWorkflow",
                WorkflowRunStatus.Running,
                0,
                sequence,
                Epoch: epoch,
                new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 4, 5, 10, 0, TimeSpan.Zero),
                null,
                null,
                default,
                default,
                [],
                false,
                null,
                null),
            retryCounters,
            new Dictionary<string, byte[]>(),
            inputsDocument?.RootElement ?? default,
            stepOutputs,
            default,
            []);
    }

    private sealed class ListenerHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public static async Task<ListenerHost> StartAsync(IWorkflowCheckpointStore inner, RunnerKeyRing ring)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();

            WebApplication app = builder.Build();
            app.MapWorkflowCheckpointEndpoints(
                new SealingCheckpointStore(inner, ring),
                requireAuthorization: false,
                authenticateCheckpointToken: (address, token) => CheckpointToken.TryValidate(Secret, token, address, DateTimeOffset.UtcNow));
            await app.StartAsync();

            return new ListenerHost(app, app.GetTestClient());
        }

        public Task<HttpResponseMessage> GetCheckpointAsync(string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"/environments/{Env}/runs/{RunId}/checkpoint");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PostCheckpointAsync(byte[] row, long sequence, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/environments/{Env}/runs/{RunId}/checkpoint")
            {
                Content = new ByteArrayContent(CheckpointRow.SubmittedBytes(row).ToArray()) { Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") } },
            };
            request.Headers.Add("X-Arazzo-Checkpoint-Seq", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            client.Dispose();
            await app.DisposeAsync();
        }
    }
}