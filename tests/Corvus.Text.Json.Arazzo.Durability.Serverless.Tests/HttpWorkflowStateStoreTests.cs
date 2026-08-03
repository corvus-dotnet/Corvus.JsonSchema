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

    // The sequence travels inside the checkpoint, so a payload under test has to be a document that carries one.
    private static ReadOnlyMemory<byte> At(long sequence)
        => System.Text.Encoding.UTF8.GetBytes("""{"runId":"run-1","cursor":0,"sequence":""" + sequence + "}");

    [TestMethod]
    public async Task Load_returns_the_checkpoint_and_leaves_the_sequence_to_the_document()
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

        // The store keeps no counter of its own: the sequence it sends is the one the run authored into the document,
        // so a header the load happened to carry cannot pull the two out of step.
        await store.SaveAsync("run-1", At(8), AnyIndex, default, default);
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
    public async Task Saves_for_one_run_are_dispatched_one_at_a_time_and_in_order()
    {
        // ADR 0065 decision 6: at most one save per run is in flight. They used to race on the wire, which is what let
        // two writes for the same run reach the store out of order; the assertion is now on arrival order, not on the
        // set, because the ordering is the property under test.
        var inFlight = 0;
        var concurrent = false;
        var handler = new StubHandler(async _ =>
        {
            if (Interlocked.Increment(ref inFlight) > 1)
            {
                concurrent = true;
            }

            await Task.Yield();
            Interlocked.Decrement(ref inFlight);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var store = new HttpWorkflowStateStore(Client(handler));

        await store.SaveAsync("run-1", At(1), AnyIndex, default, default);
        await store.SaveAsync("run-1", At(2), AnyIndex, default, default);
        await store.SaveAsync("run-1", At(3), AnyIndex, default, default);
        await store.FlushAsync(default);

        concurrent.ShouldBeFalse();
        handler.Requests.Select(r => r.Seq).ShouldBe(["1", "2", "3"]);
    }

    [TestMethod]
    public async Task Saves_for_different_runs_are_not_serialised_behind_each_other()
    {
        // The interlock is per run. A single chain would make one slow run's checkpoint latency everyone else's.
        var release = new TaskCompletionSource();
        var handler = new StubHandler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("slow-run", StringComparison.Ordinal))
            {
                await release.Task;
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var store = new HttpWorkflowStateStore(Client(handler));

        await store.SaveAsync("slow-run", At(1), AnyIndex, default, default);
        await store.SaveAsync("quick-run", At(1), AnyIndex, default, default);

        // The quick run's save completes while the slow run's is still held.
        await Task.Delay(50);
        handler.Requests.Any(r => r.Path.Contains("quick-run", StringComparison.Ordinal)).ShouldBeTrue();

        release.SetResult();
        await store.FlushAsync(default);
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
    public async Task A_send_lost_in_transport_is_resent_byte_identically()
    {
        // ADR 0065 decision 6: a retry is a byte-identical resend of the same sequence, not a re-authoring. Skipping to
        // the next checkpoint would leave a hole the store will not accept, and because the sequence lives inside the
        // document the runner could not close that hole by renumbering a header.
        var attempts = 0;
        var handler = new StubHandler(_ =>
        {
            attempts++;
            return attempts == 1
                ? throw new HttpRequestException("connection reset")
                : Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        });
        var store = new HttpWorkflowStateStore(Client(handler));

        await store.SaveAsync("run-1", At(4), AnyIndex, default, default);
        await Should.NotThrowAsync(async () => await store.FlushAsync(default));

        attempts.ShouldBe(2);
        handler.Requests.Select(r => r.Seq).ShouldAllBe(seq => seq == "4");
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