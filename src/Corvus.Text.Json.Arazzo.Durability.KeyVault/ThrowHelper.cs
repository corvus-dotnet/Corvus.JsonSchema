// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Azure;
using Azure.Security.KeyVault.Keys.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault;

/// <summary>
/// Centralized exception-throwing helpers for the Azure Key Vault integration.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from a catch or a value-producing expression are <c>Get*Exception</c> factories the caller throws (so a definitely-assigned local stays assigned, which <see cref="DoesNotReturnAttribute"/> does not satisfy). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when the reference is not a <c>keyvault://</c> reference this resolver handles.</summary>
    /// <param name="reference">The secret reference.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSchemeMismatch(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.SchemeMismatch);

    /// <summary>Throws when a <c>keyvault://</c> reference does not have the required locator shape.</summary>
    /// <param name="reference">The secret reference.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowReferenceMalformed(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.ReferenceMalformed);

    /// <summary>Creates the exception for a failed Key Vault read, for the caller to throw.</summary>
    /// <param name="reference">The secret reference.</param>
    /// <param name="secretName">The secret name being read.</param>
    /// <param name="vaultUri">The vault URI.</param>
    /// <param name="inner">The Key Vault request failure.</param>
    /// <returns>The exception to throw.</returns>
    public static SecretResolutionException GetReadFailedException(SecretRef reference, string secretName, Uri vaultUri, RequestFailedException inner)
        => new(reference, SR.Format(SR.ReadFailed, inner.Status, secretName, vaultUri), inner);

    /// <summary>Creates the exception for a signing algorithm the verifier does not understand, for the caller to throw.</summary>
    /// <param name="algorithm">The unsupported algorithm.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetUnsupportedAlgorithmException(SignatureAlgorithm algorithm)
        => new(SR.Format(SR.UnsupportedAlgorithm, algorithm), nameof(algorithm));
}