// <copyright file="KeyVaultCheckpointProtectorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault.Tests;

/// <summary>Tests for <see cref="KeyVaultCheckpointProtector"/> using a fake cryptography client.</summary>
[TestClass]
public sealed class KeyVaultCheckpointProtectorTests
{
    private static readonly WorkflowRunId Run = new("run-1");

    [TestMethod]
    public async Task Round_trips_a_checkpoint_through_the_vault_wrap()
    {
        var protector = new KeyVaultCheckpointProtector(new FakeCryptographyClient());
        byte[] plaintext = Encoding.UTF8.GetBytes("""{"value":42}""");

        ReadOnlyMemory<byte> ciphertext = await protector.ProtectAsync(plaintext, Run, default);
        ReadOnlyMemory<byte> roundTripped = await protector.UnprotectAsync(ciphertext, Run, default);

        roundTripped.ToArray().ShouldBe(plaintext);
        ciphertext.ToArray().ShouldNotBe(plaintext);
    }

    [TestMethod]
    public async Task Decrypting_under_a_different_run_id_fails()
    {
        var protector = new KeyVaultCheckpointProtector(new FakeCryptographyClient());
        ReadOnlyMemory<byte> ciphertext = await protector.ProtectAsync(Encoding.UTF8.GetBytes("secret"), Run, default);

        await Should.ThrowAsync<CryptographicException>(async () => await protector.UnprotectAsync(ciphertext, new WorkflowRunId("other"), default));
    }

    [TestMethod]
    public void Rejects_a_null_client()
    {
        Should.Throw<ArgumentNullException>(() => new KeyVaultCheckpointProtector((Azure.Security.KeyVault.Keys.Cryptography.CryptographyClient)null!));
    }
}
