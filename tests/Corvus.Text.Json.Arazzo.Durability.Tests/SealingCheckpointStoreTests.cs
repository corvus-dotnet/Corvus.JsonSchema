// <copyright file="SealingCheckpointStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The runner's side of ADR 0065 decisions 4, 5 and 10: the decorator encrypts and seals what it saves for an
/// environment it holds a key for, verifies and opens what it loads, and refuses a clear row for an environment it
/// serves sealed. The run above it never sees a difference.
/// </summary>
[TestClass]
public sealed class SealingCheckpointStoreTests
{
    private const string Production = "production";
    private const string Development = "development";
    private static readonly DateTimeOffset T0 = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 1)).ToArray();
    private static readonly WorkflowRunAddress ProductionRun = new(Production, new WorkflowRunId("run-1"));
    private static readonly WorkflowRunAddress DevelopmentRun = new(Development, new WorkflowRunId("run-2"));

    [TestMethod]
    public async Task A_row_saved_for_a_held_environment_is_encrypted_and_sealed_at_the_store_and_opened_on_load()
    {
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(ProductionRun, inputs: "{\"petId\":7}");

        await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        CheckpointRowLayout layout = CheckpointRow.Parse(stored.Row.Span);
        layout.Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm, "what reaches the store is encrypted");
        CheckpointIntegrity.KeyIdOf(stored.Row.Span).ShouldBe("k1", "and sealed");
        CheckpointIntegrity.Verify(stored.Row.Span, EnvelopeMac()).ShouldBeTrue();
        System.Text.Encoding.Latin1.GetString(stored.Row.Span).Contains("petId", StringComparison.Ordinal).ShouldBeFalse("the payload does not reach the store in the clear");
        stored.Row.Span[layout.RunnerRegion].ToArray().ShouldBe(row[CheckpointRow.Parse(row).RunnerRegion], "the envelope is clear by design");
        WorkflowCheckpointSerializer.TryProject(stored.Row, out CheckpointProjection storedProjection).ShouldBeTrue("the control plane still projects the envelope");
        storedProjection.Sequence.ShouldBe(3);

        WorkflowCheckpoint loaded = (await store.LoadAsync(ProductionRun, default))!.Value;
        loaded.Etag.ShouldBe(stored.Etag, "the etag is the stored row's, so the next save is conditioned on it");
        CheckpointRow.Parse(loaded.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Clear, "the run sees a clear row");
        loaded.Row.ToArray().ShouldBe(row, "opened, the row is byte for byte the one that was saved");
    }

    [TestMethod]
    public async Task A_run_checkpoints_and_resumes_through_the_store_without_knowing_it_is_sealed()
    {
        // The run is crypto-free: it saves its products and gets them back, and the store in between holds ciphertext.
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        using ParsedJsonDocument<JsonElement> products = ParsedJsonDocument<JsonElement>.Parse("""{"inputs":{"petId":7},"getPet":{"status":"available"}}"""u8.ToArray());
        using (WorkflowRun run = WorkflowRun.CreateNew(store, ProductionRun.RunId, "petWorkflow", products.RootElement.GetProperty("inputs"u8), Production))
        {
            await run.BeginStepAsync("getPet", default);
            run.SetStepOutputs("getPet", products.RootElement.GetProperty("getPet"u8));
            run.RecordStep("getPet", WorkflowStepStatus.Succeeded, 1, T0, T0);
            await run.CheckpointAsync(1, default);
        }

        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        CheckpointRow.Parse(stored.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm);
        System.Text.Encoding.Latin1.GetString(stored.Row.Span).ShouldNotContain("available");

        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, ProductionRun);
        resumed.ShouldNotBeNull();
        resumed!.Cursor.ShouldBe(1);
        resumed.TryGetStepOutputs("getPet", out JsonElement getPet).ShouldBeTrue();
        getPet.GetProperty("status"u8).GetString().ShouldBe("available");
        resumed.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
    }

    [TestMethod]
    public async Task A_control_plane_reader_of_the_stored_row_gets_the_envelope_and_no_payload()
    {
        // ADR 0065 decision 5: the row at rest deserializes to its envelope alone for a reader without the key.
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(ProductionRun, inputs: "{\"petId\":7}");
        await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        using WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize((await inner.LoadAsync(ProductionRun, default))!.Value.Row);

        state.PayloadSealed.ShouldBeTrue();
        state.Sequence.ShouldBe(3);
        state.Environment.ShouldBe(Production);
        state.Inputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
        state.Outputs.ValueKind.ShouldBe(JsonValueKind.Undefined);
        state.StepOutputs.Count.ShouldBe(0);
        state.CorrelationTokens.ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_row_whose_payload_does_not_open_faults_the_load_like_a_bad_mac()
    {
        // The ciphertext moved and the row re-sealed by someone holding the MAC key but not the run: the MAC verifies,
        // the AEAD refuses, and the run sees the one integrity fault.
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(ProductionRun);
        await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        byte[] moved = [.. stored.Row.Span];
        CheckpointRowLayout layout = CheckpointRow.Parse(moved);
        moved[layout.Payload.Start.Value] ^= 0x01;
        byte[] mac = new byte[CheckpointIntegrity.MacLength];
        CheckpointIntegrity.Compute(CheckpointAlgorithm.Aes256Gcm, "k1"u8, moved.AsSpan()[layout.RunnerRegion], moved.AsSpan()[layout.Payload], EnvelopeMac(), mac);
        byte[] resealed = CheckpointRow.WithIntegrity(moved, "k1"u8, mac);
        CheckpointIntegrity.Verify(resealed, EnvelopeMac()).ShouldBeTrue("the MAC is genuine; only the AEAD can tell");
        await inner.SaveAsync(ProductionRun, resealed, WorkflowCheckpointSerializer.ProjectIndex(resealed), stored.Etag, default);

        CryptographicException fault = await Should.ThrowAsync<CryptographicException>(async () => await store.LoadAsync(ProductionRun, default));
        fault.Message.ShouldContain("does not verify");
    }

    [TestMethod]
    public async Task A_row_sealed_for_another_run_does_not_open_at_this_address()
    {
        // The whole row of run-1 copied over run-3's, MAC intact: the envelope names run-1 and the payload is bound
        // to it, so the address it is loaded at refuses it before the run compares names.
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(ProductionRun);
        await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);
        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        var other = new WorkflowRunAddress(Production, new WorkflowRunId("run-3"));
        await inner.SaveAsync(other, stored.Row, WorkflowCheckpointSerializer.ProjectIndex(stored.Row), WorkflowEtag.None, default);

        await Should.ThrowAsync<CryptographicException>(async () => await store.LoadAsync(other, default));
    }

    [TestMethod]
    public async Task A_macd_row_whose_payload_is_clear_is_refused_for_a_sealed_environment()
    {
        // This store never writes one: a MAC over a clear payload is a row that left the boundary in the clear.
        var inner = new InMemoryWorkflowStateStore();
        byte[] row = CheckpointIntegrity.Seal(Row(ProductionRun), "k1", EnvelopeMac());
        await inner.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        CryptographicException fault = await Should.ThrowAsync<CryptographicException>(async () => await new SealingCheckpointStore(inner, Ring(sealedProduction: true)).LoadAsync(ProductionRun, default));
        fault.Message.ShouldContain("clear row");

        (await new SealingCheckpointStore(inner, Ring(sealedProduction: false)).LoadAsync(ProductionRun, default)).ShouldNotBeNull("an environment held but not marked sealed tolerates it");
    }

    [TestMethod]
    public async Task A_row_that_already_names_a_generation_is_not_sealed_twice()
    {
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = CheckpointIntegrity.Seal(Row(ProductionRun), "k1", EnvelopeMac());

        await Should.ThrowAsync<InvalidOperationException>(async () => await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default));
        (await inner.LoadAsync(ProductionRun, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_row_for_an_environment_the_ring_does_not_admit_is_neither_loaded_nor_saved()
    {
        // ADR 0065 decision 10: the ring is the allowlist. An environment with no entry is not served at all, so a
        // binding the control plane wrote for it gets the runner nothing; an entry with no key is served clear.
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(DevelopmentRun, inputs: "{\"petId\":7}");
        await inner.SaveAsync(DevelopmentRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        (await Should.ThrowAsync<CheckpointEnvironmentNotAdmittedException>(async () => await store.LoadAsync(DevelopmentRun, default))).Address.ShouldBe(DevelopmentRun);
        await Should.ThrowAsync<CheckpointEnvironmentNotAdmittedException>(async () => await store.SaveAsync(DevelopmentRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default));

        var admitting = new SealingCheckpointStore(inner, RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys> { [Production] = new("k1", PayloadKey, EnvelopeMac(), true) }, Development));
        WorkflowCheckpoint loaded = (await admitting.LoadAsync(DevelopmentRun, default))!.Value;
        loaded.Row.ToArray().ShouldBe(row, "an admitted environment with no key is served clear");
        RunnerKeyRing.Admitting(Development).Admits(Development).ShouldBeTrue();
        RunnerKeyRing.Admitting(Development).Admits(Production).ShouldBeFalse();
        RunnerKeyRing.Empty.IsEmpty.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_tampered_row_faults_the_load()
    {
        // SEQ-1: the envelope rewritten at rest. The run never sees the row; the load faults with the integrity error.
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(ProductionRun);
        await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        WorkflowCheckpoint stored = (await inner.LoadAsync(ProductionRun, default))!.Value;
        byte[] tampered = CheckpointRow.WithIntegrity(stored.Row.Span, "k1"u8, new byte[CheckpointIntegrity.MacLength]);
        await inner.SaveAsync(ProductionRun, tampered, WorkflowCheckpointSerializer.ProjectIndex(tampered), stored.Etag, default);

        CryptographicException fault = await Should.ThrowAsync<CryptographicException>(async () => await store.LoadAsync(ProductionRun, default));
        fault.Message.ShouldContain("does not verify");
    }

    [TestMethod]
    public async Task A_row_under_a_generation_the_ring_does_not_hold_faults_the_load()
    {
        var inner = new InMemoryWorkflowStateStore();
        byte[] row = CheckpointIntegrity.Seal(Row(ProductionRun), "k0", EnvelopeMac(keyId: "k0"));
        await inner.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));

        await Should.ThrowAsync<CryptographicException>(async () => await store.LoadAsync(ProductionRun, default));
    }

    [TestMethod]
    public async Task A_clear_genesis_row_for_a_sealed_environment_is_accepted()
    {
        // ADR 0065 decisions 4 and 6: the control plane writes a run's first row before any runner has claimed it,
        // holding no key and no lease. It is the one clear row a sealed environment's runner opens, told by its
        // missing lease epoch. Substituting a whole row with a clear genesis row is a rollback, the anchor's to catch.
        var inner = new InMemoryWorkflowStateStore();
        byte[] row = Row(ProductionRun, sequence: 1, epoch: null);
        await inner.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));

        (await store.LoadAsync(ProductionRun, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_clear_row_for_a_sealed_environment_is_refused()
    {
        // The row a control plane, a backup or a peer without the key wrote over one a runner had sealed: it carries
        // a runner's lease epoch and no MAC. Nothing in it is trusted.
        var inner = new InMemoryWorkflowStateStore();
        byte[] row = Row(ProductionRun, epoch: 2);
        await inner.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));

        CryptographicException fault = await Should.ThrowAsync<CryptographicException>(async () => await store.LoadAsync(ProductionRun, default));
        fault.Message.ShouldContain("clear row");
    }

    [TestMethod]
    public async Task A_clear_row_for_a_held_but_unsealed_environment_is_accepted()
    {
        // A ring entry that is not marked sealed seals what it writes and verifies what carries a MAC, but tolerates a
        // clear row: the migration posture for an environment whose rows predate its key.
        var inner = new InMemoryWorkflowStateStore();
        byte[] row = Row(ProductionRun, epoch: 2);
        await inner.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: false));

        (await store.LoadAsync(ProductionRun, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task A_sealed_start_opens_at_first_claim_into_the_clear_row_the_run_resumes_from()
    {
        // ADR 0065 decision 9: the control plane wrote the initiator's seal as the genesis row; the runner that holds
        // the seal key and pins the initiator opens it, and the run sees its inputs as any inputs.
        (byte[] sealSpki, byte[] sealPkcs8) = InputSealTests.SealKeyPair();
        using var initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, SealingRing(sealPkcs8, [initiator.ExportSubjectPublicKeyInfo()]));
        WorkflowRunAddress address = SealedRun;
        await EnqueueSealedAsync(inner, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 3, address.RunId.Value, """{"petId":7}"""u8, initiator));

        WorkflowCheckpoint stored = (await inner.LoadAsync(address, default))!.Value;
        CheckpointRow.Parse(stored.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.SealedGenesis);
        WorkflowCheckpoint opened = (await store.LoadAsync(address, default))!.Value;
        opened.Etag.ShouldBe(stored.Etag);
        CheckpointRowLayout layout = CheckpointRow.Parse(opened.Row.Span);
        layout.Algorithm.ShouldBe(CheckpointAlgorithm.Clear);
        opened.Row.Span[layout.RunnerRegion].ToArray().ShouldBe(stored.Row.Span[CheckpointRow.Parse(stored.Row.Span).RunnerRegion].ToArray(), "the envelope is the control plane's, as written");
        using (WorkflowCheckpointState state = WorkflowCheckpointSerializer.Deserialize(opened.Row))
        {
            state.SealedStart.ShouldBeTrue();
            state.PayloadSealed.ShouldBeFalse();
            state.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
            state.Sequence.ShouldBe(0);
        }

        // The run resumes from it and its first save is sealed under the environment's key, saying it started sealed.
        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, address, leaseEpoch: 2))!)
        {
            run.SealedStart.ShouldBeTrue();
            run.Sequence.ShouldBe(0);
            await run.CheckpointAsync(1, default);
        }

        WorkflowCheckpoint saved = (await inner.LoadAsync(address, default))!.Value;
        CheckpointRow.Parse(saved.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.Aes256Gcm);
        using WorkflowCheckpointState resumed = WorkflowCheckpointSerializer.Deserialize((await store.LoadAsync(address, default))!.Value.Row);
        resumed.SealedStart.ShouldBeTrue();
        resumed.Sequence.ShouldBe(1);
        resumed.Inputs.GetProperty("petId"u8).GetInt32().ShouldBe(7);
    }

    [TestMethod]
    public async Task A_sealed_start_that_does_not_open_is_refused_with_the_row_and_why()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = InputSealTests.SealKeyPair();
        (byte[] otherSpki, byte[] otherPkcs8) = InputSealTests.SealKeyPair();
        using var initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] pinned = initiator.ExportSubjectPublicKeyInfo();
        WorkflowRunAddress address = SealedRun;

        // The initiator's genuine seal for this run, opened by a runner holding no seal key at all.
        var noSealKey = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(noSealKey, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 3, address.RunId.Value, "{}"u8, initiator));
        SealedStartException fault = await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(noSealKey, Ring(sealedProduction: true)).LoadAsync(address, default));
        fault.Refusal.ShouldBe(SealedStartRefusal.NoSealKey);
        fault.Address.ShouldBe(address);
        fault.Row.ToArray().ShouldBe((await noSealKey.LoadAsync(address, default))!.Value.Row.ToArray(), "the row is carried so the runner can fault the run from its envelope");
        fault.Etag.ShouldBe((await noSealKey.LoadAsync(address, default))!.Value.Etag);

        // Sealed to another generation than the one the runner holds.
        var otherGeneration = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(otherGeneration, address, RunStartInitiator.Seal(otherSpki, "k0", Production, "pet", 3, address.RunId.Value, "{}"u8, initiator));
        (await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(otherGeneration, SealingRing(sealPkcs8, [pinned])).LoadAsync(address, default))).Refusal.ShouldBe(SealedStartRefusal.UnknownGeneration);

        // Signed by an initiator the runner does not pin: the seal itself is fine, which is the point.
        var unpinned = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(unpinned, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 3, address.RunId.Value, "{}"u8, stranger));
        (await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(unpinned, SealingRing(sealPkcs8, [pinned])).LoadAsync(address, default))).Refusal.ShouldBe(SealedStartRefusal.UnpinnedInitiator);

        // Sealed for another run, another workflow version, or another environment: the binding the runner derives
        // from its own address and the envelope does not match, so the signature and the seal both fail.
        var movedRun = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(movedRun, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 3, "fedcba9876543210fedcba9876543210", "{}"u8, initiator));
        (await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(movedRun, SealingRing(sealPkcs8, [pinned])).LoadAsync(address, default))).Refusal.ShouldBe(SealedStartRefusal.UnpinnedInitiator);
        var movedVersion = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(movedVersion, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 4, address.RunId.Value, "{}"u8, initiator));
        (await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(movedVersion, SealingRing(sealPkcs8, [pinned])).LoadAsync(address, default))).Refusal.ShouldBe(SealedStartRefusal.UnpinnedInitiator);

        // Sealed to another key of the same generation id: the signature verifies, and the seal does not open.
        var otherKey = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(otherKey, address, RunStartInitiator.Seal(otherSpki, "k1", Production, "pet", 3, address.RunId.Value, "{}"u8, initiator));
        (await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(otherKey, SealingRing(sealPkcs8, [pinned])).LoadAsync(address, default))).Refusal.ShouldBe(SealedStartRefusal.Unopenable);

        // Sealed plaintext that is not a JSON value opens cryptographically and is still no start.
        var notJson = new InMemoryWorkflowStateStore();
        await EnqueueSealedAsync(notJson, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 3, address.RunId.Value, "not json"u8, initiator));
        (await Should.ThrowAsync<SealedStartException>(async () => await new SealingCheckpointStore(notJson, SealingRing(sealPkcs8, [pinned])).LoadAsync(address, default))).Refusal.ShouldBe(SealedStartRefusal.Unopenable);

        // A runner that admits the environment clear and holds no key for it gets the row as stored, as any keyless
        // reader does; one that does not admit the environment at all gets nothing (decision 10).
        var elsewhere = new InMemoryWorkflowStateStore();
        WorkflowRunAddress developmentRun = new(Development, address.RunId);
        await EnqueueSealedAsync(elsewhere, developmentRun, RunStartInitiator.Seal(sealSpki, "k1", Development, "pet", 3, address.RunId.Value, "{}"u8, initiator));
        var admittingClear = new SealingCheckpointStore(elsewhere, RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys> { [Production] = new("k1", PayloadKey, EnvelopeMac(), true, sealPkcs8, [pinned]) }, Development));
        CheckpointRow.Parse((await admittingClear.LoadAsync(developmentRun, default))!.Value.Row.Span).Algorithm.ShouldBe(CheckpointAlgorithm.SealedGenesis);
        await Should.ThrowAsync<CheckpointEnvironmentNotAdmittedException>(async () => await new SealingCheckpointStore(elsewhere, SealingRing(sealPkcs8, [pinned])).LoadAsync(developmentRun, default));
        _ = otherPkcs8;
    }

    [TestMethod]
    public async Task An_anchored_sealed_start_is_the_origin_the_tenant_commits_to_under_the_genesis_label()
    {
        (byte[] sealSpki, byte[] sealPkcs8) = InputSealTests.SealKeyPair();
        using var initiator = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var inner = new InMemoryWorkflowStateStore();
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Production, 1, default);
        var store = new SealingCheckpointStore(inner, SealingRing(sealPkcs8, [initiator.ExportSubjectPublicKeyInfo()]), anchors);
        WorkflowRunAddress address = SealedRun;
        await EnqueueSealedAsync(inner, address, RunStartInitiator.Seal(sealSpki, "k1", Production, "pet", 3, address.RunId.Value, "{}"u8, initiator));
        WorkflowCheckpoint genesis = (await inner.LoadAsync(address, default))!.Value;
        CheckpointRowLayout layout = CheckpointRow.Parse(genesis.Row.Span);
        AnchorDigest expected = CheckpointDigest.ForGenesis(genesis.Row.Span[layout.Payload], genesis.Row.Span[layout.Mac]);

        using (WorkflowRun run = (await WorkflowRun.ResumeAsync(store, address, leaseEpoch: 2, incarnation: 1))!)
        {
            await run.CheckpointAsync(1, default);
        }

        AnchorRecord record = (await anchors.ReadAsync(Production, address.RunId.Value, default))!.Value;
        record.Committed.Sequence.ShouldBe(0UL);
        record.Committed.Digest.ShouldBe(expected, "the create commits to the initiator-signed row under the genesis label, not the ordinary one");
        record.Committed.Digest.ShouldNotBe(CheckpointDigest.ForSubmitted(CheckpointRow.SubmittedBytes(genesis.Row).Span));
    }

    [TestMethod]
    public async Task The_ring_is_built_from_the_runners_own_secret_store()
    {
        // Decision 5: the payload key is the runner's, read through its own resolver; the ring derives the subkeys once.
        var secrets = new FixedSecretResolver(Convert.ToBase64String(PayloadKey));
        RunnerKeyRing ring = await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry(Production, Sealed: true, KeyId: "k1", PayloadKey: SecretRef.Parse("env://PAYLOAD_KEY"), SealKeyFingerprint: "pinned")], secrets, default);

        ring.IsEmpty.ShouldBeFalse();
        ring.IsSealed(Production).ShouldBeTrue();
        ring.IsSealed(Development).ShouldBeFalse();
        ring.TryGet(Production, out RunnerEnvironmentKeys keys).ShouldBeTrue();
        keys.KeyId.ShouldBe("k1");
        keys.PayloadKey.ShouldBe(PayloadKey);
        keys.EnvelopeMac.ShouldBe(EnvelopeMac());
        secrets.Resolutions.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_payload_key_that_is_not_thirty_two_bytes_refuses_to_build_the_ring()
    {
        var secrets = new FixedSecretResolver(Convert.ToBase64String(new byte[16]));
        InvalidOperationException fault = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await RunnerKeyRing.BuildAsync([new RunnerKeyRingEntry(Production, Sealed: true, KeyId: "k1", PayloadKey: SecretRef.Parse("env://PAYLOAD_KEY"), SealKeyFingerprint: "pinned")], secrets, default));
        fault.Message.ShouldContain("32-byte");
    }

    private static RunnerKeyRing Ring(bool sealedProduction)
        => RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Production] = new("k1", PayloadKey, EnvelopeMac(), sealedProduction),
        });

    private static readonly WorkflowRunAddress SealedRun = new(Production, new WorkflowRunId("0123456789abcdef0123456789abcdef"));

    private static RunnerKeyRing SealingRing(byte[] sealPrivateKey, IReadOnlyList<byte[]> initiators)
        => RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Production] = new("k1", PayloadKey, EnvelopeMac(), Sealed: true, sealPrivateKey, initiators),
        });

    // What the control plane's sealed start does: the seal becomes the genesis row, unread.
    private static async ValueTask EnqueueSealedAsync(IWorkflowCheckpointStore store, WorkflowRunAddress address, SealedInputs sealedInputs)
    {
        using WorkflowRun run = WorkflowRun.CreateSealed(store, address.RunId, "pet-v3", sealedInputs, address.Environment);
        await run.EnqueueAsync(default);
    }

    private static byte[] EnvelopeMac(string keyId = "k1")
    {
        byte[] subkey = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Production, keyId, subkey);
        return subkey;
    }

    private static byte[] Row(WorkflowRunAddress address, long sequence = 3, long? epoch = 2, string? inputs = null)
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
        using ParsedJsonDocument<JsonElement>? inputsDocument = inputs is null ? null : ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(inputs));
        return WorkflowCheckpointSerializer.Serialize(
            new CheckpointEnvelope(
                address.RunId,
                address.Environment,
                "petWorkflow",
                WorkflowRunStatus.Running,
                0,
                sequence,
                Epoch: epoch,
                T0,
                T0,
                CorrelationId: null,
                RerunOf: null,
                default,
                default,
                [],
                false,
                null,
                null),
            retryCounters,
            new Dictionary<string, byte[]>(StringComparer.Ordinal),
            inputs: inputsDocument?.RootElement ?? default,
            stepOutputs,
            outputs: default,
            []);
    }

    private sealed class FixedSecretResolver(string secret) : ISecretResolver
    {
        public int Resolutions { get; private set; }

        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
        {
            this.Resolutions++;
            return new(SecretMaterial.FromString(secret));
        }
    }
}