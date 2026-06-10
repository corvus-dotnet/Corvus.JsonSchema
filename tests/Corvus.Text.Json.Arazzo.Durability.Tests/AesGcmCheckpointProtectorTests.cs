// <copyright file="AesGcmCheckpointProtectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Tests for <see cref="AesGcmCheckpointProtector"/>.</summary>
[TestClass]
public sealed class AesGcmCheckpointProtectorTests
{
    private static readonly WorkflowRunId Run = new("run-1");

    [TestMethod]
    public void Round_trips_a_checkpoint()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        byte[] plaintext = Encoding.UTF8.GetBytes("""{"value":42}""");

        ReadOnlyMemory<byte> ciphertext = protector.Protect(plaintext, Run);
        ReadOnlyMemory<byte> roundTripped = protector.Unprotect(ciphertext.Span, Run);

        roundTripped.ToArray().ShouldBe(plaintext);
    }

    [TestMethod]
    public void Ciphertext_is_not_the_plaintext_and_varies_per_call()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        byte[] plaintext = Encoding.UTF8.GetBytes("secret");

        byte[] first = protector.Protect(plaintext, Run).ToArray();
        byte[] second = protector.Protect(plaintext, Run).ToArray();

        first.ShouldNotBe(plaintext);
        first.ShouldNotBe(second); // fresh random nonce per call
    }

    [TestMethod]
    public void Tampered_ciphertext_fails_closed()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        byte[] ciphertext = protector.Protect(Encoding.UTF8.GetBytes("secret"), Run).ToArray();
        ciphertext[^1] ^= 0xFF;

        Should.Throw<CryptographicException>(() => protector.Unprotect(ciphertext, Run));
    }

    [TestMethod]
    public void Decrypting_under_a_different_run_id_fails()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        ReadOnlyMemory<byte> ciphertext = protector.Protect(Encoding.UTF8.GetBytes("secret"), Run);

        Should.Throw<CryptographicException>(() => protector.Unprotect(ciphertext.Span, new WorkflowRunId("other")));
    }

    [TestMethod]
    public void Decrypting_under_a_different_key_fails()
    {
        ReadOnlyMemory<byte> ciphertext;
        using (var encryptor = new AesGcmCheckpointProtector(Key(0)))
        {
            ciphertext = encryptor.Protect(Encoding.UTF8.GetBytes("secret"), Run);
        }

        using var other = new AesGcmCheckpointProtector(Key(99));
        Should.Throw<CryptographicException>(() => other.Unprotect(ciphertext.Span, Run));
    }

    [TestMethod]
    public void Rejects_a_key_of_the_wrong_length()
    {
        Should.Throw<ArgumentException>(() => new AesGcmCheckpointProtector(new byte[20]));
    }

    [TestMethod]
    public void Rejects_ciphertext_that_is_too_short()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        Should.Throw<CryptographicException>(() => protector.Unprotect(new byte[8], Run));
    }

    private static byte[] Key(int seed)
    {
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + seed);
        }

        return key;
    }
}