// <copyright file="RunnerAllowlistTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// The runner's allowlist over the real runner API (ADR 0065 decision 10): a runner serves the environments it names
/// and no other, whatever the control plane binds it to; a keyed environment is served only while the seal key the
/// control plane advertises is the one the tenant pinned; and a runner nobody configured serves nothing.
/// </summary>
[TestClass]
public sealed class RunnerAllowlistTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string KeyId = "k2";
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 6)).ToArray();

    [TestMethod]
    public async Task A_claim_for_an_environment_the_runner_does_not_name_is_handed_back_and_a_runner_with_no_allowlist_serves_nothing()
    {
        // The control plane binds the principal to production and offers it a production run; the runner's own list
        // names staging only, so the claim is handed back with the lease, and the sweep goes on.
        await using Fixture fixture = await Fixture.StartAsync(keyRing: RunnerKeyRing.Admitting("staging"));
        await fixture.SeedAsync(Run1, WorkflowRunStatus.Pending);

        var dispatcher = new RunnerApiDispatcher(fixture.Client);
        (await dispatcher.DispatchClaimableAsync([Fixture.Version], (_, _) => throw new InvalidOperationException("an environment the runner does not name is never run"), default)).ShouldBe(0);
        (await fixture.Store.AcquireLeaseAsync(Address(Run1), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the lease was given back");
        (await fixture.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.NotAllowlisted);
        (await fixture.Client.AdmitsAsync("staging")).ShouldBe(RunnerAdmission.Admitted, "a clear entry is admitted with no check");

        // A runner built with no allowlist at all admits nothing: that is the fail-closed reading of "not configured".
        (await fixture.StrangerClient.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.Admitted, "the fixture's stranger names production");
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        var unconfigured = new ArazzoRunnerClient(new Corvus.Text.Json.OpenApi.HttpTransport.HttpClientTransport(http));
        unconfigured.Allowlist.IsEmpty.ShouldBeTrue();
        (await unconfigured.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.NotAllowlisted);
    }

    [TestMethod]
    public async Task A_keyed_environment_is_served_only_while_the_advertised_seal_key_is_the_pinned_one()
    {
        using var sealKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string pinned = RunStartInitiator.SealKeyFingerprint(sealKey.ExportSubjectPublicKeyInfo());
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);

        // The control plane advertises the tenant's key for the generation the runner holds: admitted, and the
        // check is cached, so a sweep does not pay a round trip per claim.
        await using Fixture matching = await Fixture.StartAsync(
            keyRing: Ring(pinned),
            anchors: anchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) },
            sealKeys: Advertised(KeyId, sealKey.ExportSubjectPublicKeyInfo(), active: true));
        await matching.SeedGenesisWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0));
        matching.Clock.Advance(TimeSpan.FromMinutes(1));
        (await matching.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.Admitted);
        (await new RunnerApiWorker(matching.Client).ResumeDueTimersAsync([Fixture.Version], Park, default)).ShouldBe(1);

        // The control plane advertises a key of its own for the same generation: production is suspended, the run
        // is handed back, and nothing under that key is opened or sealed.
        var swappedAnchors = new InMemoryTenantAnchorStore();
        await swappedAnchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture swapped = await Fixture.StartAsync(
            keyRing: Ring(pinned),
            anchors: swappedAnchors,
            sealedGenerations: new Dictionary<string, IReadOnlySet<string>> { [Fixture.Production] = new HashSet<string>([KeyId]) },
            sealKeys: Advertised(KeyId, otherKey.ExportSubjectPublicKeyInfo(), active: true));
        await swapped.SeedGenesisWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0));
        swapped.Clock.Advance(TimeSpan.FromMinutes(1));
        (await swapped.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.SealKeyMismatch);
        (await new RunnerApiWorker(swapped.Client).ResumeDueTimersAsync([Fixture.Version], (_, _) => throw new InvalidOperationException("a suspended environment is never run"), default)).ShouldBe(0);
        (await swapped.Store.AcquireLeaseAsync(Address(Run1), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the lease was given back");

        // A generation the environment does not hold active, and a control plane that advertises nothing at all.
        var retiredAnchors = new InMemoryTenantAnchorStore();
        await retiredAnchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture retired = await Fixture.StartAsync(keyRing: Ring(pinned), anchors: retiredAnchors, sealKeys: Advertised(KeyId, sealKey.ExportSubjectPublicKeyInfo(), active: false));
        (await retired.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.GenerationNotActive);
        var silentAnchors = new InMemoryTenantAnchorStore();
        await silentAnchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        await using Fixture silent = await Fixture.StartAsync(keyRing: Ring(pinned), anchors: silentAnchors);
        (await silent.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.GenerationNotActive, "nothing advertised for the generation held");

        static async ValueTask<WorkflowRunResultKind> Park(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.CheckpointAsync(run.Cursor + 1, cancellationToken);
            await run.SuspendForTimerAsync(run.Cursor, TimeSpan.FromMinutes(5), cancellationToken);
            return WorkflowRunResultKind.Suspended;
        }
    }

    [TestMethod]
    public async Task A_runner_holding_a_successor_writes_under_it_once_the_control_plane_advertises_it_chained_to_the_pin()
    {
        // ADR 0065 decision 12: the runner holds k1 (pinned) and k3 (provisioned ahead). While only k1 is registered
        // it writes under k1; once k3 is registered active with k1's signature over the rotation, it writes under k2
        // without a restart; a k3 the outgoing key did not hand over to is never written under, and with k1 retired
        // such a runner is suspended.
        using var first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var second = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] firstSpki = first.ExportSubjectPublicKeyInfo();
        byte[] secondSpki = second.ExportSubjectPublicKeyInfo();
        string pinned = RunStartInitiator.SealKeyFingerprint(firstSpki);
        string link = Convert.ToBase64String(EnvironmentKeyRotation.Sign(first, Fixture.Production, KeyId, "k3", secondSpki));
        string strangerLink = Convert.ToBase64String(EnvironmentKeyRotation.Sign(stranger, Fixture.Production, KeyId, "k3", secondSpki));

        // Only k1 registered: k1 is written under.
        await using Fixture before = await Fixture.StartAsync(
            keyRing: TwoGenerationRing(pinned),
            anchors: await AnchorsAsync(),
            sealedGenerations: Generations(KeyId),
            sealKeys: new Dictionary<string, IReadOnlyList<RunnerSealKeyGeneration>> { [Fixture.Production] = [new RunnerSealKeyGeneration(KeyId, Convert.ToBase64String(firstSpki), true)] });
        (await before.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.Admitted);
        before.Client.Allowlist.WriteGenerationOf(Fixture.Production).ShouldBe(KeyId);
        await before.SeedGenesisWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0));
        before.Clock.Advance(TimeSpan.FromMinutes(1));
        (await new RunnerApiWorker(before.Client).ResumeDueTimersAsync([Fixture.Version], Park, default)).ShouldBe(1);
        CheckpointIntegrity.KeyIdOf((await before.Store.LoadAsync(Address(Run1), default))!.Value.Row.Span).ShouldBe(KeyId);

        // k3 registered active, chained from k1: the write generation advances to k3 and the next save is under it;
        // a message wait parked under k1 still wakes, since the delivery queries every held generation's index.
        await using Fixture after = await Fixture.StartAsync(
            keyRing: TwoGenerationRing(pinned),
            anchors: await AnchorsAsync(),
            sealedGenerations: Generations(KeyId, "k3"),
            sealKeys: new Dictionary<string, IReadOnlyList<RunnerSealKeyGeneration>>
            {
                [Fixture.Production] = [new RunnerSealKeyGeneration(KeyId, Convert.ToBase64String(firstSpki), true), new RunnerSealKeyGeneration("k3", Convert.ToBase64String(secondSpki), true, KeyId, link)],
            });
        (await after.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.Admitted);
        after.Client.Allowlist.WriteGenerationOf(Fixture.Production).ShouldBe("k3", "the newest held generation that chains to the pin");
        await after.SeedGenesisWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0));
        after.Clock.Advance(TimeSpan.FromMinutes(1));
        (await new RunnerApiWorker(after.Client).ResumeDueTimersAsync([Fixture.Version], Park, default)).ShouldBe(1);
        CheckpointIntegrity.KeyIdOf((await after.Store.LoadAsync(Address(Run1), default))!.Value.Row.Span).ShouldBe("k3");

        // k3 advertised under a link the outgoing key did not sign: k1 is still written under while it is active,
        // and once k1 is retired the runner is suspended rather than following the swap.
        await using Fixture forged = await Fixture.StartAsync(
            keyRing: TwoGenerationRing(pinned),
            anchors: await AnchorsAsync(),
            sealedGenerations: Generations(KeyId, "k3"),
            sealKeys: new Dictionary<string, IReadOnlyList<RunnerSealKeyGeneration>>
            {
                [Fixture.Production] = [new RunnerSealKeyGeneration(KeyId, Convert.ToBase64String(firstSpki), true), new RunnerSealKeyGeneration("k3", Convert.ToBase64String(secondSpki), true, KeyId, strangerLink)],
            });
        (await forged.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.Admitted);
        forged.Client.Allowlist.WriteGenerationOf(Fixture.Production).ShouldBe(KeyId, "the forged successor is not followed");
        await using Fixture forgedAndRetired = await Fixture.StartAsync(
            keyRing: TwoGenerationRing(pinned),
            anchors: await AnchorsAsync(),
            sealedGenerations: Generations("k3"),
            sealKeys: new Dictionary<string, IReadOnlyList<RunnerSealKeyGeneration>>
            {
                [Fixture.Production] = [new RunnerSealKeyGeneration(KeyId, Convert.ToBase64String(firstSpki), false), new RunnerSealKeyGeneration("k3", Convert.ToBase64String(secondSpki), true, KeyId, strangerLink)],
            });
        (await forgedAndRetired.Client.AdmitsAsync(Fixture.Production)).ShouldBe(RunnerAdmission.SealKeyMismatch);

        static async ValueTask<WorkflowRunResultKind> Park(WorkflowRun run, CancellationToken cancellationToken)
        {
            await run.CheckpointAsync(run.Cursor + 1, cancellationToken);
            await run.SuspendForTimerAsync(run.Cursor, TimeSpan.FromMinutes(5), cancellationToken);
            return WorkflowRunResultKind.Suspended;
        }

        static async Task<InMemoryTenantAnchorStore> AnchorsAsync()
        {
            var anchors = new InMemoryTenantAnchorStore();
            await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
            return anchors;
        }

        static Dictionary<string, IReadOnlySet<string>> Generations(params string[] active)
            => new() { [Fixture.Production] = new HashSet<string>(active) };
    }

    [TestMethod]
    public void A_keyed_entry_needs_a_pinned_fingerprint_and_the_minimum_generation_is_the_one_held()
    {
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        var transport = new Corvus.Text.Json.OpenApi.HttpTransport.HttpClientTransport(http);
        Should.Throw<InvalidOperationException>(() => new ArazzoRunnerClient(transport, keyRing: Ring("pinned"))).Message.ShouldContain("anchor", customMessage: "a sealed entry still needs the anchor");
        new ArazzoRunnerClient(transport, keyRing: Ring("pinned"), anchors: new InMemoryTenantAnchorStore()).Allowlist.Admits(Fixture.Production).ShouldBeTrue();
    }

    private static WorkflowRunAddress Address(string runId) => new(Fixture.Production, new WorkflowRunId(runId));

    private static Dictionary<string, IReadOnlyList<RunnerSealKeyGeneration>> Advertised(string keyId, byte[] spki, bool active)
        => new() { [Fixture.Production] = [new RunnerSealKeyGeneration(keyId, Convert.ToBase64String(spki), active)] };

    private static readonly byte[] SecondPayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(77 + i)).ToArray();

    // k1 and k2 held, oldest first (decision 12), pinned on k1's seal key.
    private static RunnerKeyRing TwoGenerationRing(string pinnedFingerprint)
    {
        byte[] firstMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, firstMac);
        byte[] secondMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(SecondPayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, "k3", secondMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = RunnerEnvironmentKeys.Holding(
                [new RunnerGenerationKeys(KeyId, PayloadKey, firstMac), new RunnerGenerationKeys("k3", SecondPayloadKey, secondMac)],
                @sealed: true,
                sealKeyFingerprint: pinnedFingerprint),
        });
    }

    private static RunnerKeyRing Ring(string pinnedFingerprint)
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, envelopeMac);
        return RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>
        {
            [Fixture.Production] = new(KeyId, PayloadKey, envelopeMac, Sealed: true, SealKeyFingerprint: pinnedFingerprint),
        });
    }
}