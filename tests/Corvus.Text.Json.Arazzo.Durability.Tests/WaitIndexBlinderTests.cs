// <copyright file="WaitIndexBlinderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The blind wait index of ADR 0065 decision 4: an HMAC under the environment's <c>wait-index</c> subkey over the
/// length-framed channel and correlation id, keyed by generation, with an explicit sentinel for a channel-only wait.
/// </summary>
[TestClass]
public sealed class WaitIndexBlinderTests
{
    private static readonly byte[] PayloadKey = Enumerable.Range(0, 32).Select(i => (byte)(i + 9)).ToArray();

    [TestMethod]
    public void The_index_is_the_generation_and_the_hmac_under_the_wait_index_subkey_over_the_framed_key()
    {
        var blinder = new WaitIndexBlinder("production", "k2", PayloadKey);

        string index = blinder.Blind("kyc.verdict", "acct-42");

        byte[] subkey = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.WaitIndex, "production", "k2", subkey);
        byte[] channel = Encoding.UTF8.GetBytes("kyc.verdict");
        byte[] correlation = Encoding.UTF8.GetBytes("acct-42");
        byte[] message = [.. Frame(channel), 1, .. Frame(correlation)];
        index.ShouldBe("k2." + Base64Url.EncodeToString(HMACSHA256.HashData(subkey, message)));
        blinder.KeyId.ShouldBe("k2");
    }

    [TestMethod]
    public void Framing_stops_a_different_split_of_the_same_bytes_colliding()
    {
        var blinder = new WaitIndexBlinder("production", "k2", PayloadKey);

        blinder.Blind("orders", "123").ShouldNotBe(blinder.Blind("order", "s123"));
        blinder.Blind("orders", "123").ShouldBe(blinder.Blind("orders", "123"), "deterministic, or nothing would ever match");
    }

    [TestMethod]
    public void The_environment_and_the_generation_separate_indexes_that_share_a_channel_and_key()
    {
        string production = new WaitIndexBlinder("production", "k2", PayloadKey).Blind("kyc.verdict", "acct-42");
        string staging = new WaitIndexBlinder("staging", "k2", PayloadKey).Blind("kyc.verdict", "acct-42");
        string rotated = new WaitIndexBlinder("production", "k3", PayloadKey).Blind("kyc.verdict", "acct-42");

        production.ShouldNotBe(staging, "one tenant's staging message must not wake its production run");
        production.ShouldNotBe(rotated, "the store keys the index by generation, so both match during a roll");
        rotated.ShouldStartWith("k3.");
    }

    [TestMethod]
    public void A_channel_only_wait_uses_a_sentinel_that_no_correlation_id_can_equal()
    {
        var blinder = new WaitIndexBlinder("production", "k2", PayloadKey);

        string any = blinder.BlindChannelOnly("kyc.verdict");
        any.ShouldBe(blinder.Blind("kyc.verdict", null));
        any.ShouldNotBe(blinder.Blind("kyc.verdict", string.Empty), "an empty correlation id is a value, not absence");
        any.ShouldNotBe(blinder.Blind("kyc.verdict", "\u0000"), "absence is a kind of its own under the framing, so no correlation id spells it");
        byte[] subkey = new byte[32];
        CheckpointDerivation.DeriveSubkey(PayloadKey, CheckpointSubkey.WaitIndex, "production", "k2", subkey);
        byte[] channelOnlyMessage = [.. Frame(Encoding.UTF8.GetBytes("kyc.verdict")), 0];
        any.ShouldBe("k2." + Base64Url.EncodeToString(HMACSHA256.HashData(subkey, channelOnlyMessage)), "the channel, then the no-correlation kind byte, and nothing after it");
        any.ShouldBe(blinder.BlindChannelOnly("kyc.verdict"), "a per-channel constant, which is the accepted residue AR-8");
        any.ShouldNotBe(blinder.BlindChannelOnly("kyc.requests"));
    }

    [TestMethod]
    public void An_oversized_identifier_is_refused()
    {
        var blinder = new WaitIndexBlinder("production", "k2", PayloadKey);

        Should.Throw<ArgumentException>(() => blinder.Blind(new string('c', 257), "x"));
        Should.Throw<ArgumentException>(() => blinder.Blind("c", new string('x', 257)));
        Should.Throw<ArgumentException>(() => blinder.Blind(string.Empty, "x"));
    }

    private static byte[] Frame(byte[] value)
    {
        byte[] framed = new byte[4 + value.Length];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(framed, value.Length);
        value.CopyTo(framed, 4);
        return framed;
    }
}