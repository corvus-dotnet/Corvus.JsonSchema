// <copyright file="TrustStoreExecutorPackageVerifier.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// An <see cref="IExecutorPackageVerifier"/> backed by a local trust store of public keys (design §3.3, §12): the
/// runner is configured with the public key(s) it trusts, keyed by key id, and verifies each package signature against
/// the key the signature names. The store is deliberately independent of the control-plane vault that holds the private
/// signing key — the runner never contacts that vault and cannot sign. Trusting more than one key id supports rolling a
/// signing key over without a flag day (sign with the new key, keep verifying the old until every package is re-signed).
/// </summary>
/// <remarks>
/// It verifies any signer's output — the local ECDSA signer, AWS KMS, or Azure Key Vault — because verification uses
/// only the exported public key and the algorithm the signature declares (ECDSA P-256/P-384, or RSASSA-PSS). A key id
/// the store does not hold, an algorithm the trusted key cannot perform, or a signature that does not verify all return
/// <see langword="false"/> (fail-closed).
/// </remarks>
public sealed class TrustStoreExecutorPackageVerifier : IExecutorPackageVerifier
{
    private readonly IReadOnlyDictionary<string, AsymmetricAlgorithm> trustedKeys;

    /// <summary>Initializes a new instance of the <see cref="TrustStoreExecutorPackageVerifier"/> class.</summary>
    /// <param name="trustedPublicKeysByKeyId">The trusted public keys (<see cref="ECDsa"/> or <see cref="RSA"/>), keyed by the key id the signer stamps into a signature; the caller owns their lifetime.</param>
    public TrustStoreExecutorPackageVerifier(IReadOnlyDictionary<string, AsymmetricAlgorithm> trustedPublicKeysByKeyId)
    {
        ArgumentNullException.ThrowIfNull(trustedPublicKeysByKeyId);
        this.trustedKeys = trustedPublicKeysByKeyId;
    }

    /// <summary>Builds a verifier from PEM-encoded public keys (the <c>-----BEGIN PUBLIC KEY-----</c> form a runner carries in config).</summary>
    /// <param name="publicKeysByKeyId">The trusted public keys as PEM, keyed by key id.</param>
    /// <returns>A verifier over the imported keys.</returns>
    /// <exception cref="ArgumentException">A PEM value is neither an ECDSA nor an RSA public key.</exception>
    public static TrustStoreExecutorPackageVerifier FromPem(IReadOnlyDictionary<string, string> publicKeysByKeyId)
    {
        ArgumentNullException.ThrowIfNull(publicKeysByKeyId);
        var keys = new Dictionary<string, AsymmetricAlgorithm>(publicKeysByKeyId.Count, StringComparer.Ordinal);
        foreach ((string keyId, string pem) in publicKeysByKeyId)
        {
            keys[keyId] = ImportPublicKey(keyId, pem);
        }

        return new TrustStoreExecutorPackageVerifier(keys);
    }

    /// <inheritdoc/>
    public bool Verify(ReadOnlyMemory<byte> manifestUtf8, in ExecutorPackageSignature signature)
    {
        if (!this.trustedKeys.TryGetValue(signature.KeyId, out AsymmetricAlgorithm? key))
        {
            return false; // the key id is not in the trust store
        }

        ReadOnlySpan<byte> data = manifestUtf8.Span;
        ReadOnlySpan<byte> value = signature.Value.Span;
        return signature.Algorithm switch
        {
            ExecutorSignatureAlgorithms.EcdsaP256Sha256 => key is ECDsa ec256 && ec256.VerifyData(data, value, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            ExecutorSignatureAlgorithms.EcdsaP384Sha384 => key is ECDsa ec384 && ec384.VerifyData(data, value, HashAlgorithmName.SHA384, DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            ExecutorSignatureAlgorithms.RsaPssSha256 => key is RSA rsa && rsa.VerifyData(data, value, HashAlgorithmName.SHA256, RSASignaturePadding.Pss),
            _ => false, // an algorithm this runner does not understand
        };
    }

    private static AsymmetricAlgorithm ImportPublicKey(string keyId, string pem)
    {
        try
        {
            var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(pem);
            return ecdsa;
        }
        catch (ArgumentException)
        {
            // Not an ECDSA key — fall through to RSA.
        }

        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException($"The trusted public key '{keyId}' is neither an ECDSA nor an RSA public key.", nameof(pem), ex);
        }
    }
}