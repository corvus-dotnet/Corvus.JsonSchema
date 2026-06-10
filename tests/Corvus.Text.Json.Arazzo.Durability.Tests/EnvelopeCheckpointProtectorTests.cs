// <copyright file="EnvelopeCheckpointProtectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests the <see cref="EnvelopeCheckpointProtector"/> framing and data-key lifecycle with a fake wrapper, so
/// the shared envelope logic is verified without a key-management service (the real Key Vault / KMS wrap/unwrap
/// is exercised end-to-end by the satellite packages' tests).
/// </summary>
[TestClass]
public sealed class EnvelopeCheckpointProtectorTests
{
    private static readonly WorkflowRunId Run = new("run-1");

    [TestMethod]
    public async Task Round_trips_a_checkpoint()
    {
        var protector = new ReversingEnvelopeProtector();
        byte[] plaintext = Encoding.UTF8.GetBytes("""{"value":42}""");

        ReadOnlyMemory<byte> ciphertext = await protector.ProtectAsync(plaintext, Run, default);
        ReadOnlyMemory<byte> roundTripped = await protector.UnprotectAsync(ciphertext, Run, default);

        roundTripped.ToArray().ShouldBe(plaintext);
        ciphertext.ToArray().ShouldNotBe(plaintext);
    }

    [TestMethod]
    public async Task Tampered_ciphertext_fails_closed()
    {
        var protector = new ReversingEnvelopeProtector();
        byte[] ciphertext = (await protector.ProtectAsync(Encoding.UTF8.GetBytes("secret"), Run, default)).ToArray();
        ciphertext[^1] ^= 0xFF;

        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(ciphertext, Run, default));
    }

    [TestMethod]
    public async Task Decrypting_under_a_different_run_id_fails()
    {
        var protector = new ReversingEnvelopeProtector();
        ReadOnlyMemory<byte> ciphertext = await protector.ProtectAsync(Encoding.UTF8.GetBytes("secret"), Run, default);

        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(ciphertext, new WorkflowRunId("other"), default));
    }

    [TestMethod]
    public async Task Rejects_malformed_input()
    {
        var protector = new ReversingEnvelopeProtector();
        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(new byte[2], Run, default));
        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00 }, Run, default));
    }

    // Wraps the data key by reversing it — proves the envelope carries an arbitrary wrapped blob and round-trips
    // it, without depending on a key-management service.
    private sealed class ReversingEnvelopeProtector : EnvelopeCheckpointProtector
    {
        protected override ValueTask<ReadOnlyMemory<byte>> WrapAsync(ReadOnlyMemory<byte> dataKey, WorkflowRunId id, CancellationToken cancellationToken)
        {
            byte[] wrapped = dataKey.ToArray();
            Array.Reverse(wrapped);
            return new ValueTask<ReadOnlyMemory<byte>>(wrapped);
        }

        protected override ValueTask<ReadOnlyMemory<byte>> UnwrapAsync(ReadOnlyMemory<byte> wrappedDataKey, WorkflowRunId id, CancellationToken cancellationToken)
        {
            byte[] dataKey = wrappedDataKey.ToArray();
            Array.Reverse(dataKey);
            return new ValueTask<ReadOnlyMemory<byte>>(dataKey);
        }
    }
}