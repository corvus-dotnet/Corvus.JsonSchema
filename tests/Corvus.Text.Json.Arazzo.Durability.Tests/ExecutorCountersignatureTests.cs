// <copyright file="ExecutorCountersignatureTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

[TestClass]
public sealed class ExecutorCountersignatureTests
{
    private const string Production = "production";
    private const string Hash = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    private const string Digest = "sha256:2222222222222222222222222222222222222222222222222222222222222222";

    [TestMethod]
    public void A_countersignature_by_the_tenants_key_verifies_and_nothing_else_does()
    {
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] tenantSpki = tenant.ExportSubjectPublicKeyInfo();
        byte[] signature = ExecutorCountersignature.Sign(tenant, Production, "onboard", 3, Hash, Digest);

        ExecutorCountersignature.Verify(Production, "onboard", 3, Hash, Digest, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.Verified);

        // A stranger's signature, and every field of the tuple: another environment, another workflow, another
        // version, another package hash, another assembly digest. The environment is what keeps a staging
        // countersignature out of production; the hash and the digest are what keep a repack and a recompile out.
        ExecutorCountersignature.Verify(Production, "onboard", 3, Hash, Digest, tenantSpki, ExecutorCountersignature.Sign(stranger, Production, "onboard", 3, Hash, Digest)).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);
        ExecutorCountersignature.Verify("staging", "onboard", 3, Hash, Digest, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);
        ExecutorCountersignature.Verify(Production, "onboard-async", 3, Hash, Digest, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);
        ExecutorCountersignature.Verify(Production, "onboard", 4, Hash, Digest, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);
        ExecutorCountersignature.Verify(Production, "onboard", 3, Digest, Digest, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);
        ExecutorCountersignature.Verify(Production, "onboard", 3, Hash, Hash, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);

        // A signer key that is not a P-256 key is refused rather than thrown, and an oversized identifier is refused
        // rather than overflowing the tuple's stack allocation.
        ExecutorCountersignature.Verify(Production, "onboard", 3, Hash, Digest, new byte[] { 1, 2, 3 }, signature).ShouldBe(ExecutorCountersignatureResult.SignerKeyUnreadable);
        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        ExecutorCountersignature.Verify(Production, "onboard", 3, Hash, Digest, p384.ExportSubjectPublicKeyInfo(), signature).ShouldBe(ExecutorCountersignatureResult.SignerKeyUnreadable);
        ExecutorCountersignature.Verify(Production, new string('w', 257), 3, Hash, Digest, tenantSpki, signature).ShouldBe(ExecutorCountersignatureResult.IdentifierTooLong);
    }

    [TestMethod]
    public void The_tuple_frames_every_field_so_a_different_split_does_not_verify()
    {
        using ECDsa tenant = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] signature = ExecutorCountersignature.Sign(tenant, "prod", "ab", 1, "cd", "ef");

        // ("ab", "cd") and ("a", "bcd") concatenate the same way; framing keeps them apart.
        ExecutorCountersignature.Verify("prod", "a", 1, "bcd", "ef", tenant.ExportSubjectPublicKeyInfo(), signature).ShouldBe(ExecutorCountersignatureResult.SignatureInvalid);
        ExecutorCountersignature.Verify("prod", "ab", 1, "cd", "ef", tenant.ExportSubjectPublicKeyInfo(), signature).ShouldBe(ExecutorCountersignatureResult.Verified);
    }
}