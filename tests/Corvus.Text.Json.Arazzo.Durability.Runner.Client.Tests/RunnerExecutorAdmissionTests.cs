// <copyright file="RunnerExecutorAdmissionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

[TestClass]
public sealed class RunnerExecutorAdmissionTests
{
    private const string Run1 = "0123456789abcdef0123456789abcdef";
    private const string KeyId = "k2";
    private const string Base = "flow";
    private const string Hash = "sha256:" + "1111111111111111111111111111111111111111111111111111111111111111";
    private const string Digest = "sha256:" + "2222222222222222222222222222222222222222222222222222222222222222";
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 6)).ToArray();

    [TestMethod]
    public async Task An_executor_is_admitted_by_a_listed_digest_or_a_verified_countersignature_and_by_nothing_else()
    {
        // ADR 0065 phase C: the runner's policy for production pins the tenant's executor-signing key. The control
        // plane advertises the tenant's countersignature for version 1 of the flow; the runner verifies it under the
        // pin, over the manifest of the executor it loaded, and admits that executor and no other.
        using var tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] tenantSpki = tenant.ExportSubjectPublicKeyInfo();
        string countersigned = Convert.ToBase64String(ExecutorCountersignature.Sign(tenant, Fixture.Production, Base, 1, Hash, Digest));
        string forged = Convert.ToBase64String(ExecutorCountersignature.Sign(stranger, Fixture.Production, Base, 1, Hash, Digest));

        await using Fixture fixture = await Fixture.StartAsync(
            keyRing: Ring(RunnerExecutorPolicy.Signed(tenantSpki)),
            anchors: await AnchorsAsync(),
            executorCountersignatures: new Dictionary<string, IReadOnlyList<RunnerExecutorCountersignature>>
            {
                [Fixture.Production] =
                [
                    new RunnerExecutorCountersignature(Base, 1, Hash, Digest, countersigned),
                    new RunnerExecutorCountersignature(Base, 2, Hash, Digest, forged),
                ],
            });
        await fixture.SeedCatalogAsync(Base, Fixture.Production);
        await fixture.SeedCatalogAsync(Base, Fixture.Production);
        await fixture.SeedCatalogAsync(Base, Fixture.Production);
        RunnerExecutorAdmission admission = fixture.Client.ExecutorAdmission;

        (await admission.AdmitAsync(Fixture.Production, Base, 1, Manifest(Hash, Digest), default)).ShouldBe(ExecutorAdmission.Admitted);

        // The countersignature names the executor by its package hash and assembly digest: a repacked or recompiled
        // executor of the same version, whose manifest says otherwise, is not the one the tenant countersigned.
        (await admission.AdmitAsync(Fixture.Production, Base, 1, Manifest(Hash, "sha256:" + new string('3', 64)), default)).ShouldBe(ExecutorAdmission.CountersignatureInvalid);
        (await admission.AdmitAsync(Fixture.Production, Base, 1, Manifest("sha256:" + new string('4', 64), Digest), default)).ShouldBe(ExecutorAdmission.CountersignatureInvalid);

        // A countersignature a stranger made, a version the tenant never countersigned, and an environment the runner
        // is not bound to (the runner API answers 404 for all three cases it cannot tell apart).
        (await admission.AdmitAsync(Fixture.Production, Base, 2, Manifest(Hash, Digest), default)).ShouldBe(ExecutorAdmission.CountersignatureInvalid);
        (await admission.AdmitAsync(Fixture.Production, Base, 3, Manifest(Hash, Digest), default)).ShouldBe(ExecutorAdmission.NotCountersigned);

        // A clear environment with no policy runs what the loader verified; one whose policy lists digests only
        // admits a listed digest and refuses any other without a round trip.
        (await fixture.StrangerClient.ExecutorAdmission.AdmitAsync(Fixture.Production, Base, 3, Manifest(Hash, Digest), default)).ShouldBe(ExecutorAdmission.Admitted, "the stranger names production clear with no policy");
        var listing = new RunnerExecutorAdmission(RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys>(), new Dictionary<string, RunnerExecutorPolicy> { [Fixture.Production] = RunnerExecutorPolicy.Listing(Digest) }, Fixture.Production), fixture.Client.EnvironmentsClient);
        (await listing.AdmitAsync(Fixture.Production, Base, 3, Manifest(Hash, Digest), default)).ShouldBe(ExecutorAdmission.Admitted);
        (await listing.AdmitAsync(Fixture.Production, Base, 3, Manifest(Hash, "sha256:" + new string('5', 64)), default)).ShouldBe(ExecutorAdmission.NotListed);
    }

    [TestMethod]
    public async Task A_claim_whose_executor_is_not_admitted_is_handed_back_unended_and_a_keyed_ring_without_a_policy_does_not_start()
    {
        await using Fixture fixture = await Fixture.StartAsync(keyRing: Ring(RunnerExecutorPolicy.Listing(Digest)), anchors: await AnchorsAsync());
        await fixture.SeedGenesisWaitingAsync(Run1, WorkflowWait.Timer(Fixture.T0));
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        // The resumer stands in for the resolver's refusal: the run is handed back with its lease, not faulted, and the
        // sweep goes on.
        int refused = await new RunnerApiWorker(fixture.Client).ResumeDueTimersAsync(
            [Fixture.Version],
            (run, _) => throw new ExecutorNotAdmittedException(run.Environment, run.WorkflowId, ExecutorAdmission.NotCountersigned, "not yet"),
            default);
        refused.ShouldBe(0);
        (await fixture.Store.AcquireLeaseAsync(Address(Run1), "someone-else", TimeSpan.FromMinutes(1), default)).ShouldNotBeNull("the lease was given back");
        WorkflowCheckpoint? row = await fixture.Store.LoadAsync(Address(Run1), default);
        WorkflowCheckpointSerializer.Deserialize(row!.Value.Row).Status.ShouldBe(WorkflowRunStatus.Suspended, "the run is as it was, waiting for the operator's countersignature");

        // A keyed environment whose executor the platform alone would choose is refused at construction.
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, envelopeMac);
        RunnerKeyRing unpoliced = RunnerKeyRing.From(new Dictionary<string, RunnerEnvironmentKeys> { [Fixture.Production] = new(KeyId, PayloadKey, envelopeMac, Sealed: true) });
        using var http = new HttpClient { BaseAddress = new Uri("http://localhost/") };
        InvalidOperationException refusal = Should.Throw<InvalidOperationException>(() => new ArazzoRunnerClient(new Corvus.Text.Json.OpenApi.HttpTransport.HttpClientTransport(http), keyRing: unpoliced, anchors: new InMemoryTenantAnchorStore()));
        refusal.Message.ShouldContain("executor policy");
    }

    private static WorkflowExecutorManifest Manifest(string packageHash, string assemblyDigest)
        => new(2, "net10.0", packageHash, assemblyDigest, "Flow.Executor", $"{Base}-v1", true, null, []);

    private static RunnerKeyRing Ring(RunnerExecutorPolicy policy)
    {
        byte[] envelopeMac = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, Fixture.Production, KeyId, envelopeMac);
        return RunnerKeyRing.From(
            new Dictionary<string, RunnerEnvironmentKeys> { [Fixture.Production] = new(KeyId, PayloadKey, envelopeMac, Sealed: true) },
            new Dictionary<string, RunnerExecutorPolicy> { [Fixture.Production] = policy });
    }

    private static async Task<InMemoryTenantAnchorStore> AnchorsAsync()
    {
        var anchors = new InMemoryTenantAnchorStore();
        await anchors.AttestIncarnationAsync(Fixture.Production, 1, default);
        return anchors;
    }

    private static WorkflowRunAddress Address(string runId) => new(Fixture.Production, new WorkflowRunId(runId));
}