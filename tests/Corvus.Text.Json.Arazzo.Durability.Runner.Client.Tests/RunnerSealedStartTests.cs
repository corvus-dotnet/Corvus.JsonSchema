// <copyright file="RunnerSealedStartTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// A sealed start over the real runner API (ADR 0065 decision 9): an initiator seals the inputs to the environment's
/// seal key and signs them, the control plane stores the seal as the genesis row and reads none of it, and the runner
/// that first claims the run opens the inputs, validates them against the version's schema, and carries on, or
/// faults the run at its start and never claims it again.
/// </summary>
[TestClass]
public sealed class RunnerSealedStartTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string Run2 = "fedcba9876543210fedcba9876543210";
    private const string KeyId = "k2";
    private const string Base = "adopt";
    private const string Version = "adopt-v1";
    private const string InputsSchema = """{ "type": "object", "required": ["email"], "properties": { "email": { "type": "string" } } }""";
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 4)).ToArray();

    [TestMethod]
    public async Task A_sealed_start_is_opened_validated_and_advanced_by_the_runner_that_holds_the_seal_key_and_pins_the_initiator()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealPkcs8, [initiator.ExportSubjectPublicKeyInfo()]),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedCatalogAsync(Base, Fixture.Production, InputsSchema);

        // The initiator seals and the control plane enqueues: the row it writes carries ciphertext and a signature,
        // and nothing that reads the store sees the inputs.
        SealedInputs sealedInputs = RunStartInitiator.Seal(sealSpki, KeyId, Fixture.Production, Base, 1, Run1, """{"email":"ada@example.com"}"""u8, initiator);
        await EnqueueSealedAsync(fixture, Run1, sealedInputs);
        WorkflowCheckpoint genesis = (await fixture.Store.LoadAsync(Address(Run1), default))!.Value;
        CheckpointRow.Parse(genesis.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.SealedGenesis);
        System.Text.Encoding.Latin1.GetString(genesis.Row.Span).Contains("ada@example.com", StringComparison.Ordinal).ShouldBeFalse();
        using (WorkflowCheckpointState keyless = WorkflowCheckpointSerializer.Deserialize(genesis.Row))
        {
            keyless.PayloadSealed.ShouldBeTrue("a keyless reader gets the envelope alone");
            keyless.SealedStart.ShouldBeTrue();
        }

        // The runner's first claim opens the inputs; the executor sees them as any inputs.
        string? seen = null;
        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        (await dispatcher.DispatchClaimableAsync([Version], async (run, ct) =>
        {
            run.SealedStart.ShouldBeTrue();
            seen = run.Inputs.GetProperty("email"u8).GetString();
            await run.CheckpointAsync(run.Cursor + 1, ct);
            await run.SuspendForTimerAsync(run.Cursor, TimeSpan.FromMinutes(5), ct);
            return WorkflowRunResultKind.Suspended;
        }, default)).ShouldBe(1);
        seen.ShouldBe("ada@example.com");

        // What the runner wrote is sealed under the environment's key and says the run started sealed, inside the
        // MAC'd region; the tenant's anchor committed to the initiator-signed genesis row as the origin.
        WorkflowCheckpoint rested = (await fixture.Store.LoadAsync(Address(Run1), default))!.Value;
        CheckpointRow.Parse(rested.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm);
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(rested.Row))
        {
            state.SealedStart.ShouldBeTrue();
            state.Sequence.ShouldBe(2);
            state.Status.ShouldBe(WorkflowRunStatus.Suspended);
        }

        AnchorRecord record = (await anchors.ReadAsync(Fixture.Production, Run1, default))!.Value;
        CheckpointRowLayout genesisLayout = CheckpointRow.Parse(genesis.Row.Span);
        record.EpochHighWater.Incarnation.ShouldBe(1UL);
        record.Committed.Sequence.ShouldBe(2UL);
        (await fixture.Store.AcquireLeaseAsync(Address(Run1), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the lease was given back");
        _ = CheckpointDigest.ForGenesis(genesis.Row.Span[genesisLayout.Payload], genesis.Row.Span[genesisLayout.Mac]);
    }

    [TestMethod]
    public async Task The_anchor_creates_the_record_over_the_genesis_digest_of_the_initiator_signed_row()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealPkcs8, [initiator.ExportSubjectPublicKeyInfo()]),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedCatalogAsync(Base, Fixture.Production);
        await EnqueueSealedAsync(fixture, Run1, RunStartInitiator.Seal(sealSpki, KeyId, Fixture.Production, Base, 1, Run1, "{}"u8, initiator));
        WorkflowCheckpoint genesis = (await fixture.Store.LoadAsync(Address(Run1), default))!.Value;
        CheckpointRowLayout layout = CheckpointRow.Parse(genesis.Row.Span);
        AnchorDigest expected = CheckpointDigest.ForGenesis(genesis.Row.Span[layout.Payload], genesis.Row.Span[layout.Mac]);

        // One save mid-advance, then a crash before the run rests: the record holds the create over the genesis
        // digest as committed and the save as pending, which is where the genesis digest is visible.
        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        await Should.ThrowAsync<InvalidOperationException>(async () => await dispatcher.DispatchClaimableAsync([Version], async (run, ct) =>
        {
            await run.CheckpointAsync(run.Cursor + 1, ct);
            throw new InvalidOperationException("crash");
        }, default));

        AnchorRecord record = (await anchors.ReadAsync(Fixture.Production, Run1, default))!.Value;
        record.Committed.Sequence.ShouldBe(0UL);
        record.Committed.Digest.ShouldBe(expected, "the origin the tenant committed to is the initiator-signed row under the genesis label");
        record.Pending!.Value.Sequence.ShouldBe(1UL);
    }

    [TestMethod]
    public async Task Inputs_that_do_not_validate_fault_the_run_at_its_start_and_it_is_not_claimed_again()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealPkcs8, [initiator.ExportSubjectPublicKeyInfo()]),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedCatalogAsync(Base, Fixture.Production, InputsSchema);
        await EnqueueSealedAsync(fixture, Run1, RunStartInitiator.Seal(sealSpki, KeyId, Fixture.Production, Base, 1, Run1, """{"name":"no email"}"""u8, initiator));

        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        (await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("the executor must not run on invalid inputs"), default)).ShouldBe(0);

        await ShouldBeFaultedAtStartAsync(fixture, Run1, SealedStartFault.InputsInvalid);
        (await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("not claimable again"), default)).ShouldBe(0);
    }

    [TestMethod]
    public async Task A_seal_signed_by_an_initiator_the_runner_does_not_pin_is_unopenable()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = SealKeyPair();
        using ECDsa pinned = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealPkcs8, [pinned.ExportSubjectPublicKeyInfo()]),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedCatalogAsync(Base, Fixture.Production, InputsSchema);

        // Valid inputs, a valid seal to the right key: only the signer is wrong. A control plane holding the public
        // seal key could make exactly this start.
        await EnqueueSealedAsync(fixture, Run1, RunStartInitiator.Seal(sealSpki, KeyId, Fixture.Production, Base, 1, Run1, """{"email":"mallory@example.com"}"""u8, stranger));

        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        (await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("the executor must not run"), default)).ShouldBe(0);
        await ShouldBeFaultedAtStartAsync(fixture, Run1, SealedStartFault.Unopenable);
    }

    [TestMethod]
    public async Task A_seal_moved_to_another_run_does_not_open_there()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealPkcs8, [initiator.ExportSubjectPublicKeyInfo()]),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedCatalogAsync(Base, Fixture.Production, InputsSchema);

        // The control plane stores a seal the initiator made for run 1 under run 2: the signature is genuine and the
        // ciphertext untouched, and the binding says it is not for this run.
        SealedInputs forRun1 = RunStartInitiator.Seal(sealSpki, KeyId, Fixture.Production, Base, 1, Run1, """{"email":"ada@example.com"}"""u8, initiator);
        await EnqueueSealedAsync(fixture, Run2, forRun1);

        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        (await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("the executor must not run"), default)).ShouldBe(0);
        await ShouldBeFaultedAtStartAsync(fixture, Run2, SealedStartFault.Unopenable);
    }

    [TestMethod]
    public async Task A_runner_without_the_seal_key_faults_a_sealed_start_rather_than_running_it_blind()
    {
        (byte[] sealSpki, _) = SealKeyPair();
        using ECDsa initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(sealPrivateKey: null, initiators: null),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) });
        await fixture.SeedCatalogAsync(Base, Fixture.Production, InputsSchema);
        await EnqueueSealedAsync(fixture, Run1, RunStartInitiator.Seal(sealSpki, KeyId, Fixture.Production, Base, 1, Run1, """{"email":"ada@example.com"}"""u8, initiator));

        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        (await dispatcher.DispatchClaimableAsync([Version], (_, _) => throw new InvalidOperationException("the executor must not run"), default)).ShouldBe(0);
        await ShouldBeFaultedAtStartAsync(fixture, Run1, SealedStartFault.Unopenable);
    }

    private static async ValueTask ShouldBeFaultedAtStartAsync(Fixture fixture, string runId, string errorType)
    {
        WorkflowCheckpoint faulted = (await fixture.Store.LoadAsync(Address(runId), default))!.Value;
        CheckpointRow.Parse(faulted.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm, "the fault is a save like any other, sealed under the environment's key");
        WorkflowRunIndexEntry index = WorkflowCheckpointSerializer.ProjectIndex(faulted.Row);
        index.Status.ShouldBe(WorkflowRunStatus.Faulted);
        index.ErrorType.ShouldBe(errorType);
        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(faulted.Row);
        state.SealedStart.ShouldBeTrue();
        state.Sequence.ShouldBe(1);
        state.Fault!.Value.StepId.ShouldBe(SealedStartFault.StepId);
        (await fixture.Store.AcquireLeaseAsync(Address(runId), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the lease was given back");
    }

    private static async ValueTask EnqueueSealedAsync(Fixture fixture, string runId, SealedInputs sealedInputs)
    {
        // What the control plane's sealed start does: the seal becomes the genesis row, unread.
        using WorkflowRun run = WorkflowRun.CreateSealed(fixture.Store, new WorkflowRunId(runId), Version, sealedInputs, Fixture.Production, fixture.Clock);
        await run.EnqueueAsync(default);
    }

    private static WorkflowRunAddress Address(string runId) => new(Fixture.Production, new WorkflowRunId(runId));

    private static (byte[] Spki, byte[] Pkcs8) SealKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfo(), key.ExportPkcs8PrivateKey());
    }

    private static RunnerKeyRing Ring(byte[]? sealPrivateKey, IReadOnlyList<byte[]>? initiators)
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, envelopeMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = new(KeyId, PayloadKey, envelopeMac, Sealed: true, sealPrivateKey, initiators),
        });
    }
}