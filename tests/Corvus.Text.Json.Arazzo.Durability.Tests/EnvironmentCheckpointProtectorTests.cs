// <copyright file="EnvironmentCheckpointProtectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves the environment-routing protector implements the ADR 0065 trust matrix end to end: a sealed
/// environment's saves are sealed to its registered key and follow rotation; unsealed environments and unpinned
/// runs take the baseline; a runner holding the open key reads its environments' state; the control plane
/// (no open keys) is refused by key custody with the key id named; and other tenants' state stays closed.
/// </summary>
[TestClass]
public sealed class EnvironmentCheckpointProtectorTests
{
    private static readonly WorkflowRunId Run = new("run-1");
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("""{"outputs":{"secret":"tenant data"}}""");

    [TestMethod]
    public async Task A_sealed_environments_save_routes_to_its_key_and_the_runner_ring_opens_it()
    {
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using var baseline = new AesGcmCheckpointProtector(new byte[32]);

        // The control plane's posture: it resolves the seal key from the environment record and holds no open key.
        using var controlPlane = new EnvironmentCheckpointProtector(
            baseline,
            (environment, _) => new ValueTask<EnvironmentSealKey?>(
                environment == "acme-prod" ? new EnvironmentSealKey("acme-1", pair.SealKey) : null));

        ReadOnlyMemory<byte> sealed_ = await controlPlane.ProtectAsync(Plaintext, Run, "acme-prod", default);
        SealedCheckpointProtector.TryReadSealedKeyId(sealed_.Span, out string keyId).ShouldBeTrue();
        keyId.ShouldBe("acme-1");

        // The runner's posture: the environment's open key registered from the tenant's custody.
        using var runner = new EnvironmentCheckpointProtector(
            baseline,
            openKeys: [new KeyValuePair<string, ReadOnlyMemory<byte>>("acme-1", pair.OpenKey)]);

        (await runner.UnprotectAsync(sealed_, Run, default)).ToArray().ShouldBe(Plaintext);
    }

    [TestMethod]
    public async Task The_control_plane_cannot_read_sealed_state_and_the_refusal_names_the_key()
    {
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using var baseline = new AesGcmCheckpointProtector(new byte[32]);
        using var controlPlane = new EnvironmentCheckpointProtector(
            baseline,
            (_, _) => new ValueTask<EnvironmentSealKey?>(new EnvironmentSealKey("acme-1", pair.SealKey)));

        ReadOnlyMemory<byte> sealed_ = await controlPlane.ProtectAsync(Plaintext, Run, "acme-prod", default);

        CryptographicException refusal = await Should.ThrowAsync<CryptographicException>(
            async () => await controlPlane.UnprotectAsync(sealed_, Run, default));
        refusal.Message.ShouldContain("acme-1");
    }

    [TestMethod]
    public async Task Another_tenants_runner_is_refused_and_a_forged_ring_entry_fails_closed()
    {
        SealedCheckpointKeyPair acme = SealedCheckpointProtector.GenerateKeyPair();
        SealedCheckpointKeyPair rival = SealedCheckpointProtector.GenerateKeyPair();
        using var baseline = new AesGcmCheckpointProtector(new byte[32]);
        using var sealer = new EnvironmentCheckpointProtector(
            baseline,
            (_, _) => new ValueTask<EnvironmentSealKey?>(new EnvironmentSealKey("acme-1", acme.SealKey)));

        ReadOnlyMemory<byte> sealed_ = await sealer.ProtectAsync(Plaintext, Run, "acme-prod", default);

        // The rival tenant's runner holds only its own key: refused by key id (custody, not policy).
        using var rivalRunner = new EnvironmentCheckpointProtector(
            baseline,
            openKeys: [new KeyValuePair<string, ReadOnlyMemory<byte>>("rival-1", rival.OpenKey)]);
        await Should.ThrowAsync<CryptographicException>(async () => await rivalRunner.UnprotectAsync(sealed_, Run, default));

        // Even a rival registering its own key UNDER acme's key id fails closed: the ECDH agreement is wrong.
        using var forger = new EnvironmentCheckpointProtector(
            baseline,
            openKeys: [new KeyValuePair<string, ReadOnlyMemory<byte>>("acme-1", rival.OpenKey)]);
        await Should.ThrowAsync<CryptographicException>(async () => await forger.UnprotectAsync(sealed_, Run, default));
    }

    [TestMethod]
    public async Task Unsealed_environments_and_unpinned_runs_take_the_baseline_and_round_trip()
    {
        using var baseline = new AesGcmCheckpointProtector(new byte[32]);
        using var protector = new EnvironmentCheckpointProtector(
            baseline,
            (_, _) => new ValueTask<EnvironmentSealKey?>((EnvironmentSealKey?)null));

        ReadOnlyMemory<byte> unsealedEnvironment = await protector.ProtectAsync(Plaintext, Run, "open-dev", default);
        ReadOnlyMemory<byte> unpinned = await protector.ProtectAsync(Plaintext, Run, null, default);

        SealedCheckpointProtector.TryReadSealedKeyId(unsealedEnvironment.Span, out _).ShouldBeFalse();
        (await protector.UnprotectAsync(unsealedEnvironment, Run, default)).ToArray().ShouldBe(Plaintext);
        (await protector.UnprotectAsync(unpinned, Run, default)).ToArray().ShouldBe(Plaintext);
    }

    [TestMethod]
    public async Task Rotation_follows_the_resolver_and_the_ring_opens_both_generations()
    {
        SealedCheckpointKeyPair first = SealedCheckpointProtector.GenerateKeyPair();
        SealedCheckpointKeyPair second = SealedCheckpointProtector.GenerateKeyPair();
        using var baseline = new AesGcmCheckpointProtector(new byte[32]);

        EnvironmentSealKey current = new("acme-2026-08", first.SealKey);
        using var sealer = new EnvironmentCheckpointProtector(
            baseline,
            (_, _) => new ValueTask<EnvironmentSealKey?>(current));

        ReadOnlyMemory<byte> beforeRotation = await sealer.ProtectAsync(Plaintext, Run, "acme-prod", default);
        current = new EnvironmentSealKey("acme-2026-09", second.SealKey);
        ReadOnlyMemory<byte> afterRotation = await sealer.ProtectAsync(Plaintext, Run, "acme-prod", default);

        SealedCheckpointProtector.TryReadSealedKeyId(beforeRotation.Span, out string firstId).ShouldBeTrue();
        SealedCheckpointProtector.TryReadSealedKeyId(afterRotation.Span, out string secondId).ShouldBeTrue();
        firstId.ShouldBe("acme-2026-08");
        secondId.ShouldBe("acme-2026-09");

        // A runner ring holding both generations reads state sealed before and after the rotation.
        using var runner = new EnvironmentCheckpointProtector(
            baseline,
            openKeys:
            [
                new KeyValuePair<string, ReadOnlyMemory<byte>>("acme-2026-08", first.OpenKey),
                new KeyValuePair<string, ReadOnlyMemory<byte>>("acme-2026-09", second.OpenKey),
            ]);
        (await runner.UnprotectAsync(beforeRotation, Run, default)).ToArray().ShouldBe(Plaintext);
        (await runner.UnprotectAsync(afterRotation, Run, default)).ToArray().ShouldBe(Plaintext);
    }

    [TestMethod]
    public async Task The_protected_store_seals_by_the_runs_environment_end_to_end()
    {
        // The wrapper detects the environment-aware capability exactly as it detects store capabilities: the save
        // routes by the index projection's environment, so a sealed environment's runs seal end to end — and the
        // whole ADR 0065 matrix holds over one set of persisted bytes.
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using var baseline = new AesGcmCheckpointProtector(new byte[32]);
        var inner = new InMemoryWorkflowStateStore();
        DateTimeOffset now = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        WorkflowRunIndexEntry index = new("wf@1", WorkflowRunStatus.Running, now, now, Environment: "acme-prod");

        // The control-plane posture writes through the wrapper: the save routes by the index's environment.
        using var sealer = new EnvironmentCheckpointProtector(
            baseline,
            (environment, _) => new ValueTask<EnvironmentSealKey?>(
                environment == "acme-prod" ? new EnvironmentSealKey("acme-1", pair.SealKey) : null));
        var controlPlaneStore = new ProtectedWorkflowStateStore(inner, sealer);
        await controlPlaneStore.SaveAsync(Run, Plaintext, in index, WorkflowEtag.None, default);

        // The stored blob is a sealed envelope naming the environment's key — and the writer cannot read it back.
        WorkflowCheckpoint? raw = await inner.LoadAsync(Run, default);
        raw.ShouldNotBeNull();
        SealedCheckpointProtector.TryReadSealedKeyId(raw.Value.Utf8.Span, out string keyId).ShouldBeTrue();
        keyId.ShouldBe("acme-1");
        await Should.ThrowAsync<CryptographicException>(async () => await controlPlaneStore.LoadAsync(Run, default));

        // The runner's posture over the SAME persisted bytes opens it.
        using var runner = new EnvironmentCheckpointProtector(
            baseline,
            openKeys: [new KeyValuePair<string, ReadOnlyMemory<byte>>("acme-1", pair.OpenKey)]);
        var runnerStore = new ProtectedWorkflowStateStore(inner, runner);
        WorkflowCheckpoint? opened = await runnerStore.LoadAsync(Run, default);
        opened.ShouldNotBeNull();
        opened.Value.Utf8.ToArray().ShouldBe(Plaintext);
    }
}