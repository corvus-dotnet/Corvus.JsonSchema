// <copyright file="EcdsaExecutorPackageSigner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// An <see cref="IExecutorPackageSigner"/> that signs the manifest with a local in-process ECDSA private key — the
/// development / single-node signer, the counterpart of the local <c>AesGcmCheckpointProtector</c>. Production
/// deployments use the AWS KMS or Azure Key Vault signer, where the private key never leaves the vault; this signer
/// holds the key in process, so the trust boundary is only as strong as the control-plane host.
/// </summary>
/// <remarks>
/// The curve fixes the algorithm and digest: P-256 signs with SHA-256 (<see cref="ExecutorSignatureAlgorithms.EcdsaP256Sha256"/>)
/// and P-384 with SHA-384 (<see cref="ExecutorSignatureAlgorithms.EcdsaP384Sha384"/>); the signature is the fixed-size
/// IEEE P1363 encoding a runner's verifier expects.
/// </remarks>
public sealed class EcdsaExecutorPackageSigner : IExecutorPackageSigner
{
    private readonly ECDsa privateKey;
    private readonly string keyId;
    private readonly string algorithm;
    private readonly HashAlgorithmName hash;

    /// <summary>Initializes a new instance of the <see cref="EcdsaExecutorPackageSigner"/> class.</summary>
    /// <param name="privateKey">The ECDSA private key (P-256 or P-384); the caller owns its lifetime.</param>
    /// <param name="keyId">The key identifier recorded in the signature, by which a runner's trust store selects the public key to verify against.</param>
    /// <exception cref="ArgumentException">The key is not a supported curve size (256 or 384).</exception>
    public EcdsaExecutorPackageSigner(ECDsa privateKey, string keyId)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        this.privateKey = privateKey;
        this.keyId = keyId;
        (this.algorithm, this.hash) = privateKey.KeySize switch
        {
            256 => (ExecutorSignatureAlgorithms.EcdsaP256Sha256, HashAlgorithmName.SHA256),
            384 => (ExecutorSignatureAlgorithms.EcdsaP384Sha384, HashAlgorithmName.SHA384),
            _ => throw new ArgumentException($"An executor-package ECDSA key must be P-256 or P-384; this one is {privateKey.KeySize}-bit.", nameof(privateKey)),
        };
    }

    /// <inheritdoc/>
    public ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken)
    {
        byte[] signature = this.privateKey.SignData(manifestUtf8.Span, this.hash, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return new ValueTask<ExecutorPackageSignature>(new ExecutorPackageSignature(this.algorithm, this.keyId, signature));
    }
}