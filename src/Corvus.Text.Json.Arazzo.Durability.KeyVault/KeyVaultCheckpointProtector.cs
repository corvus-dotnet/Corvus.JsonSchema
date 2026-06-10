// <copyright file="KeyVaultCheckpointProtector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault;

/// <summary>
/// An <see cref="ICheckpointProtector"/> that envelope-encrypts checkpoints using an Azure Key Vault key as the
/// key-encryption key: each checkpoint gets a fresh data key (AES-GCM, run id bound as additional authenticated
/// data), and that data key is wrapped/unwrapped by the vault via <see cref="CryptographyClient"/>. The data
/// key never leaves the process in the clear except transiently; the vault key never leaves the vault.
/// </summary>
/// <remarks>
/// The default wrap algorithm is <see cref="KeyWrapAlgorithm.RsaOaep256"/> (for an RSA vault key); pass another
/// for an EC or AES (Managed HSM) key. Key rotation is handled by the vault — a new key version wraps fresh
/// checkpoints while old versions still unwrap existing ones, with no data re-encryption.
/// </remarks>
public sealed class KeyVaultCheckpointProtector : EnvelopeCheckpointProtector
{
    private readonly CryptographyClient cryptographyClient;
    private readonly KeyWrapAlgorithm wrapAlgorithm;

    /// <summary>Initializes a new instance of the <see cref="KeyVaultCheckpointProtector"/> class.</summary>
    /// <param name="cryptographyClient">A cryptography client for the Key Vault key (the caller owns it).</param>
    /// <param name="wrapAlgorithm">The key-wrap algorithm; defaults to <see cref="KeyWrapAlgorithm.RsaOaep256"/>.</param>
    public KeyVaultCheckpointProtector(CryptographyClient cryptographyClient, KeyWrapAlgorithm? wrapAlgorithm = null)
    {
        ArgumentNullException.ThrowIfNull(cryptographyClient);
        this.cryptographyClient = cryptographyClient;
        this.wrapAlgorithm = wrapAlgorithm ?? KeyWrapAlgorithm.RsaOaep256;
    }

    /// <summary>Initializes a new instance of the <see cref="KeyVaultCheckpointProtector"/> class.</summary>
    /// <param name="keyId">The Key Vault key identifier (e.g. <c>https://vault.vault.azure.net/keys/name/version</c>).</param>
    /// <param name="credential">The credential to authenticate to the vault (for example a managed identity).</param>
    /// <param name="wrapAlgorithm">The key-wrap algorithm; defaults to <see cref="KeyWrapAlgorithm.RsaOaep256"/>.</param>
    public KeyVaultCheckpointProtector(Uri keyId, TokenCredential credential, KeyWrapAlgorithm? wrapAlgorithm = null)
        : this(new CryptographyClient(keyId, credential), wrapAlgorithm)
    {
    }

    /// <inheritdoc/>
    protected override async ValueTask<ReadOnlyMemory<byte>> WrapAsync(ReadOnlyMemory<byte> dataKey, WorkflowRunId id, CancellationToken cancellationToken)
    {
        WrapResult result = await this.cryptographyClient.WrapKeyAsync(this.wrapAlgorithm, dataKey.ToArray(), cancellationToken).ConfigureAwait(false);
        return result.EncryptedKey;
    }

    /// <inheritdoc/>
    protected override async ValueTask<ReadOnlyMemory<byte>> UnwrapAsync(ReadOnlyMemory<byte> wrappedDataKey, WorkflowRunId id, CancellationToken cancellationToken)
    {
        UnwrapResult result = await this.cryptographyClient.UnwrapKeyAsync(this.wrapAlgorithm, wrappedDataKey.ToArray(), cancellationToken).ConfigureAwait(false);
        return result.Key;
    }
}