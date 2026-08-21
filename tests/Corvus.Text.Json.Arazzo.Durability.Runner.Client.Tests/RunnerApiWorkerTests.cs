// <copyright file="RunnerApiWorkerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// Resuming waiting runs over the runner API (ADR 0065): a due timer, and a delivered message. The store is present
/// only so the API has something real to terminate into. The worker never sees it, and never sees the wait index.
/// </summary>
[TestClass]
public sealed class RunnerApiWorkerTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Run2 = "fedcba9876543210fedcba9876543210";
    private const string Run3 = "00112233445566778899aabbccddeeff";
    private const string Version = Fixture.Version;
    private const string Channel = "orders.approved";

    [TestMethod]
    public async Task A_run_whose_timer_is_due_is_resumed()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0 + TimeSpan.FromMinutes(5)));
        var worker = new RunnerApiWorker(fixture.Client);

        // Not yet due. The server answers against its own clock, so this is the clock that matters.
        (await worker.ResumeDueTimersAsync([Version], Advance, default)).ShouldBe(0);

        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        (await worker.ResumeDueTimersAsync([Version], Advance, default)).ShouldBe(1);
    }

    [TestMethod]
    public async Task A_runner_cannot_ask_for_timers_that_have_not_fired()
    {
        // There is no cutoff parameter to pass, which is the point: a runner with a fast clock, or one asking for
        // future work, gets the same answer as one with a correct clock.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0 + TimeSpan.FromHours(1)));
        var worker = new RunnerApiWorker(fixture.Client);

        (await worker.ResumeDueTimersAsync([Version], Advance, default)).ShouldBe(0);

        // Still suspended and still waiting, rather than advanced early.
        WorkflowCheckpoint? stored = await fixture.Store.LoadAsync(new WorkflowRunAddress(Fixture.Production, new WorkflowRunId(Run1)), default);
        WorkflowCheckpointSerializer.ProjectIndex(stored!.Value.Utf8).Status.ShouldBe(WorkflowRunStatus.Suspended);
    }

    [TestMethod]
    public async Task A_due_run_for_a_version_this_runner_does_not_host_is_not_offered()
    {
        // The store-backed worker had no such filter and would hand a runner a due run it could only fault.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0), workflowId: "something-else-v1");
        var worker = new RunnerApiWorker(fixture.Client);

        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        (await worker.ResumeDueTimersAsync([Version], (_, _) => throw new InvalidOperationException("this runner cannot execute that version"), default)).ShouldBe(0);
    }

    [TestMethod]
    public async Task A_message_resumes_every_run_awaiting_it()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Message(Channel, null));
        await fixture.SeedWaitingAsync(Run2, WorkflowWait.Message(Channel, null));
        var worker = new RunnerApiWorker(fixture.Client);
        using ParsedJsonDocument<JsonElement> payload = NewPayload();

        int resumed = await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Version], Advance, default);

        resumed.ShouldBe(2);
    }

    [TestMethod]
    public async Task Correlation_matches_when_it_is_equal_or_absent_on_either_side()
    {
        // Absence is a wildcard on either side, which is what the store does and what the conformance suite pins
        // across every backend. A message carrying no correlation reaches a run waiting for a particular one, and a
        // run waiting for none is reached by any message. Only two present-and-different correlations fail to match.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Message(Channel, "order-42"));
        var worker = new RunnerApiWorker(fixture.Client);
        using ParsedJsonDocument<JsonElement> payload = NewPayload();

        (await worker.DeliverMessageAsync(Channel, "order-99", payload.RootElement, [Version], Advance, default)).ShouldBe(0);
        (await worker.DeliverMessageAsync(Channel, "order-42", payload.RootElement, [Version], Advance, default)).ShouldBe(1);
        (await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Version], Advance, default)).ShouldBe(1);

        // A run awaiting no particular correlation is reached by a correlated message. run-1 is not, because two
        // present correlations that differ is the one combination that does not match.
        await fixture.SeedWaitingAsync(Run2, WorkflowWait.Message(Channel, null));
        (await worker.DeliverMessageAsync(Channel, "anything", payload.RootElement, [Version], Advance, default)).ShouldBe(1);
    }

    [TestMethod]
    public async Task The_delivered_message_reaches_the_run()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Message(Channel, null));
        var worker = new RunnerApiWorker(fixture.Client);
        using ParsedJsonDocument<JsonElement> payload = NewPayload();

        int seen = 0;
        await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Version], (run, _) =>
        {
            run.TryTakeDeliveredMessage(out JsonElement delivered).ShouldBeTrue();
            seen = delivered.GetProperty("orderId"u8).GetInt32();
            return ValueTask.FromResult(WorkflowRunResultKind.Suspended);
        }, default);

        seen.ShouldBe(42);
    }

    [TestMethod]
    public async Task A_sweep_leases_what_it_claims_and_gives_it_back()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Message(Channel, null));
        var worker = new RunnerApiWorker(fixture.Client);
        using ParsedJsonDocument<JsonElement> payload = NewPayload();

        // Held while the run is advancing, so a peer sweeping the same channel gets nothing.
        await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Version], async (_, _) =>
        {
            var peer = new RunnerApiWorker(fixture.PeerClient);
            (await peer.DeliverMessageAsync(Channel, null, default, [Version], Advance, default)).ShouldBe(0);
            return WorkflowRunResultKind.Suspended;
        }, default);

        // And handed back afterwards, with no clock advance: released rather than waited out.
        (await fixture.PeerClient.ClaimAwaitingMessageAsync(Channel, null, [Version])).Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_sweep_claims_no_more_than_it_is_allowed()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedWaitingAsync(Run1, WorkflowWait.Message(Channel, null));
        await fixture.SeedWaitingAsync(Run2, WorkflowWait.Message(Channel, null));
        await fixture.SeedWaitingAsync(Run3, WorkflowWait.Message(Channel, null));
        var worker = new RunnerApiWorker(fixture.Client) { MaximumRunsPerSweep = 2 };
        using ParsedJsonDocument<JsonElement> payload = NewPayload();

        (await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Version], (run, ct) => CompleteAsync(fixture, run, ct), default)).ShouldBe(2);

        // Exactly the one it was not allowed to take is still awaiting: not stranded mid-claim, and not swept anyway.
        (await fixture.PeerClient.ClaimAwaitingMessageAsync(Channel, null, [Version])).Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_run_that_is_no_longer_waiting_is_not_resumed()
    {
        // The wait index says a message matched, but the run may have been advanced since. Re-entering it would take
        // it back to a step it has already left, so a run that is not Suspended is refused under the lease.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Completed);
        var worker = new RunnerApiWorker(fixture.Client);

        (await worker.DeliverMessageAsync(Channel, null, default, [Version], (_, _) => throw new InvalidOperationException("a completed run must never be resumed"), default)).ShouldBe(0);
    }

    [TestMethod]
    public async Task Nothing_waiting_resumes_nothing()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        var worker = new RunnerApiWorker(fixture.Client);

        (await worker.ResumeDueTimersAsync([Version], Advance, default)).ShouldBe(0);
        (await worker.DeliverMessageAsync(Channel, null, default, [Version], Advance, default)).ShouldBe(0);
    }

    // The document owns the payload's memory, so it outlives every delivery that hands out its root element. A
    // helper returning a bare element would hand back a view of a document already disposed.
    private static ParsedJsonDocument<JsonElement> NewPayload()
        => ParsedJsonDocument<JsonElement>.Parse("""{"orderId":42}"""u8.ToArray());

    private static ValueTask<WorkflowRunResultKind> Advance(WorkflowRun run, CancellationToken cancellationToken)
        => ValueTask.FromResult(WorkflowRunResultKind.Suspended);

    // What a real executor does on the way out: checkpoint, which clears the wait and takes the run out of the
    // awaiting set. A resumer that wrote nothing would leave every run still waiting, so a test using it would be
    // measuring the degenerate path.
    private static async ValueTask<WorkflowRunResultKind> CompleteAsync(Fixture fixture, WorkflowRun run, CancellationToken cancellationToken)
    {
        byte[] advanced = Fixture.Checkpoint(run.Id.Value, WorkflowRunStatus.Completed, sequence: 2);
        await fixture.Client.Checkpoints.SaveAsync(run.Address, advanced, WorkflowCheckpointSerializer.ProjectIndex(advanced), WorkflowEtag.None, cancellationToken);
        return WorkflowRunResultKind.Completed;
    }
}