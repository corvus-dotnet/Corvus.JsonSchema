// <copyright file="KeyVaultExecutorPackageSigner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault;

/// <summary>
/// An <see cref="IExecutorPackageSigner"/> that signs an executor manifest with an Azure Key Vault key (design §3.3,
/// §12): the sign operation runs in the vault via a <see cref="CryptographyClient"/>, so the private key never leaves
/// the vault — a control-plane concern the runner cannot reach. The runner verifies with only the exported public key,
/// held in its own trust store, so it can never sign.
/// </summary>
/// <remarks>
/// Key Vault returns an EC signature already in the fixed-size IEEE P1363 <c>r || s</c> form the shared
/// <c>TrustStoreExecutorPackageVerifier</c> checks (and RSASSA-PSS in the raw form it checks), so no re-encoding is
/// needed. The supported algorithms are <see cref="SignatureAlgorithm.ES256"/> (P-256), <see cref="SignatureAlgorithm.ES384"/>
/// (P-384) and <see cref="SignatureAlgorithm.PS256"/> — the ones the verifier understands.
/// </remarks>
public sealed class KeyVaultExecutorPackageSigner : IExecutorPackageSigner
{
    private readonly CryptographyClient cryptographyClient;
    private readonly string keyId;
    private readonly SignatureAlgorithm algorithm;
    private readonly string executorAlgorithm;

    /// <summary>Initializes a new instance of the <see cref="KeyVaultExecutorPackageSigner"/> class.</summary>
    /// <param name="cryptographyClient">A cryptography client for the Key Vault key (the caller owns it).</param>
    /// <param name="keyId">The key identifier stamped into the signature, by which a runner's trust store selects the public key to verify against.</param>
    /// <param name="algorithm">The signing algorithm: <see cref="SignatureAlgorithm.ES256"/>, <see cref="SignatureAlgorithm.ES384"/>, or <see cref="SignatureAlgorithm.PS256"/>.</param>
    /// <exception cref="ArgumentException">The algorithm is not one the executor-package verifier understands.</exception>
    public KeyVaultExecutorPackageSigner(CryptographyClient cryptographyClient, string keyId, SignatureAlgorithm algorithm)
    {
        ArgumentNullException.ThrowIfNull(cryptographyClient);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        this.cryptographyClient = cryptographyClient;
        this.keyId = keyId;
        this.algorithm = algorithm;
        this.executorAlgorithm = MapAlgorithm(algorithm);
    }

    /// <summary>Initializes a new instance of the <see cref="KeyVaultExecutorPackageSigner"/> class.</summary>
    /// <param name="keyVaultKeyId">The Key Vault key identifier (e.g. <c>https://vault.vault.azure.net/keys/name/version</c>) whose key signs the manifest.</param>
    /// <param name="credential">The credential to authenticate to the vault (for example a managed identity).</param>
    /// <param name="keyId">The key identifier stamped into the signature, by which a runner's trust store selects the public key to verify against.</param>
    /// <param name="algorithm">The signing algorithm: <see cref="SignatureAlgorithm.ES256"/>, <see cref="SignatureAlgorithm.ES384"/>, or <see cref="SignatureAlgorithm.PS256"/>.</param>
    public KeyVaultExecutorPackageSigner(Uri keyVaultKeyId, TokenCredential credential, string keyId, SignatureAlgorithm algorithm)
        : this(new CryptographyClient(keyVaultKeyId, credential), keyId, algorithm)
    {
    }

    /// <inheritdoc/>
    public async ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken)
    {
        // SignData hashes the manifest with the algorithm's digest inside the vault, then signs; the returned signature is
        // already in the encoding the verifier expects (P1363 for EC, raw PSS for RSA).
        SignResult result = await this.cryptographyClient.SignDataAsync(this.algorithm, manifestUtf8.ToArray(), cancellationToken).ConfigureAwait(false);
        return new ExecutorPackageSignature(this.executorAlgorithm, this.keyId, result.Signature);
    }

    private static string MapAlgorithm(SignatureAlgorithm algorithm)
        => algorithm.ToString() switch
        {
            "ES256" => ExecutorSignatureAlgorithms.EcdsaP256Sha256,
            "ES384" => ExecutorSignatureAlgorithms.EcdsaP384Sha384,
            "PS256" => ExecutorSignatureAlgorithms.RsaPssSha256,
            _ => throw new ArgumentException($"An executor-package Key Vault signer must use ES256, ES384, or PS256; '{algorithm}' is not supported.", nameof(algorithm)),
        };
}