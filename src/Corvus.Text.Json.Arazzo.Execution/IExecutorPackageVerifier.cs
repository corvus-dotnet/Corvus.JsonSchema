// <copyright file="IExecutorPackageVerifier.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// Verifies a detached executor-package signature at load time (design §3.3, §12) against a <em>public</em> key the
/// runner holds locally in its own trust store — a store deliberately separate from the control-plane vault that holds
/// the private signing key. Verification is therefore a local operation with no call to the signing vault, so the
/// runner's credentials grant it no ability to sign.
/// </summary>
public interface IExecutorPackageVerifier
{
    /// <summary>Verifies that <paramref name="signature"/> is a valid signature over <paramref name="manifestUtf8"/> by a trusted key.</summary>
    /// <param name="manifestUtf8">The executor manifest as UTF-8 JSON (the exact bytes that were signed).</param>
    /// <param name="signature">The detached signature to check.</param>
    /// <returns><see langword="true"/> if the signature verifies against a trusted public key; <see langword="false"/> if the key id is not trusted, the algorithm is unsupported, or the signature does not verify.</returns>
    bool Verify(ReadOnlyMemory<byte> manifestUtf8, in ExecutorPackageSignature signature);
}