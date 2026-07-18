// <copyright file="IExecutorPackageSigner.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// Signs an executor manifest at catalog-add time (design §3.3, §12). The signing key lives in a vault the
/// <em>control plane</em> can reach (an AWS KMS asymmetric key, an Azure Key Vault key, or a local development key);
/// the private key never leaves that vault — the crypto op runs where the key is held. The resulting detached
/// <see cref="ExecutorPackageSignature"/> is carried in the package for a runner to verify with the corresponding
/// public key.
/// </summary>
/// <remarks>
/// The signer is a control-plane concern (it holds the private signing key), so it is injected on the add path — never
/// on a runner. A runner is given only the counterpart <see cref="IExecutorPackageVerifier"/>, whose vault/trust store
/// is deliberately separate from the signer's, so a compromised runner cannot sign.
/// </remarks>
public interface IExecutorPackageSigner
{
    /// <summary>Signs the manifest's exact UTF-8 bytes.</summary>
    /// <param name="manifestUtf8">The executor manifest as UTF-8 JSON (the package's <c>metadata/executor-manifest.json</c> bytes, signed verbatim).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The detached signature over <paramref name="manifestUtf8"/>.</returns>
    ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken);
}