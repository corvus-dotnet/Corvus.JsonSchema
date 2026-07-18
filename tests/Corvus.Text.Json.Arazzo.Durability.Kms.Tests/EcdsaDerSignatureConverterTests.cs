// <copyright file="EcdsaDerSignatureConverterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Kms.Tests;

/// <summary>
/// Tests that <see cref="EcdsaDerSignatureConverter"/> turns the ASN.1 DER ECDSA signature AWS KMS returns into the
/// fixed-size IEEE P1363 encoding the executor-package verifier checks. The oracle is a real ECDSA verify against the
/// P1363 output: if the r/s padding were wrong the verify would fail. No AWS dependency — the DER is produced locally.
/// </summary>
[TestClass]
public sealed class EcdsaDerSignatureConverterTests
{
    [TestMethod]
    public void Converts_P256_der_signatures_to_verifiable_fixed_size_p1363()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // ECDSA is randomised, so many iterations exercise both the DER leading-0x00 case (r or s high bit set) and the
        // short-component case (r or s shorter than the field), which the fixed-size left-padding must handle.
        for (int i = 0; i < 200; i++)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes($"executor manifest {i}");
            byte[] der = key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

            byte[] p1363 = EcdsaDerSignatureConverter.ToP1363(der, 32);

            p1363.Length.ShouldBe(64);
            key.VerifyData(data, p1363, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
        }
    }

    [TestMethod]
    public void Converts_P384_der_signatures_to_verifiable_fixed_size_p1363()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP384);

        for (int i = 0; i < 200; i++)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes($"executor manifest {i}");
            byte[] der = key.SignData(data, HashAlgorithmName.SHA384, DSASignatureFormat.Rfc3279DerSequence);

            byte[] p1363 = EcdsaDerSignatureConverter.ToP1363(der, 48);

            p1363.Length.ShouldBe(96);
            key.VerifyData(data, p1363, HashAlgorithmName.SHA384, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeTrue();
        }
    }

    [TestMethod]
    public void A_p1363_conversion_does_not_verify_against_the_wrong_message()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] der = key.SignData("the signed manifest"u8.ToArray(), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        byte[] p1363 = EcdsaDerSignatureConverter.ToP1363(der, 32);

        key.VerifyData("a tampered manifest"u8.ToArray(), p1363, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation).ShouldBeFalse();
    }

    [TestMethod]
    public void Rejects_a_signature_that_is_not_valid_der()
    {
        Should.Throw<CryptographicException>(() => EcdsaDerSignatureConverter.ToP1363([0x01, 0x02, 0x03, 0x04], 32));
    }
}