// <copyright file="RunnerExecutorPolicyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

[TestClass]
public sealed class RunnerExecutorPolicyTests
{
    private const string Production = "production";
    private const string Digest = "sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [TestMethod]
    public void A_policy_parses_pinned_signers_and_listed_digests_and_refuses_anything_else()
    {
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string signer = Convert.ToBase64String(tenant.ExportSubjectPublicKeyInfo());

        RunnerExecutorPolicy policy = RunnerExecutorPolicy.Parse(Production, [signer], [Digest])!;
        policy.Signers.Count.ShouldBe(1);
        policy.Lists(Digest).ShouldBeTrue();
        policy.Lists("sha256:" + new string('f', 64)).ShouldBeFalse();
        policy.IsEmpty.ShouldBeFalse();

        // Neither configured is no policy, not an empty one.
        RunnerExecutorPolicy.Parse(Production, null, null).ShouldBeNull();
        RunnerExecutorPolicy.Parse(Production, [], []).ShouldBeNull();

        // A signer that is not a P-256 public key, or not base64, stops the runner; so does a digest outside the
        // sha256:<64 lowercase hex> grammar, uppercase included, since a digest that never matches is a policy that
        // admits nothing and says nothing.
        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Should.Throw<InvalidOperationException>(() => RunnerExecutorPolicy.Parse(Production, [Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo())], null));
        Should.Throw<InvalidOperationException>(() => RunnerExecutorPolicy.Parse(Production, ["not base64!"], null));
        Should.Throw<InvalidOperationException>(() => RunnerExecutorPolicy.Parse(Production, null, ["sha256:" + new string('A', 64)]));
        Should.Throw<InvalidOperationException>(() => RunnerExecutorPolicy.Parse(Production, null, ["sha1:" + new string('a', 64)]));
        Should.Throw<InvalidOperationException>(() => RunnerExecutorPolicy.Parse(Production, null, ["sha256:" + new string('a', 63)]));
    }

    [TestMethod]
    public async Task A_ring_carries_the_policy_per_environment_on_clear_and_keyed_entries_alike()
    {
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string signer = Convert.ToBase64String(tenant.ExportSubjectPublicKeyInfo());
        var entries = new List<RunnerKeyRingEntry>
        {
            new("development", ExecutorSigners: [signer]),
            new("staging", Executors: [Digest]),
            RunnerKeyRingEntry.Clear("sandbox"),
        };

        RunnerKeyRing ring = await RunnerKeyRing.BuildAsync(entries, secrets: null, default);
        ring.Admits("development").ShouldBeTrue();
        ring.ExecutorPolicyOf("development")!.Signers.Count.ShouldBe(1);
        ring.ExecutorPolicyOf("staging")!.Lists(Digest).ShouldBeTrue();
        ring.ExecutorPolicyOf("sandbox").ShouldBeNull("a clear entry without a policy runs what the loader verified");
        ring.ExecutorPolicyOf(Production).ShouldBeNull("an environment not on the ring has no policy either");

        // A ring built from keys a host holds itself takes the policies beside them.
        byte[] payloadKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        byte[] mac = new byte[32];
        CheckpointDerivation.DeriveSubkey(payloadKey, CheckpointSubkey.EnvelopeMac, Production, "k1", mac);
        RunnerKeyRing held = RunnerKeyRing.From(
            new Dictionary<string, RunnerEnvironmentKeys> { [Production] = new("k1", payloadKey, mac, Sealed: true) },
            new Dictionary<string, RunnerExecutorPolicy> { [Production] = RunnerExecutorPolicy.Listing(Digest) },
            "development");
        held.ExecutorPolicyOf(Production)!.Lists(Digest).ShouldBeTrue();
        held.Admits("development").ShouldBeTrue();
        RunnerKeyRing.Empty.ExecutorPolicyOf(Production).ShouldBeNull();
    }
}