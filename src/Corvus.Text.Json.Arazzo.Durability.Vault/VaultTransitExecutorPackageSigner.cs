// <copyright file="VaultTransitExecutorPackageSigner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using Corvus.Text.Json.Arazzo.Execution;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.Transit;

namespace Corvus.Text.Json.Arazzo.Durability.Vault;

/// <summary>
/// An <see cref="IExecutorPackageSigner"/> that signs an executor manifest with a HashiCorp Vault <strong>Transit</strong>
/// engine key (design §3.3, §12): the sign operation runs in Vault, so the private key never leaves it — a control-plane
/// concern the runner cannot reach. The runner verifies with only the exported public key, held in its own trust store,
/// so it can never sign. The same Vault the <c>vault://</c> secret resolver reads can host the signing key on its Transit
/// mount.
/// </summary>
/// <remarks>
/// For ECDSA keys the signer asks Vault for the <c>jws</c> marshaling, so the signature is the fixed-size IEEE P1363
/// <c>r || s</c> the shared <c>TrustStoreExecutorPackageVerifier</c> checks (Vault's default <c>asn1</c> marshaling would
/// be DER, which the verifier does not accept); Vault base64url-encodes that value. For an RSA key it asks for the
/// <c>pss</c> signature algorithm, whose raw signature Vault standard-base64-encodes. Either way Vault wraps the value as
/// <c>vault:v&lt;n&gt;:&lt;base64&gt;</c>; this signer strips that envelope and decodes to the raw signature bytes.
/// </remarks>
public sealed class VaultTransitExecutorPackageSigner : IExecutorPackageSigner
{
    private const string DefaultMountPoint = "transit";

    private readonly IVaultClient client;
    private readonly string keyName;
    private readonly string mountPoint;
    private readonly string keyId;
    private readonly string algorithm;
    private readonly TransitHashAlgorithm hashAlgorithm;
    private readonly MarshalingAlgorithm? marshalingAlgorithm;
    private readonly SignatureAlgorithm? signatureAlgorithm;
    private readonly bool base64Url;

    /// <summary>Initializes a new instance of the <see cref="VaultTransitExecutorPackageSigner"/> class.</summary>
    /// <param name="client">The Vault client (caller-configured address + least-privileged auth with sign access on the Transit key).</param>
    /// <param name="keyName">The Transit key name that signs the manifest.</param>
    /// <param name="keyId">The key identifier stamped into the signature, by which a runner's trust store selects the public key to verify against.</param>
    /// <param name="algorithm">The signature algorithm, one of <see cref="ExecutorSignatureAlgorithms"/>: <see cref="ExecutorSignatureAlgorithms.EcdsaP256Sha256"/>, <see cref="ExecutorSignatureAlgorithms.EcdsaP384Sha384"/>, or <see cref="ExecutorSignatureAlgorithms.RsaPssSha256"/>.</param>
    /// <param name="mountPoint">The Transit engine mount point; defaults to <c>transit</c>.</param>
    /// <exception cref="ArgumentException">The algorithm is not one the executor-package verifier understands.</exception>
    public VaultTransitExecutorPackageSigner(IVaultClient client, string keyName, string keyId, string algorithm, string mountPoint = DefaultMountPoint)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentException.ThrowIfNullOrEmpty(keyName);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        ArgumentException.ThrowIfNullOrEmpty(mountPoint);
        this.client = client;
        this.keyName = keyName;
        this.mountPoint = mountPoint;
        this.keyId = keyId;
        this.algorithm = algorithm;
        switch (algorithm)
        {
            case ExecutorSignatureAlgorithms.EcdsaP256Sha256:
                (this.hashAlgorithm, this.marshalingAlgorithm, this.signatureAlgorithm, this.base64Url) = (TransitHashAlgorithm.SHA2_256, MarshalingAlgorithm.jws, null, true);
                break;
            case ExecutorSignatureAlgorithms.EcdsaP384Sha384:
                (this.hashAlgorithm, this.marshalingAlgorithm, this.signatureAlgorithm, this.base64Url) = (TransitHashAlgorithm.SHA2_384, MarshalingAlgorithm.jws, null, true);
                break;
            case ExecutorSignatureAlgorithms.RsaPssSha256:
                (this.hashAlgorithm, this.marshalingAlgorithm, this.signatureAlgorithm, this.base64Url) = (TransitHashAlgorithm.SHA2_256, null, SignatureAlgorithm.pss, false);
                break;
            default:
                ThrowHelper.ThrowUnsupportedAlgorithm(algorithm);
                break;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken)
    {
        var options = new SignRequestOptions
        {
            Base64EncodedInput = Convert.ToBase64String(manifestUtf8.Span),
            HashAlgorithm = this.hashAlgorithm,
        };
        if (this.marshalingAlgorithm is { } marshaling)
        {
            options.MarshalingAlgorithm = marshaling;
        }

        if (this.signatureAlgorithm is { } signature)
        {
            options.SignatureAlgorithm = signature;
        }

        Secret<SigningResponse> result = await this.client.V1.Secrets.Transit.SignDataAsync(this.keyName, options, this.mountPoint).ConfigureAwait(false);
        byte[] value = DecodeVaultSignature(result.Data.Signature, this.base64Url);
        return new ExecutorPackageSignature(this.algorithm, this.keyId, value);
    }

    // Strips Vault's "vault:v<n>:" envelope and decodes the base64 payload — base64url for the ECDSA jws marshaling,
    // standard base64 for the raw RSA-PSS signature. Internal for round-trip testing without a live Vault.
    internal static byte[] DecodeVaultSignature(string vaultSignature, bool base64Url)
    {
        if (string.IsNullOrEmpty(vaultSignature))
        {
            ThrowHelper.ThrowEmptyTransitSignature();
        }

        int lastColon = vaultSignature.LastIndexOf(':');
        ReadOnlySpan<char> encoded = lastColon >= 0 ? vaultSignature.AsSpan(lastColon + 1) : vaultSignature;

        // Base64Url.DecodeFromChars reads the base64url alphabet and tolerates the absent padding directly, so neither
        // branch needs the substring, the character swaps, or the re-padding those used to cost.
        return base64Url ? Base64Url.DecodeFromChars(encoded) : Convert.FromBase64String(new string(encoded));
    }
}