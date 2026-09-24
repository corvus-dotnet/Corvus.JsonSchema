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
/// The runner's side of ADR 0065 decisions 4 and 10: the decorator seals what it saves for an environment it holds a
/// key for, verifies what it loads, and refuses a clear row for an environment it serves sealed. The run above it never
/// sees a difference.
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
    public async Task A_row_saved_for_a_held_environment_is_sealed_at_the_store_and_verified_on_load()
    {
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(ProductionRun);

        await store.SaveAsync(ProductionRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        WorkflowCheckpoint? stored = await inner.LoadAsync(ProductionRun, default);
        CheckpointIntegrity.KeyIdOf(stored!.Value.Row.Span).ShouldBe("k1", "what reaches the store is sealed");
        CheckpointIntegrity.Verify(stored.Value.Row.Span, EnvelopeMac()).ShouldBeTrue();

        WorkflowCheckpoint? loaded = await store.LoadAsync(ProductionRun, default);
        loaded.ShouldNotBeNull();
        WorkflowCheckpointSerializer.TryProject(loaded!.Value.Row, out CheckpointProjection projection).ShouldBeTrue();
        projection.Sequence.ShouldBe(3);
    }

    [TestMethod]
    public async Task A_row_for_an_environment_the_ring_does_not_hold_passes_through_clear()
    {
        var inner = new InMemoryWorkflowStateStore();
        var store = new SealingCheckpointStore(inner, Ring(sealedProduction: true));
        byte[] row = Row(DevelopmentRun);

        await store.SaveAsync(DevelopmentRun, row, WorkflowCheckpointSerializer.ProjectIndex(row), WorkflowEtag.None, default);

        CheckpointIntegrity.KeyIdOf((await inner.LoadAsync(DevelopmentRun, default))!.Value.Row.Span).ShouldBeNull();
        (await store.LoadAsync(DevelopmentRun, default)).ShouldNotBeNull();
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
    public async Task The_ring_is_built_from_the_runners_own_secret_store()
    {
        // Decision 5: the payload key is the runner's, read through its own resolver; the ring derives the subkeys once.
        var secrets = new FixedSecretResolver(Convert.ToBase64String(PayloadKey));
        RunnerKeyRing ring = await RunnerKeyRing.BuildAsync(
            [new RunnerKeyRingEntry(Production, "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true)], secrets, default);

        ring.IsEmpty.ShouldBeFalse();
        ring.IsSealed(Production).ShouldBeTrue();
        ring.IsSealed(Development).ShouldBeFalse();
        ring.TryGet(Production, out RunnerEnvironmentKeys keys).ShouldBeTrue();
        keys.KeyId.ShouldBe("k1");
        keys.EnvelopeMac.ShouldBe(EnvelopeMac());
        secrets.Resolutions.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_payload_key_that_is_not_thirty_two_bytes_refuses_to_build_the_ring()
    {
        var secrets = new FixedSecretResolver(Convert.ToBase64String(new byte[16]));
        InvalidOperationException fault = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await RunnerKeyRing.BuildAsync([new RunnerKeyRingEntry(Production, "k1", SecretRef.Parse("env://PAYLOAD_KEY"), Sealed: true)], secrets, default));
        fault.Message.ShouldContain("32-byte");
    }

    private static RunnerKeyRing Ring(bool sealedProduction)
        => RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Production] = new("k1", EnvelopeMac(), sealedProduction),
        });

    private static byte[] EnvelopeMac(string keyId = "k1")
    {
        byte[] subkey = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Production, keyId, subkey);
        return subkey;
    }

    private static byte[] Row(WorkflowRunAddress address, long sequence = 3, long? epoch = 2)
    {
        using var retryCounters = PooledUtf8Map<int>.Rent(0);
        using var stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
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
            inputs: default,
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