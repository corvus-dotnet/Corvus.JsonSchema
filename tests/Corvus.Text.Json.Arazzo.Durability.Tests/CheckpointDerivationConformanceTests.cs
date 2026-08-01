// <copyright file="CheckpointDerivationConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Conformance assertion 4 of ADR 0065's normative specification: the derivation table and the digest extent,
/// which are byte-exact contracts shared by six writers and every open that compares one.
/// </summary>
/// <remarks>
/// Several of these tests encode a defect that adversarial review found while these rules were prose. A test
/// keeps them closed after every future edit, which prose could not: the review had to notice the ambiguity, and
/// noticing is exactly what stopped happening between rounds.
/// </remarks>
[TestClass]
public sealed class CheckpointDerivationConformanceTests
{
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
    private static readonly byte[] Salt = Enumerable.Range(100, 32).Select(i => (byte)i).ToArray();

    [TestMethod]
    public void Field_framing_stops_a_different_split_deriving_the_same_key()
    {
        // The defect this encodes: unframed concatenation makes (keyId "k1", runId "0abc") and
        // (keyId "k10", runId "abc") one info string and therefore one key — and with a counter nonce that is a
        // repeated (key, nonce) pair over different plaintexts, which yields the GCM authentication subkey.
        //
        // Assert first that these two inputs really are a collision pair absent framing. Without this the test
        // below passes for any two distinct inputs, proving nothing about framing at all.
        string.Concat("k1", "0abc").ShouldBe(string.Concat("k10", "abc"));

        Span<byte> a = stackalloc byte[32];
        Span<byte> b = stackalloc byte[32];

        CheckpointDerivation.DeriveDataKey(PayloadKey, "env", "k1", "0abc", 1, Salt, a);
        CheckpointDerivation.DeriveDataKey(PayloadKey, "env", "k10", "abc", 1, Salt, b);

        a.SequenceEqual(b).ShouldBeFalse("a different field split must never derive the same data key");
    }

    [TestMethod]
    public void The_environment_separates_subkeys_that_share_a_key_id()
    {
        // The defect this encodes: keyId is tenant-chosen, so one tenant registering the same id in two
        // environments derived a single wait-index subkey — and a message delivered to one environment then woke
        // a run parked in the other, defeating environment pinning.
        Span<byte> prod = stackalloc byte[32];
        Span<byte> staging = stackalloc byte[32];

        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.WaitIndex, "acme-prod", "k1", prod);
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.WaitIndex, "acme-staging", "k1", staging);

        prod.SequenceEqual(staging).ShouldBeFalse("two environments sharing a key id must not share a subkey");
    }

    [TestMethod]
    public void Every_label_derives_a_distinct_subkey()
    {
        // A tenant-registered keyId must not be able to collide a data key with a subkey, which is what the
        // closed, framed label set prevents.
        var derived = new List<byte[]>();
        foreach (CheckpointSubkey subkey in new[] { CheckpointSubkey.EnvelopeMac, CheckpointSubkey.WaitIndex, CheckpointSubkey.CheckpointToken })
        {
            byte[] key = new byte[32];
            CheckpointDerivation.DeriveSubkey(PayloadKey, subkey, "env", "k1", key);
            derived.Add(key);
        }

        byte[] dataKey = new byte[32];
        CheckpointDerivation.DeriveDataKey(PayloadKey, "env", "k1", "run", 0, Salt, dataKey);
        derived.Add(dataKey);

        derived.Select(Convert.ToHexString).Distinct().Count().ShouldBe(derived.Count);
    }

    [TestMethod]
    public void A_data_key_varies_with_every_input_that_makes_it_unique()
    {
        byte[] baseline = DataKey("env", "k1", "run", 1, Salt);

        DataKey("other", "k1", "run", 1, Salt).ShouldNotBe(baseline);
        DataKey("env", "k2", "run", 1, Salt).ShouldNotBe(baseline);
        DataKey("env", "k1", "other", 1, Salt).ShouldNotBe(baseline);
        DataKey("env", "k1", "run", 2, Salt).ShouldNotBe(baseline);
        DataKey("env", "k1", "run", 1, Enumerable.Range(7, 32).Select(i => (byte)i).ToArray()).ShouldNotBe(baseline);

        // And is deterministic for identical inputs, which is what makes an open able to reproduce it.
        DataKey("env", "k1", "run", 1, Salt).ShouldBe(baseline);
    }

    [TestMethod]
    public void A_per_generation_subkey_is_deterministic_and_does_not_take_a_run()
    {
        // The three per-generation subkeys are derived once per key generation and cached; only the data key is
        // per encryption, because only it takes the run, sequence, and salt.
        byte[] first = new byte[32];
        byte[] second = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, "env", "k1", first);
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.EnvelopeMac, "env", "k1", second);

        first.ShouldBe(second);

        Should.Throw<ArgumentException>(() => CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.DataKey, "env", "k1", first));
    }

    [TestMethod]
    public void The_digest_covers_the_submitted_bytes_and_is_domain_separated_from_genesis()
    {
        byte[] submitted = Encoding.UTF8.GetBytes("header|region|salt|nonce|tag|ciphertext|mac");

        AnchorDigest digest = CheckpointDigest.ForSubmitted(submitted);
        CheckpointDigest.ForSubmitted(submitted).ShouldBe(digest);

        // A single byte anywhere in the submitted range changes it — that is what makes the digest a
        // commitment to the checkpoint rather than to its coordinates.
        byte[] altered = (byte[])submitted.Clone();
        altered[^1] ^= 0xFF;
        CheckpointDigest.ForSubmitted(altered).ShouldNotBe(digest);

        // The two constructions are domain-separated, so a genesis row can never present as a checkpoint row.
        CheckpointDigest.ForGenesis(submitted, []).ShouldNotBe(digest);
    }

    [TestMethod]
    public void The_genesis_digest_frames_its_two_fields_independently()
    {
        // Unframed, ("ab", "c") and ("a", "bc") would hash identically — currently unexploitable only because
        // signature length is fixed per algorithm, which stops being true the moment a second one exists.
        byte[] ab = Encoding.UTF8.GetBytes("ab");
        byte[] c = Encoding.UTF8.GetBytes("c");
        byte[] a = Encoding.UTF8.GetBytes("a");
        byte[] bc = Encoding.UTF8.GetBytes("bc");

        // As above: prove the pair collides unframed, or the assertion below is vacuous.
        ab.Concat(c).ShouldBe(a.Concat(bc));

        CheckpointDigest.ForGenesis(ab, c).ShouldNotBe(CheckpointDigest.ForGenesis(a, bc));
    }

    [TestMethod]
    public void An_oversized_identifier_is_rejected_rather_than_overflowing_the_stack()
    {
        // keyId is tenant-registered, so it reaches this code from outside the trust boundary. The buffer is
        // stack-allocated from its length, and a stack overflow kills the process rather than failing the write.
        byte[] key = new byte[32];
        string huge = new('k', 100_000);

        Should.Throw<ArgumentException>(() => CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.WaitIndex, "env", huge, key));
        Should.Throw<ArgumentException>(() => CheckpointDerivation.DeriveDataKey(PayloadKey, "env", "k1", huge, 1, Salt, key));
    }

    [TestMethod]
    public void A_derived_key_is_usable_as_an_aead_key()
    {
        // The derivation is only useful if what comes out is a valid AES-256-GCM key; this also pins the size.
        byte[] key = DataKey("env", "k1", "run", 3, Salt);
        key.Length.ShouldBe(32);

        Span<byte> nonce = stackalloc byte[12];
        Span<byte> tag = stackalloc byte[16];
        byte[] plaintext = Encoding.UTF8.GetBytes("""{"outputs":{"value":42}}""");
        Span<byte> ciphertext = stackalloc byte[plaintext.Length];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, "run|1"u8);

        Span<byte> roundTripped = stackalloc byte[plaintext.Length];
        aes.Decrypt(nonce, ciphertext, tag, roundTripped, "run|1"u8);
        roundTripped.SequenceEqual(plaintext).ShouldBeTrue();
    }

    private static byte[] DataKey(string environmentId, string keyId, string runId, ulong sequence, byte[] salt)
    {
        byte[] key = new byte[32];
        CheckpointDerivation.DeriveDataKey(PayloadKey, environmentId, keyId, runId, sequence, salt, key);
        return key;
    }
}