// <copyright file="KmsExecutorPackageSigner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.Kms;

/// <summary>
/// An <see cref="IExecutorPackageSigner"/> that signs an executor manifest with an AWS KMS <em>asymmetric</em> key
/// (design §3.3, §12): the sign operation runs inside KMS, so the private key never leaves the vault — a control-plane
/// concern the runner cannot reach. The runner verifies the resulting signature with only the exported public key, held
/// in its own trust store, so it can never sign.
/// </summary>
/// <remarks>
/// KMS returns an ECDSA signature in ASN.1 DER form; this signer normalises it to the fixed-size IEEE P1363 encoding the
/// shared <c>TrustStoreExecutorPackageVerifier</c> checks (RSASSA-PSS needs no normalisation). The supported KMS signing
/// algorithms are <c>ECDSA_SHA_256</c> (P-256), <c>ECDSA_SHA_384</c> (P-384) and <c>RSASSA_PSS_SHA_256</c> — the ones the
/// verifier understands.
/// </remarks>
public sealed class KmsExecutorPackageSigner : IExecutorPackageSigner
{
    private readonly IAmazonKeyManagementService kms;
    private readonly string kmsKeyId;
    private readonly string keyId;
    private readonly SigningAlgorithmSpec kmsAlgorithm;
    private readonly string algorithm;
    private readonly int? p1363FieldSizeBytes;

    /// <summary>Initializes a new instance of the <see cref="KmsExecutorPackageSigner"/> class.</summary>
    /// <param name="kms">An AWS KMS client (the caller owns it).</param>
    /// <param name="kmsKeyId">The KMS key id, ARN, or alias whose asymmetric key signs the manifest.</param>
    /// <param name="keyId">The key identifier stamped into the signature, by which a runner's trust store selects the public key to verify against (often the KMS key ARN, but any stable id the runner is configured to trust).</param>
    /// <param name="signingAlgorithm">The KMS signing algorithm: <see cref="SigningAlgorithmSpec.ECDSA_SHA_256"/>, <see cref="SigningAlgorithmSpec.ECDSA_SHA_384"/>, or <see cref="SigningAlgorithmSpec.RSASSA_PSS_SHA_256"/>.</param>
    /// <exception cref="ArgumentException">The signing algorithm is not one the executor-package verifier understands.</exception>
    public KmsExecutorPackageSigner(IAmazonKeyManagementService kms, string kmsKeyId, string keyId, SigningAlgorithmSpec signingAlgorithm)
    {
        ArgumentNullException.ThrowIfNull(kms);
        ArgumentException.ThrowIfNullOrEmpty(kmsKeyId);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        ArgumentNullException.ThrowIfNull(signingAlgorithm);
        this.kms = kms;
        this.kmsKeyId = kmsKeyId;
        this.keyId = keyId;
        this.kmsAlgorithm = signingAlgorithm;
        (this.algorithm, this.p1363FieldSizeBytes) = signingAlgorithm.Value switch
        {
            "ECDSA_SHA_256" => (ExecutorSignatureAlgorithms.EcdsaP256Sha256, 32),
            "ECDSA_SHA_384" => (ExecutorSignatureAlgorithms.EcdsaP384Sha384, 48),
            "RSASSA_PSS_SHA_256" => (ExecutorSignatureAlgorithms.RsaPssSha256, (int?)null),
            _ => throw new ArgumentException($"An executor-package KMS signer must use ECDSA_SHA_256, ECDSA_SHA_384, or RSASSA_PSS_SHA_256; '{signingAlgorithm.Value}' is not supported.", nameof(signingAlgorithm)),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken)
    {
        var request = new SignRequest
        {
            KeyId = this.kmsKeyId,
            Message = new MemoryStream(manifestUtf8.ToArray()),
            MessageType = MessageType.RAW,
            SigningAlgorithm = this.kmsAlgorithm,
        };

        SignResponse response = await this.kms.SignAsync(request, cancellationToken).ConfigureAwait(false);
        byte[] raw = response.Signature.ToArray();

        // ECDSA comes back as ASN.1 DER; the verifier expects P1363. RSASSA-PSS is already in the raw form the verifier checks.
        byte[] value = this.p1363FieldSizeBytes is int fieldSize ? EcdsaDerSignatureConverter.ToP1363(raw, fieldSize) : raw;
        return new ExecutorPackageSignature(this.algorithm, this.keyId, value);
    }
}