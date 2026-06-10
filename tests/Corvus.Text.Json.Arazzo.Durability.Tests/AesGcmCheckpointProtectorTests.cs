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
    public async Task Round_trips_a_checkpoint()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        byte[] plaintext = Encoding.UTF8.GetBytes("""{"value":42}""");

        ReadOnlyMemory<byte> ciphertext = await protector.ProtectAsync(plaintext, Run, default);
        ReadOnlyMemory<byte> roundTripped = await protector.UnprotectAsync(ciphertext, Run, default);

        roundTripped.ToArray().ShouldBe(plaintext);
    }

    [TestMethod]
    public async Task Ciphertext_is_not_the_plaintext_and_varies_per_call()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        byte[] plaintext = Encoding.UTF8.GetBytes("secret");

        byte[] first = (await protector.ProtectAsync(plaintext, Run, default)).ToArray();
        byte[] second = (await protector.ProtectAsync(plaintext, Run, default)).ToArray();

        first.ShouldNotBe(plaintext);
        first.ShouldNotBe(second); // fresh random nonce per call
    }

    [TestMethod]
    public async Task Tampered_ciphertext_fails_closed()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        byte[] ciphertext = (await protector.ProtectAsync(Encoding.UTF8.GetBytes("secret"), Run, default)).ToArray();
        ciphertext[^1] ^= 0xFF;

        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(ciphertext, Run, default));
    }

    [TestMethod]
    public async Task Decrypting_under_a_different_run_id_fails()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        ReadOnlyMemory<byte> ciphertext = await protector.ProtectAsync(Encoding.UTF8.GetBytes("secret"), Run, default);

        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(ciphertext, new WorkflowRunId("other"), default));
    }

    [TestMethod]
    public async Task Decrypting_under_a_different_key_fails()
    {
        ReadOnlyMemory<byte> ciphertext;
        using (var encryptor = new AesGcmCheckpointProtector(Key(0)))
        {
            ciphertext = await encryptor.ProtectAsync(Encoding.UTF8.GetBytes("secret"), Run, default);
        }

        using var other = new AesGcmCheckpointProtector(Key(99));
        await Should.ThrowAsync<CryptographicException>(async () => await other.UnprotectAsync(ciphertext, Run, default));
    }

    [TestMethod]
    public void Rejects_a_key_of_the_wrong_length()
    {
        Should.Throw<ArgumentException>(() => new AesGcmCheckpointProtector(new byte[20]));
    }

    [TestMethod]
    public async Task Rejects_ciphertext_that_is_too_short()
    {
        using var protector = new AesGcmCheckpointProtector(Key(0));
        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(new byte[8], Run, default));
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