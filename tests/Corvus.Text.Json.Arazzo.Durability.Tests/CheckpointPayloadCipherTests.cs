// <copyright file="CheckpointPayloadCipherTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The payload encryption of ADR 0065 decision 5, as built: AES-256-GCM under a data key derived for the one
/// operation, with the run, environment, generation and sequence bound as associated data. Each case below is one
/// input the ciphertext has to be bound to, and one way a payload could be moved that the binding refuses.
/// </summary>
[TestClass]
public sealed class CheckpointPayloadCipherTests
{
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i * 3 + 1)).ToArray();
    private static readonly byte[] OtherKey = Enumerable.Range(0, 32).Select(i => (byte)(i * 5 + 2)).ToArray();
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("""{"correlationTokens":{},"inputs":{"petId":7},"stepOutputs":{"getPet":{"status":"available"}}}""");

    [TestMethod]
    public void A_payload_round_trips_and_the_ciphertext_hides_it()
    {
        Sealed sealedPayload = Encrypt(Plaintext);

        sealedPayload.Salt.Length.ShouldBe(CheckpointPayloadCipher.SaltLength);
        sealedPayload.Nonce.ShouldBe(new byte[CheckpointPayloadCipher.NonceLength], "the nonce is the counter's first value; the fresh salt is what makes it safe");
        sealedPayload.Tag.Length.ShouldBe(CheckpointPayloadCipher.TagLength);
        sealedPayload.Ciphertext.Length.ShouldBe(Plaintext.Length);
        Encoding.Latin1.GetString(sealedPayload.Ciphertext).ShouldNotContain("petId");

        Decrypt(sealedPayload).ShouldBe(Plaintext);
    }

    [TestMethod]
    public void Every_encryption_draws_a_fresh_salt_so_the_same_payload_never_encrypts_the_same_way()
    {
        // Decision 5: a fresh salt per operation, never per logical checkpoint. Two saves of one sequence (a retry
        // after a 409) must not share a data key, or the counter nonce repeats under it.
        Sealed first = Encrypt(Plaintext);
        Sealed second = Encrypt(Plaintext);

        first.Salt.ShouldNotBe(second.Salt);
        first.Ciphertext.ShouldNotBe(second.Ciphertext);
        first.Tag.ShouldNotBe(second.Tag);
    }

    [TestMethod]
    public void The_payload_is_bound_to_its_run()
    {
        Sealed sealedPayload = Encrypt(Plaintext);
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload, runId: "run-2"));
    }

    [TestMethod]
    public void The_payload_is_bound_to_its_environment()
    {
        Sealed sealedPayload = Encrypt(Plaintext);
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload, environment: "staging"));
    }

    [TestMethod]
    public void The_payload_is_bound_to_its_key_generation()
    {
        Sealed sealedPayload = Encrypt(Plaintext);
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload, keyId: "k2"));
    }

    [TestMethod]
    public void The_payload_is_bound_to_its_sequence()
    {
        // The sequence is in the AAD as a fixed-width integer: a payload from checkpoint 3 served as checkpoint 4 does
        // not open, whichever envelope it is joined to.
        Sealed sealedPayload = Encrypt(Plaintext);
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload, sequence: 4));
    }

    [TestMethod]
    public void The_associated_data_is_the_framed_run_environment_generation_and_sequence_and_nothing_else()
    {
        // The derivation already binds every one of these, so a payload moved to another run fails on the key before
        // the AEAD is asked. The AEAD's own binding is the second layer, and this pins it: the derived data key opens
        // the payload under exactly this associated data, and under none other, so the layer is present and its
        // framing is the one ADR 0065 decision 4 names.
        Sealed sealedPayload = Encrypt(Plaintext);
        byte[] dataKey = new byte[32];
        Anchoring.CheckpointDerivation.DeriveDataKey(PayloadKey, "production", "k1", "run-1", 3, sealedPayload.Salt, dataKey);
        byte[] opened = new byte[sealedPayload.Ciphertext.Length];
        using var aes = new AesGcm(dataKey, CheckpointPayloadCipher.TagLength);

        byte[] associatedData = [.. Frame("run-1"), .. Frame("production"), .. Frame("k1"), 0, 0, 0, 0, 0, 0, 0, 3];
        aes.Decrypt(sealedPayload.Nonce, sealedPayload.Ciphertext, sealedPayload.Tag, opened, associatedData);
        opened.ShouldBe(Plaintext);

        byte[] none = [];
        byte[] otherSequence = [.. Frame("run-1"), .. Frame("production"), .. Frame("k1"), 0, 0, 0, 0, 0, 0, 0, 0];
        byte[] noSequence = [.. Frame("run-1"), .. Frame("production"), .. Frame("k1")];
        Should.Throw<CryptographicException>(() => aes.Decrypt(sealedPayload.Nonce, sealedPayload.Ciphertext, sealedPayload.Tag, opened, none), "no associated data");
        Should.Throw<CryptographicException>(() => aes.Decrypt(sealedPayload.Nonce, sealedPayload.Ciphertext, sealedPayload.Tag, opened, otherSequence), "the sequence is in it");
        Should.Throw<CryptographicException>(() => aes.Decrypt(sealedPayload.Nonce, sealedPayload.Ciphertext, sealedPayload.Tag, opened, noSequence), "and fixed-width");

        static byte[] Frame(string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            return [0, 0, 0, (byte)utf8.Length, .. utf8];
        }
    }

    [TestMethod]
    public void The_wrong_key_does_not_open_the_payload()
    {
        Sealed sealedPayload = Encrypt(Plaintext);
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload, key: OtherKey));
    }

    [TestMethod]
    public void A_tampered_ciphertext_or_tag_or_salt_does_not_open()
    {
        Sealed sealedPayload = Encrypt(Plaintext);

        byte[] ciphertext = [.. sealedPayload.Ciphertext];
        ciphertext[5] ^= 0x01;
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload with { Ciphertext = ciphertext }));

        byte[] tag = [.. sealedPayload.Tag];
        tag[0] ^= 0x80;
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload with { Tag = tag }));

        byte[] salt = [.. sealedPayload.Salt];
        salt[31] ^= 0x01;
        Should.Throw<CryptographicException>(() => Decrypt(sealedPayload with { Salt = salt }), "a different salt is a different data key");
    }

    [TestMethod]
    public void A_failed_decryption_leaves_no_plaintext_behind()
    {
        Sealed sealedPayload = Encrypt(Plaintext);
        byte[] tag = [.. sealedPayload.Tag];
        tag[0] ^= 0x80;
        byte[] plaintext = new byte[sealedPayload.Ciphertext.Length];
        Array.Fill(plaintext, (byte)0xEE);

        Should.Throw<CryptographicException>(() => CheckpointPayloadCipher.Decrypt(PayloadKey, "production", "k1", "run-1", 3, sealedPayload.Salt, sealedPayload.Nonce, tag, sealedPayload.Ciphertext, plaintext));

        plaintext.ShouldAllBe(b => b == 0);
    }

    [TestMethod]
    public void The_regions_have_fixed_lengths()
    {
        byte[] ciphertext = new byte[Plaintext.Length];
        Should.Throw<ArgumentOutOfRangeException>(() => CheckpointPayloadCipher.Encrypt(PayloadKey, "production", "k1", "run-1", 3, Plaintext, new byte[16], new byte[12], new byte[16], ciphertext));
        Should.Throw<ArgumentOutOfRangeException>(() => CheckpointPayloadCipher.Encrypt(PayloadKey, "production", "k1", "run-1", 3, Plaintext, new byte[32], new byte[16], new byte[16], ciphertext));
        Should.Throw<ArgumentOutOfRangeException>(() => CheckpointPayloadCipher.Encrypt(PayloadKey, "production", "k1", "run-1", 3, Plaintext, new byte[32], new byte[12], new byte[12], ciphertext));
        Should.Throw<ArgumentOutOfRangeException>(() => CheckpointPayloadCipher.Encrypt(PayloadKey, "production", "k1", "run-1", 3, Plaintext, new byte[32], new byte[12], new byte[16], new byte[Plaintext.Length + 1]));
    }

    [TestMethod]
    public void An_identifier_beyond_the_derivation_bound_is_refused_before_anything_is_encrypted()
    {
        byte[] ciphertext = new byte[Plaintext.Length];
        Should.Throw<ArgumentException>(() => CheckpointPayloadCipher.Encrypt(PayloadKey, "production", new string('k', 257), "run-1", 3, Plaintext, new byte[32], new byte[12], new byte[16], ciphertext));
    }

    private static Sealed Encrypt(byte[] plaintext)
    {
        byte[] salt = new byte[CheckpointPayloadCipher.SaltLength];
        byte[] nonce = new byte[CheckpointPayloadCipher.NonceLength];
        byte[] tag = new byte[CheckpointPayloadCipher.TagLength];
        byte[] ciphertext = new byte[plaintext.Length];
        CheckpointPayloadCipher.Encrypt(PayloadKey, "production", "k1", "run-1", 3, plaintext, salt, nonce, tag, ciphertext);
        return new Sealed(salt, nonce, tag, ciphertext);
    }

    private static byte[] Decrypt(Sealed sealedPayload, byte[]? key = null, string environment = "production", string keyId = "k1", string runId = "run-1", ulong sequence = 3)
    {
        byte[] plaintext = new byte[sealedPayload.Ciphertext.Length];
        CheckpointPayloadCipher.Decrypt(key ?? PayloadKey, environment, keyId, runId, sequence, sealedPayload.Salt, sealedPayload.Nonce, sealedPayload.Tag, sealedPayload.Ciphertext, plaintext);
        return plaintext;
    }

    private sealed record Sealed(byte[] Salt, byte[] Nonce, byte[] Tag, byte[] Ciphertext);
}