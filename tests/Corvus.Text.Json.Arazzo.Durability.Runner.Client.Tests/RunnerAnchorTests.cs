// <copyright file="RunnerAnchorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.OpenApi.HttpTransport;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The tenant anchor over the real runner API (ADR 0065 decision 6): a runner serving a sealed environment claims a
/// run, writes its grant into every row, stages every save with its own tenant store, and, when the control plane
/// rolls the run back, refuses to advance it, gives the lease back and carries on with the rest of its sweep.
/// </summary>
[TestClass]
public sealed class RunnerAnchorTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Run2 = "fedcba9876543210fedcba9876543210";
    private const string KeyId = "k2";
    private const string Channel = "orders.approved";
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 3)).ToArray();

    [TestMethod]
    public void A_runner_serving_a_sealed_environment_needs_a_tenant_anchor_store()
    {
        // The runner client is the lease holder, and the lease holder is a run's sole anchor writer. A sealed ring
        // with nowhere to anchor would leave the environment's freshness to the control plane.
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        var transport = new HttpClientTransport(http);

        Should.Throw<InvalidOperationException>(() => new ArazzoRunnerClient(transport, keyRing: Ring(sealedProduction: true))).Message.ShouldContain("anchor");
        new ArazzoRunnerClient(transport, keyRing: Ring(sealedProduction: false)).Checkpoints.ShouldNotBeNull("an environment held but not marked sealed may run unanchored");
        new ArazzoRunnerClient(transport, keyRing: Ring(sealedProduction: true), anchors: new InMemoryTenantAnchorStore()).Checkpoints.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_sealed_run_is_anchored_across_the_runner_api_and_a_rollback_by_the_control_plane_is_refused()
    {
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealedProduction: true),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });

        // Two runs at their genesis rows, as the control plane wrote them: clear, sequence 0, no grant, waiting on a
        // message. A message rather than a timer, because the run's own clock is the process's and the server's is
        // the fixture's, and a message is due whenever it is delivered.
        await fixture.SeedGenesisWaitingAsync(Run1, WorkflowWait.Message(Channel, null));
        await fixture.SeedGenesisWaitingAsync(Run2, WorkflowWait.Message(Channel, null));
        using ParsedJsonDocument<JsonElement> payload = ParsedJsonDocument<JsonElement>.Parse("""{"orderId":42}"""u8.ToArray());

        var worker = new RunnerApiWorker(fixture.Client);
        (await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Fixture.Version], Advance, default)).ShouldBe(2);

        // Every row the runner wrote carries its grant, sealed under the environment's key, and the tenant's record
        // committed to the last one before the run rested.
        WorkflowCheckpoint firstRest = (await fixture.Store.LoadAsync(Address(Run1), default))!.Value;
        CheckpointRow.Parse(firstRest.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm);
        WorkflowCheckpointSerializer.TryProject(firstRest.Row, out CheckpointProjection projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(2);
        projection.Epoch.ShouldNotBeNull();
        projection.Incarnation.ShouldBe(1UL);
        AnchorRecord record = (await anchors.ReadAsync(Fixture.Production, Run1, default))!.Value;
        record.State.ShouldBe(AnchorState.Live);
        record.Pending.ShouldBeNull();
        record.Committed.ShouldBe(new AnchorMark(new AnchorOrderingKey(1, (ulong)projection.Epoch!.Value), 2, CheckpointDigest.ForSubmitted(CheckpointRow.SubmittedBytes(firstRest.Row).Span)));

        // A second advance of both, so run 1 has a resting row behind the one the tenant committed to.
        (await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Fixture.Version], Advance, default)).ShouldBe(2);
        WorkflowCheckpoint secondRest = (await fixture.Store.LoadAsync(Address(Run1), default))!.Value;
        (await anchors.ReadAsync(Fixture.Production, Run1, default))!.Value.Committed.Sequence.ShouldBe(4UL);

        // The control plane rolls run 1 back to where it first rested: a genuine, claimable row, MAC intact, under the
        // runner's own grant. Nothing the row says about itself is false.
        await fixture.Store.SaveAsync(Address(Run1), firstRest.Row, WorkflowCheckpointSerializer.ProjectIndex(firstRest.Row), secondRest.Etag, default);

        // The sweep offers both runs. Run 1 is refused by the anchor and handed back; run 2 advances as usual.
        (await worker.DeliverMessageAsync(Channel, null, payload.RootElement, [Fixture.Version], Advance, default)).ShouldBe(1);
        (await anchors.ReadAsync(Fixture.Production, Run1, default))!.Value.Committed.Sequence.ShouldBe(4UL, "the anchor is not moved by a refusal");
        WorkflowCheckpointSerializer.TryProject((await fixture.Store.LoadAsync(Address(Run1), default))!.Value.Row, out projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(2, "the rolled-back row is left exactly as the control plane put it");
        (await fixture.Store.AcquireLeaseAsync(Address(Run1), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the refused run's lease was given back");
        WorkflowCheckpointSerializer.TryProject((await fixture.Store.LoadAsync(Address(Run2), default))!.Value.Row, out projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(6, "run 2 advanced three times");
        (await anchors.ReadAsync(Fixture.Production, Run2, default))!.Value.Committed.Sequence.ShouldBe(6UL);

        static async ValueTask<WorkflowRunResultKind> Advance(WorkflowRun run, CancellationToken cancellationToken)
        {
            // A real executor's shape: a checkpoint mid-advance, then the run rests on the next message.
            await run.CheckpointAsync(run.Cursor + 1, cancellationToken);
            await run.SuspendForMessageAsync(run.Cursor, Channel, null, cancellationToken);
            return WorkflowRunResultKind.Suspended;
        }
    }

    private static WorkflowRunAddress Address(string runId) => new(Fixture.Production, new WorkflowRunId(runId));

    private static RunnerKeyRing Ring(bool sealedProduction)
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, envelopeMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = new(KeyId, PayloadKey, envelopeMac, sealedProduction),
        });
    }
}