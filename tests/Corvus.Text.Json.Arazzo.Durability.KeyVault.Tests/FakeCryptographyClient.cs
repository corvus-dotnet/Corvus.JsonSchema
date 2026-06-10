// <copyright file="FakeCryptographyClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault.Tests;

/// <summary>
/// A test double for <see cref="CryptographyClient"/> that "wraps" a data key by reversing its bytes, so the
/// <see cref="KeyVaultCheckpointProtector"/> wrap/unwrap path and the envelope can be exercised end-to-end
/// without a real Key Vault (Azure has no local emulator). The AES-GCM crypto is real; only the vault round-trip
/// is faked.
/// </summary>
internal sealed class FakeCryptographyClient : CryptographyClient
{
    public override Task<WrapResult> WrapKeyAsync(KeyWrapAlgorithm algorithm, byte[] key, CancellationToken cancellationToken = default)
    {
        byte[] wrapped = (byte[])key.Clone();
        Array.Reverse(wrapped);
        return Task.FromResult(CryptographyModelFactory.WrapResult("fake", wrapped, algorithm));
    }

    public override Task<UnwrapResult> UnwrapKeyAsync(KeyWrapAlgorithm algorithm, byte[] encryptedKey, CancellationToken cancellationToken = default)
    {
        byte[] unwrapped = (byte[])encryptedKey.Clone();
        Array.Reverse(unwrapped);
        return Task.FromResult(CryptographyModelFactory.UnwrapResult("fake", unwrapped, algorithm));
    }
}
