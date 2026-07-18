// <copyright file="VaultTransitExecutorPackageSignerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Vault.Tests;

/// <summary>
/// Tests the pure signature-decoding of <see cref="VaultTransitExecutorPackageSigner"/> — stripping Vault's
/// <c>vault:v&lt;n&gt;:</c> envelope and decoding the payload — without a live Vault. The Transit round-trip itself needs
/// a running Vault (exercised by the demo's live composition); this pins the format handling the signer depends on.
/// </summary>
[TestClass]
public sealed class VaultTransitExecutorPackageSignerTests
{
    [TestMethod]
    public void Decodes_a_jws_marshaled_ecdsa_signature_to_verifiable_p1363()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] data = "the executor manifest"u8.ToArray();

        // With marshaling_algorithm=jws, Vault returns the fixed-size IEEE P1363 r||s, base64url-encoded, wrapped as
        // vault:v<n>:. Reproduce that envelope over a real P1363 signature and prove the decode yields the verifiable bytes.
        byte[] p1363 = key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string vaultSignature = "vault:v1:" + Base64Url(p1363);

        byte[] decoded = VaultTransitExecutorPackageSigner.DecodeVaultSignature(vaultSignature, base64Url: true);

        decoded.ShouldBe(p1363);
        key.VerifyData(data, decoded, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
    }

    [TestMethod]
    public void Decodes_a_standard_base64_rsa_signature()
    {
        // The RSA-PSS path is standard base64 of the raw signature (no marshaling), wrapped as vault:v<n>:.
        byte[] raw = [0x00, 0x01, 0xFF, 0xFE, 0x10, 0x7F, 0x80, 0xAB];
        string vaultSignature = "vault:v2:" + Convert.ToBase64String(raw);

        VaultTransitExecutorPackageSigner.DecodeVaultSignature(vaultSignature, base64Url: false).ShouldBe(raw);
    }

    [TestMethod]
    public void An_empty_vault_signature_is_rejected()
    {
        Should.Throw<InvalidOperationException>(() => VaultTransitExecutorPackageSigner.DecodeVaultSignature(string.Empty, base64Url: true));
    }

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}