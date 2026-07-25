// <copyright file="HttpWorkflowStateStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Tests;

/// <summary>
/// Proves the function-side <see cref="HttpWorkflowStateStore"/> proxies checkpoints to the runner over HTTP with
/// fire-and-forget saves stamped with a per-run monotonic write-sequence, and that <c>FlushAsync</c> is the terminal
/// barrier: it awaits the pending writes and fails only when the last (terminal) one did not commit. Responses key
/// on the write-sequence header, not arrival order, since fire-and-forget POSTs race by design.
/// </summary>
[TestClass]
public class HttpWorkflowStateStoreTests
{
    private static readonly WorkflowRunIndexEntry AnyIndex = new("wf", WorkflowRunStatus.Running, default, default);
    private static readonly ReadOnlyMemory<byte> Bytes = new byte[] { 1, 2, 3 };

    [TestMethod]
    public async Task Load_gets_the_checkpoint_and_seeds_the_write_sequence()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9, 9]) };
            response.Headers.ETag = new EntityTagHeaderValue("\"e1\"");
            response.Headers.Add(HttpWorkflowStateStore.WriteSequenceHeader, "7");
            return Task.FromResult(response);
        });
        var store = new HttpWorkflowStateStore(Client(handler));

        WorkflowCheckpoint? loaded = await store.LoadAsync("run-1", default);

        loaded.ShouldNotBeNull();
        loaded!.Value.Utf8.ToArray().ShouldBe([9, 9]);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].Path.ShouldBe("/runs/run-1/checkpoint");

        // The next save continues the monotonic sequence from the loaded 7.
        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);
        await store.FlushAsync(default);
        (HttpMethod Method, string Path, string? Seq) post = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        post.Path.ShouldBe("/runs/run-1/checkpoint");
        post.Seq.ShouldBe("8");
    }

    [TestMethod]
    public async Task Load_of_an_unknown_run_is_null()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        var store = new HttpWorkflowStateStore(Client(handler));

        (await store.LoadAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Save_is_fire_and_forget_and_flush_awaits_the_pending_write()
    {
        var gate = new TaskCompletionSource();
        var handler = new StubHandler(async _ =>
        {
            await gate.Task;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var store = new HttpWorkflowStateStore(Client(handler));

        // The save returns synchronously though the POST has not completed (it is gated).
        ValueTask<WorkflowEtag> save = store.SaveAsync("run-1", Bytes, AnyIndex, default, default);
        save.IsCompleted.ShouldBeTrue();

        // Flush blocks on the in-flight POST until the gate releases it.
        ValueTask flush = store.FlushAsync(default);
        flush.IsCompleted.ShouldBeFalse();
        gate.SetResult();
        await flush;
    }

    [TestMethod]
    public async Task The_write_sequence_increments_per_save()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var store = new HttpWorkflowStateStore(Client(handler));

        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);
        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);
        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);
        await store.FlushAsync(default);

        // The three saves are stamped 1/2/3; they race on the wire, so assert the set, not arrival order.
        handler.Requests.Select(r => r.Seq).ShouldBe(["1", "2", "3"], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Flush_throws_when_the_terminal_checkpoint_did_not_commit()
    {
        var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var store = new HttpWorkflowStateStore(Client(handler));

        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);

        // A 5xx on the terminal checkpoint means the run's final state is not durable, so Flush fails and the run
        // stays claimable for re-invocation.
        await Should.ThrowAsync<InvalidOperationException>(async () => await store.FlushAsync(default));
    }

    [TestMethod]
    public async Task An_interim_failure_is_tolerated_when_the_terminal_commits()
    {
        // The interim checkpoint (seq 1) fails; the terminal (seq 2) succeeds. Keyed on the seq, not arrival, so the
        // outcome is deterministic despite the race. The terminal is durable and supersedes the lost interim, so
        // Flush does not fail — the lost interim is a safe idempotent replay at worst.
        var handler = new StubHandler(request =>
        {
            string? seq = SeqOf(request);
            return Task.FromResult(new HttpResponseMessage(seq == "1" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK));
        });
        var store = new HttpWorkflowStateStore(Client(handler));

        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);
        await store.SaveAsync("run-1", Bytes, AnyIndex, default, default);

        await Should.NotThrowAsync(async () => await store.FlushAsync(default));
    }

    [TestMethod]
    public void Rejects_a_null_client()
    {
        Should.Throw<ArgumentNullException>(() => new HttpWorkflowStateStore(null!));
    }

    private static HttpClient Client(StubHandler handler) => new(handler) { BaseAddress = new Uri("http://runner.local/") };

    private static string? SeqOf(HttpRequestMessage request)
        => request.Headers.TryGetValues(HttpWorkflowStateStore.WriteSequenceHeader, out IEnumerable<string>? values)
            ? values.FirstOrDefault()
            : null;

    // Records each request (method, path, write-seq header) and answers via a callback that keys on the request
    // itself (its write-seq), so a test's outcome is deterministic even though fire-and-forget POSTs race.
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? Seq)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (this.Requests)
            {
                this.Requests.Add((request.Method, request.RequestUri!.AbsolutePath, SeqOf(request)));
            }

            return respond(request);
        }
    }
}