// <copyright file="SealedCheckpointProtectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves the sealed-checkpoint properties ADR 0065 rests on: a seal-only holder (the control plane's posture)
/// can write but can never read back; only the environment's open key opens; a foreign environment's key and a
/// tampered or spliced blob fail closed; and the key id routes rotation with both ids named on a mismatch.
/// </summary>
[TestClass]
public sealed class SealedCheckpointProtectorTests
{
    private static readonly WorkflowRunId Run = new("run-1");
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("""{"cursor":"step-2","outputs":{"secret":"tenant data"}}""");

    [TestMethod]
    public async Task A_sealing_writer_seals_and_the_opening_holder_round_trips()
    {
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector sealer = SealedCheckpointProtector.ForSealing("env-key-1", pair.SealKey);
        using SealedCheckpointProtector opener = SealedCheckpointProtector.ForOpening("env-key-1", pair.OpenKey);

        ReadOnlyMemory<byte> sealed_ = await sealer.ProtectAsync(Plaintext, Run, default);
        ReadOnlyMemory<byte> opened = await opener.UnprotectAsync(sealed_, Run, default);

        opened.ToArray().ShouldBe(Plaintext);
        sealed_.ToArray().ShouldNotBe(Plaintext);
    }

    [TestMethod]
    public async Task The_seal_only_holder_cannot_open_what_it_wrote()
    {
        // The control plane's posture (ADR 0065): it seals the initial run document at start, and can never
        // read any checkpoint back — not a permissions decision, a key-custody impossibility.
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector controlPlane = SealedCheckpointProtector.ForSealing("env-key-1", pair.SealKey);

        ReadOnlyMemory<byte> sealed_ = await controlPlane.ProtectAsync(Plaintext, Run, default);

        InvalidOperationException refusal = await Should.ThrowAsync<InvalidOperationException>(
            async () => await controlPlane.UnprotectAsync(sealed_, Run, default));
        refusal.Message.ShouldContain("seal key");
    }

    [TestMethod]
    public async Task Another_environments_key_cannot_open_a_sealed_checkpoint()
    {
        // The cross-tenant property: same key id, different environment keypair — the open fails closed.
        SealedCheckpointKeyPair tenantA = SealedCheckpointProtector.GenerateKeyPair();
        SealedCheckpointKeyPair tenantB = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector sealer = SealedCheckpointProtector.ForSealing("env-key-1", tenantA.SealKey);
        using SealedCheckpointProtector foreignOpener = SealedCheckpointProtector.ForOpening("env-key-1", tenantB.OpenKey);

        ReadOnlyMemory<byte> sealed_ = await sealer.ProtectAsync(Plaintext, Run, default);

        await Should.ThrowAsync<CryptographicException>(async () => await foreignOpener.UnprotectAsync(sealed_, Run, default));
    }

    [TestMethod]
    public async Task A_key_id_mismatch_names_both_ids()
    {
        // Rotation routing: the envelope names the keypair it was sealed to, so an opener holding a different
        // registration refuses with a diagnosable message rather than a bare decrypt failure.
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector sealer = SealedCheckpointProtector.ForSealing("env-key-1", pair.SealKey);
        using SealedCheckpointProtector rotatedOpener = SealedCheckpointProtector.ForOpening("env-key-2", pair.OpenKey);

        ReadOnlyMemory<byte> sealed_ = await sealer.ProtectAsync(Plaintext, Run, default);

        CryptographicException mismatch = await Should.ThrowAsync<CryptographicException>(
            async () => await rotatedOpener.UnprotectAsync(sealed_, Run, default));
        mismatch.Message.ShouldContain("env-key-1");
        mismatch.Message.ShouldContain("env-key-2");
    }

    [TestMethod]
    public async Task Tampered_ciphertext_and_a_spliced_run_id_fail_closed()
    {
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector sealer = SealedCheckpointProtector.ForSealing("env-key-1", pair.SealKey);
        using SealedCheckpointProtector opener = SealedCheckpointProtector.ForOpening("env-key-1", pair.OpenKey);

        byte[] tampered = (await sealer.ProtectAsync(Plaintext, Run, default)).ToArray();
        tampered[^1] ^= 0xFF;
        await Should.ThrowAsync<CryptographicException>(async () => await opener.UnprotectAsync(tampered, Run, default));

        ReadOnlyMemory<byte> sealed_ = await sealer.ProtectAsync(Plaintext, Run, default);
        await Should.ThrowAsync<CryptographicException>(async () => await opener.UnprotectAsync(sealed_, new WorkflowRunId("other-run"), default));
    }

    [TestMethod]
    public async Task The_opening_holder_can_also_seal()
    {
        // A runner holds the open key and both writes and reads checkpoints with it; the seal key is derivable.
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector runner = SealedCheckpointProtector.ForOpening("env-key-1", pair.OpenKey);

        ReadOnlyMemory<byte> sealed_ = await runner.ProtectAsync(Plaintext, Run, default);
        (await runner.UnprotectAsync(sealed_, Run, default)).ToArray().ShouldBe(Plaintext);
    }

    [TestMethod]
    public async Task Every_seal_is_unique_even_for_identical_content()
    {
        // Ephemeral key + fresh data key + fresh nonce per seal: identical plaintext never yields identical
        // ciphertext, so stored checkpoints do not reveal content equality.
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();
        using SealedCheckpointProtector sealer = SealedCheckpointProtector.ForSealing("env-key-1", pair.SealKey);

        byte[] first = (await sealer.ProtectAsync(Plaintext, Run, default)).ToArray();
        byte[] second = (await sealer.ProtectAsync(Plaintext, Run, default)).ToArray();

        first.ShouldNotBe(second);
    }

    [TestMethod]
    public void Construction_validates_keys_and_ids()
    {
        SealedCheckpointKeyPair pair = SealedCheckpointProtector.GenerateKeyPair();

        Should.Throw<CryptographicException>(() => SealedCheckpointProtector.ForSealing("env-key-1", new byte[] { 1, 2, 3 }));
        Should.Throw<CryptographicException>(() => SealedCheckpointProtector.ForOpening("env-key-1", pair.SealKey));
        Should.Throw<ArgumentException>(() => SealedCheckpointProtector.ForSealing(string.Empty, pair.SealKey));
        Should.Throw<ArgumentException>(() => SealedCheckpointProtector.ForSealing(new string('k', 300), pair.SealKey));
    }
}