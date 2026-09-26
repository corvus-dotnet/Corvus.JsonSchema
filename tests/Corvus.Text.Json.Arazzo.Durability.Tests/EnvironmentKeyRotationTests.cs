// <copyright file="EnvironmentKeyRotationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The predecessor-signed rotation link and the chain a pinned party walks along it (ADR 0065 decision 12): what a
/// link proves, the ways one is refused, and which generations reach a pinned fingerprint.
/// </summary>
[TestClass]
public sealed class EnvironmentKeyRotationTests
{
    private const string Production = "production";

    [TestMethod]
    public void A_successor_signed_by_the_predecessors_private_half_verifies_and_nothing_else_does()
    {
        using ECDsa first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa second = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] firstSpki = first.ExportSubjectPublicKeyInfo();
        byte[] secondSpki = second.ExportSubjectPublicKeyInfo();
        byte[] link = EnvironmentKeyRotation.Sign(first, Production, "k1", "k2", secondSpki);

        EnvironmentKeyRotation.Verify(Production, "k1", firstSpki, "k2", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.Verified);

        // The successor's own key cannot hand over to itself, a stranger's cannot hand over at all, and every field
        // of the tuple is inside the signature: another environment, another predecessor id, another successor id,
        // another successor key.
        EnvironmentKeyRotation.Verify(Production, "k1", firstSpki, "k2", secondSpki, EnvironmentKeyRotation.Sign(second, Production, "k1", "k2", secondSpki)).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);
        EnvironmentKeyRotation.Verify(Production, "k1", firstSpki, "k2", secondSpki, EnvironmentKeyRotation.Sign(stranger, Production, "k1", "k2", secondSpki)).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);
        EnvironmentKeyRotation.Verify("staging", "k1", firstSpki, "k2", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);
        EnvironmentKeyRotation.Verify(Production, "k0", firstSpki, "k2", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);
        EnvironmentKeyRotation.Verify(Production, "k1", firstSpki, "k3", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);
        EnvironmentKeyRotation.Verify(Production, "k1", firstSpki, "k2", stranger.ExportSubjectPublicKeyInfo(), link).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);

        // A predecessor key that is not a P-256 key is refused rather than thrown, and an oversized identifier is
        // refused rather than overflowing the tuple's stack allocation.
        EnvironmentKeyRotation.Verify(Production, "k1", new byte[] { 1, 2, 3 }, "k2", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.PredecessorKeyUnreadable);
        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        EnvironmentKeyRotation.Verify(Production, "k1", p384.ExportSubjectPublicKeyInfo(), "k2", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.PredecessorKeyUnreadable);
        EnvironmentKeyRotation.Verify(Production, new string('k', 257), firstSpki, "k2", secondSpki, link).ShouldBe(EnvironmentKeyRotationResult.IdentifierTooLong);
    }

    [TestMethod]
    public void The_tuple_frames_every_field_so_a_different_split_does_not_verify()
    {
        using ECDsa first = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki = new byte[] { 9, 9, 9 };
        byte[] link = EnvironmentKeyRotation.Sign(first, "prod", "ab", "cd", spki);

        // ("prod", "ab", "cd") and ("prod", "a", "bcd") concatenate the same way; framing keeps them apart.
        EnvironmentKeyRotation.Verify("prod", "a", first.ExportSubjectPublicKeyInfo(), "bcd", spki, link).ShouldBe(EnvironmentKeyRotationResult.SignatureInvalid);
        byte[] tuple = new byte[EnvironmentKeyRotation.MaxTupleLength("prod", "ab", "cd", spki.Length)];
        int written = EnvironmentKeyRotation.WriteSignedTuple(tuple, "prod", "ab", "cd", spki);
        written.ShouldBeLessThanOrEqualTo(tuple.Length);
        System.Text.Encoding.UTF8.GetString(tuple, 4, "environment-key-rotation".Length).ShouldBe("environment-key-rotation");
    }

    [TestMethod]
    public void A_generation_reaches_the_pin_along_verified_links_and_along_nothing_else()
    {
        using ECDsa k1 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa k2 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa k3 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] spki1 = k1.ExportSubjectPublicKeyInfo();
        byte[] spki2 = k2.ExportSubjectPublicKeyInfo();
        byte[] spki3 = k3.ExportSubjectPublicKeyInfo();
        string pin = RunStartInitiator.SealKeyFingerprint(spki1);

        // k1 (retired) handed over to k2, k2 to k3: both successors reach the pin, k3 two links away.
        AdvertisedKeyGeneration[] chain =
        [
            new("k1", spki1, Active: false),
            new("k2", spki2, Active: true, "k1", EnvironmentKeyRotation.Sign(k1, Production, "k1", "k2", spki2)),
            new("k3", spki3, Active: true, "k2", EnvironmentKeyRotation.Sign(k2, Production, "k2", "k3", spki3)),
        ];
        EnvironmentKeyChain.Distance(Production, pin, "k1", chain).ShouldBe(0, "the pinned key itself");
        EnvironmentKeyChain.Distance(Production, pin, "k2", chain).ShouldBe(1);
        EnvironmentKeyChain.Distance(Production, pin, "k3", chain).ShouldBe(2);
        EnvironmentKeyChain.Reaches(Production, pin, "k3", chain).ShouldBeTrue();
        EnvironmentKeyChain.Reaches(Production, pin, "k4", chain).ShouldBeFalse("not advertised at all");
        EnvironmentKeyChain.Reaches("staging", pin, "k2", chain).ShouldBeFalse("the links name the environment");

        // A successor with no link, a link a stranger signed, a link naming a predecessor the environment does not
        // advertise, and a link pointing at itself, none reach; a broken link breaks everything after it.
        EnvironmentKeyChain.Reaches(Production, pin, "k2", [new("k1", spki1, false), new("k2", spki2, true)]).ShouldBeFalse("no link");
        EnvironmentKeyChain.Reaches(Production, pin, "k2", [new("k1", spki1, false), new("k2", spki2, true, "k1", EnvironmentKeyRotation.Sign(stranger, Production, "k1", "k2", spki2))]).ShouldBeFalse("a stranger's link");
        EnvironmentKeyChain.Reaches(Production, pin, "k2", [new("k2", spki2, true, "k1", EnvironmentKeyRotation.Sign(k1, Production, "k1", "k2", spki2))]).ShouldBeFalse("the predecessor is not advertised, so its key cannot be checked");
        EnvironmentKeyChain.Reaches(Production, pin, "k2", [new("k1", spki1, false), new("k2", spki2, true, "k2", EnvironmentKeyRotation.Sign(k2, Production, "k2", "k2", spki2))]).ShouldBeFalse("self-reference");
        AdvertisedKeyGeneration[] broken =
        [
            new("k1", spki1, false),
            new("k2", spki2, true, "k1", EnvironmentKeyRotation.Sign(stranger, Production, "k1", "k2", spki2)),
            new("k3", spki3, true, "k2", EnvironmentKeyRotation.Sign(k2, Production, "k2", "k3", spki3)),
        ];
        EnvironmentKeyChain.Reaches(Production, pin, "k3", broken).ShouldBeFalse("k3's own link is fine, k2's is not, and the pin is behind k2");

        // A cycle among generations that never reach the pin terminates: the walk is bounded.
        AdvertisedKeyGeneration[] cycle =
        [
            new("a", spki2, true, "b", EnvironmentKeyRotation.Sign(k3, Production, "b", "a", spki2)),
            new("b", spki3, true, "a", EnvironmentKeyRotation.Sign(k2, Production, "a", "b", spki3)),
        ];
        EnvironmentKeyChain.Reaches(Production, pin, "a", cycle).ShouldBeFalse();
    }
}