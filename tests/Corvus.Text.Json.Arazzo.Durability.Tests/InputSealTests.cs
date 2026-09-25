// <copyright file="InputSealTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The input seal of ADR 0065 decision 9 is RFC 9180 HPKE, base mode, DHKEM(P-256, HKDF-SHA256) with HKDF-SHA256:
/// the RFC's published vector for that KEM and KDF (appendix A.3.1, with its AES-128-GCM) holds byte for byte, and
/// the platform's AES-256-GCM seal opens only under the key, info and associated data it was made under.
/// </summary>
[TestClass]
public sealed class InputSealTests
{
    // RFC 9180 appendix A.3.1: DHKEM(P-256, HKDF-SHA256), HKDF-SHA256, AES-128-GCM, base mode.
    private const string Info = "4f6465206f6e2061204772656369616e2055726e";
    private const string PkEm = "04a92719c6195d5085104f469a8b9814d5838ff72b60501e2c4466e5e67b325ac98536d7b61a1af4b78e5b7f951c0900be863c403ce65c9bfcb9382657222d18c4";
    private const string SkEm = "4995788ef4b9d6132b249ce59a77281493eb39af373d236a1fe415cb0c2d7beb";
    private const string PkRm = "04fe8c19ce0905191ebc298a9245792531f26f0cece2460639e8bc39cb7f706a826a779b4cf969b8a0e539c7f62fb3d30ad6aa8f80e30f1d128aafd68a2ce72ea0";
    private const string SkRm = "f3ce7fdae57e1a310d87f1ebbde6f328be0a99cdbcadf4d6589cf29de4b8ffd2";
    private const string Pt = "4265617574792069732074727574682c20747275746820626561757479";
    private const string Aad0 = "436f756e742d30";
    private const string Ct0 = "5ad590bb8baa577f8619db35a36311226a896e7342a6d836d8b7bcd2f20b6c7f9076ac232e3ab2523f39513434";

    [TestMethod]
    public void The_rfc_9180_p256_base_mode_vector_holds()
    {
        using ECDiffieHellman recipient = Key(SkRm, PkRm);
        using ECDiffieHellman ephemeral = Key(SkEm, PkEm);
        byte[] plaintext = Convert.FromHexString(Pt);
        byte[] enc = new byte[InputSeal.EncLength];
        byte[] sealedValue = new byte[InputSeal.SealedLength(plaintext.Length)];

        InputSeal.Seal(recipient, ephemeral, Convert.FromHexString(Info), Convert.FromHexString(Aad0), plaintext, enc, sealedValue, InputSeal.AeadAes128Gcm);

        Convert.ToHexStringLower(enc).ShouldBe(PkEm, "the encapsulated key is the ephemeral public point");
        Convert.ToHexStringLower(sealedValue).ShouldBe(Ct0, "the first encryption's ciphertext and tag, under the RFC's key and base nonce");

        byte[] opened = new byte[plaintext.Length];
        InputSeal.Open(recipient, enc, Convert.FromHexString(Info), Convert.FromHexString(Aad0), sealedValue, opened, InputSeal.AeadAes128Gcm);
        opened.ShouldBe(plaintext);
    }

    [TestMethod]
    public void A_seal_to_a_registered_key_opens_under_that_key_and_nothing_else()
    {
        (byte[] spki, byte[] pkcs8) = SealKeyPair();
        (byte[] otherSpki, byte[] otherPkcs8) = SealKeyPair();
        byte[] inputs = Encoding.UTF8.GetBytes("""{"email":"ada@example.com"}""");
        byte[] aad = Encoding.UTF8.GetBytes("binding");
        byte[] enc = new byte[InputSeal.EncLength];
        byte[] sealedValue = new byte[InputSeal.SealedLength(inputs.Length)];
        InputSeal.Seal(spki, SealedStartSignature.SealInfo, aad, inputs, enc, sealedValue);

        sealedValue.Length.ShouldBe(inputs.Length + InputSeal.TagLength);
        Encoding.Latin1.GetString(sealedValue).Contains("ada@example.com", StringComparison.Ordinal).ShouldBeFalse();
        byte[] opened = new byte[inputs.Length];
        InputSeal.Open(pkcs8, enc, SealedStartSignature.SealInfo, aad, sealedValue, opened);
        opened.ShouldBe(inputs);

        Should.Throw<CryptographicException>(() => InputSeal.Open(otherPkcs8, enc, SealedStartSignature.SealInfo, aad, sealedValue, opened), "another key");
        Should.Throw<CryptographicException>(() => InputSeal.Open(pkcs8, enc, SealedStartSignature.SealInfo, "other"u8, sealedValue, opened), "other associated data");
        Should.Throw<CryptographicException>(() => InputSeal.Open(pkcs8, enc, "other-info"u8, aad, sealedValue, opened), "other info");
        byte[] tampered = [.. sealedValue];
        tampered[3] ^= 1;
        Should.Throw<CryptographicException>(() => InputSeal.Open(pkcs8, enc, SealedStartSignature.SealInfo, aad, tampered, opened), "a tampered ciphertext");
        byte[] otherEnc = [.. enc];
        otherEnc[10] ^= 1;
        Should.Throw<CryptographicException>(() => InputSeal.Open(pkcs8, otherEnc, SealedStartSignature.SealInfo, aad, sealedValue, opened), "a tampered encapsulated key is off the curve or another point");
        byte[] compressed = [.. enc];
        compressed[0] = 2;
        Should.Throw<CryptographicException>(() => InputSeal.Open(pkcs8, compressed, SealedStartSignature.SealInfo, aad, sealedValue, opened), "only an uncompressed point is a seal's encapsulated key");
        Should.Throw<CryptographicException>(() => InputSeal.Open(pkcs8, enc, SealedStartSignature.SealInfo, aad, sealedValue[..8], new byte[0]), "a value shorter than a tag");
        _ = otherSpki;
    }

    [TestMethod]
    public void Every_seal_uses_a_fresh_ephemeral_key()
    {
        (byte[] spki, _) = SealKeyPair();
        byte[] inputs = "{}"u8.ToArray();
        byte[] enc1 = new byte[InputSeal.EncLength];
        byte[] enc2 = new byte[InputSeal.EncLength];
        byte[] sealed1 = new byte[InputSeal.SealedLength(inputs.Length)];
        byte[] sealed2 = new byte[InputSeal.SealedLength(inputs.Length)];
        InputSeal.Seal(spki, SealedStartSignature.SealInfo, "aad"u8, inputs, enc1, sealed1);
        InputSeal.Seal(spki, SealedStartSignature.SealInfo, "aad"u8, inputs, enc2, sealed2);
        enc1.ShouldNotBe(enc2);
        sealed1.ShouldNotBe(sealed2);
    }

    [TestMethod]
    public void A_key_that_is_not_p256_is_refused()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        byte[] spki = p384.ExportSubjectPublicKeyInfo();
        byte[] enc = new byte[InputSeal.EncLength];
        Should.Throw<CryptographicException>(() => InputSeal.Seal(spki, SealedStartSignature.SealInfo, "aad"u8, "{}"u8, enc, new byte[InputSeal.SealedLength(2)]));
    }

    internal static (byte[] Spki, byte[] Pkcs8) SealKeyPair()
    {
        // The registered seal key is an ES256 key (a P-256 key made for ECDSA); the same key serves the seal.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfo(), key.ExportPkcs8PrivateKey());
    }

    private static ECDiffieHellman Key(string privateHex, string publicHex)
    {
        byte[] point = Convert.FromHexString(publicHex);
        return ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Convert.FromHexString(privateHex),
            Q = new ECPoint { X = point[1..33], Y = point[33..] },
        });
    }
}