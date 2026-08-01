// <copyright file="EnvironmentKeyPossessionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The proof of possession on an environment key registration (ADR 0065): what it establishes, and the ways a
/// registration is refused.
/// </summary>
[TestClass]
public sealed class EnvironmentKeyPossessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_registrant_holding_the_private_half_is_verified()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();

        EnvironmentKeyPossession.Verify(
            "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, Sign(key, "acme-prod", "k1", spki, Now), Now)
            .ShouldBe(EnvironmentKeyPossessionResult.Verified);
    }

    [TestMethod]
    public void A_registration_signed_for_another_environment_is_refused()
    {
        // This is the property that stops a captured registration being replayed against a different environment,
        // which matters because the environment is what the tenancy invariant counts.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        byte[] signedForStaging = Sign(key, "acme-staging", "k1", spki, Now);

        EnvironmentKeyPossession.Verify(
            "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, signedForStaging, Now)
            .ShouldBe(EnvironmentKeyPossessionResult.SignatureInvalid);
    }

    [TestMethod]
    public void Field_framing_stops_a_different_split_verifying()
    {
        // ("e1","0abc") and ("e10","abc") concatenate identically, so without length framing a registration signed
        // for one environment and key id would verify for the other. Assert the pair really does collide first, or
        // this test passes for any two distinct inputs and proves nothing about framing.
        string.Concat("e1", "0abc").ShouldBe(string.Concat("e10", "abc"));

        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        byte[] signature = Sign(key, "e1", "0abc", spki, Now);

        EnvironmentKeyPossession.Verify("e10", "abc", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, signature, Now)
            .ShouldBe(EnvironmentKeyPossessionResult.SignatureInvalid);
    }

    [TestMethod]
    public void A_signature_from_a_different_key_is_refused()
    {
        // The registrant must hold the private half of the key it presents. Presenting someone else's public key
        // with a signature of one's own is the exact move the proof exists to stop.
        using ECDsa presented = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = presented.ExportSubjectPublicKeyInfo();

        EnvironmentKeyPossession.Verify(
            "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, Sign(other, "acme-prod", "k1", spki, Now), Now)
            .ShouldBe(EnvironmentKeyPossessionResult.SignatureInvalid);
    }

    [TestMethod]
    public void A_stale_or_post_dated_signing_instant_is_refused()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();

        DateTimeOffset stale = Now - TimeSpan.FromHours(1);
        EnvironmentKeyPossession.Verify(
            "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, stale, Sign(key, "acme-prod", "k1", spki, stale), Now)
            .ShouldBe(EnvironmentKeyPossessionResult.NotFresh);

        DateTimeOffset future = Now + TimeSpan.FromHours(1);
        EnvironmentKeyPossession.Verify(
            "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, future, Sign(key, "acme-prod", "k1", spki, future), Now)
            .ShouldBe(EnvironmentKeyPossessionResult.NotFresh);
    }

    [TestMethod]
    public void Ordinary_clock_skew_is_absorbed()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();

        DateTimeOffset slightlyAhead = Now + TimeSpan.FromSeconds(30);
        EnvironmentKeyPossession.Verify(
            "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, slightlyAhead, Sign(key, "acme-prod", "k1", spki, slightlyAhead), Now)
            .ShouldBe(EnvironmentKeyPossessionResult.Verified);
    }

    [TestMethod]
    public void An_unsupported_algorithm_is_refused_before_anything_else()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();

        EnvironmentKeyPossession.Verify("acme-prod", "k1", "ES512", spki, Now, Sign(key, "acme-prod", "k1", spki, Now), Now)
            .ShouldBe(EnvironmentKeyPossessionResult.AlgorithmUnsupported);
    }

    [TestMethod]
    public void A_key_that_is_not_a_p256_spki_is_refused_rather_than_throwing()
    {
        // A registration arrives from outside the trust boundary, so malformed key material must be a refusal and
        // never an unhandled CryptographicException surfacing as a 500.
        EnvironmentKeyPossession.Verify("acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, [1, 2, 3], Now, [4, 5, 6], Now)
            .ShouldBe(EnvironmentKeyPossessionResult.KeyUnreadable);

        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        byte[] spki = p384.ExportSubjectPublicKeyInfo();
        EnvironmentKeyPossession.Verify("acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, new byte[64], Now)
            .ShouldBe(EnvironmentKeyPossessionResult.KeyUnreadable);
    }

    [TestMethod]
    public void An_oversized_identifier_is_refused_rather_than_overflowing_the_stack()
    {
        // The signed tuple is stack-allocated from these lengths, and both reach here from outside.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();

        EnvironmentKeyPossession.Verify(
            new string('e', 100_000), "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, new byte[64], Now)
            .ShouldBe(EnvironmentKeyPossessionResult.IdentifierTooLong);

        EnvironmentKeyPossession.Verify(
            "acme-prod", new string('k', 100_000), EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, new byte[64], Now)
            .ShouldBe(EnvironmentKeyPossessionResult.IdentifierTooLong);
    }

    [TestMethod]
    public void A_replayed_registration_verifies_again_and_names_the_same_generation()
    {
        // Replay is deliberately not an error. The signed tuple fully determines the effect, so re-presenting it
        // re-registers the identical generation, which is what removes the need for a server-side nonce store.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = key.ExportSubjectPublicKeyInfo();
        byte[] signature = Sign(key, "acme-prod", "k1", spki, Now);

        for (int i = 0; i < 3; i++)
        {
            EnvironmentKeyPossession.Verify(
                "acme-prod", "k1", EnvironmentKeyPossession.EcdsaP256Sha256, spki, Now, signature, Now + TimeSpan.FromMinutes(i))
                .ShouldBe(EnvironmentKeyPossessionResult.Verified);
        }
    }

    private static byte[] Sign(ECDsa key, string environment, string keyId, byte[] spki, DateTimeOffset notBefore)
    {
        byte[] tuple = new byte[EnvironmentKeyPossession.MaxTupleLength(environment, keyId, spki.Length)];
        int written = EnvironmentKeyPossession.WriteSignedTuple(tuple, environment, keyId, spki, notBefore);
        return key.SignData(tuple.AsSpan(0, written), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
}