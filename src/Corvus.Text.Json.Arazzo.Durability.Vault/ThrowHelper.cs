// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using VaultSharp.Core;

namespace Corvus.Text.Json.Arazzo.Durability.Vault;

/// <summary>
/// Centralized exception-throwing helpers for the HashiCorp Vault integration.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; the helper used from a catch is a <c>Get*Exception</c> factory the caller throws (so a local assigned in the try stays definitely assigned, which <see cref="DoesNotReturnAttribute"/> does not satisfy). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when the reference is not a <c>vault://</c> reference this resolver handles.</summary>
    /// <param name="reference">The secret reference.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSchemeMismatch(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.SchemeMismatch);

    /// <summary>Throws when a <c>vault://</c> reference does not have the required locator shape.</summary>
    /// <param name="reference">The secret reference.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowReferenceMalformed(SecretRef reference)
        => throw new SecretResolutionException(reference, SR.ReferenceMalformed);

    /// <summary>Throws when a <c>vault://</c> reference does not name the field to read.</summary>
    /// <param name="reference">The secret reference.</param>
    public static SecretResolutionException GetFieldRequiredException(SecretRef reference)
        => new(reference, SR.FieldRequired);

    /// <summary>Creates the exception for a failed Vault read, for the caller to throw.</summary>
    /// <param name="reference">The secret reference.</param>
    /// <param name="mount">The engine mount point.</param>
    /// <param name="path">The secret path.</param>
    /// <param name="inner">The Vault API failure.</param>
    /// <returns>The exception to throw.</returns>
    public static SecretResolutionException GetReadFailedException(SecretRef reference, string mount, string path, VaultApiException inner)
        => new(reference, SR.Format(SR.ReadFailed, inner.HttpStatusCode, mount, path), inner);

    /// <summary>Throws when the resolved Vault secret has no field with the requested name.</summary>
    /// <param name="reference">The secret reference.</param>
    /// <param name="mount">The engine mount point.</param>
    /// <param name="path">The secret path.</param>
    /// <param name="field">The requested field name.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowFieldMissing(SecretRef reference, string mount, string path, string field)
        => throw new SecretResolutionException(reference, SR.Format(SR.FieldMissing, mount, path, field));

    /// <summary>Throws when the signature algorithm is not one the executor-package verifier understands.</summary>
    /// <param name="algorithm">The unsupported algorithm.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsupportedAlgorithm(string algorithm)
        => throw new ArgumentException(SR.Format(SR.UnsupportedAlgorithm, ExecutorSignatureAlgorithms.EcdsaP256Sha256, ExecutorSignatureAlgorithms.EcdsaP384Sha384, ExecutorSignatureAlgorithms.RsaPssSha256, algorithm), nameof(algorithm));

    /// <summary>Throws when Vault returns an empty Transit signature.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowEmptyTransitSignature()
        => throw new InvalidOperationException(SR.EmptyTransitSignature);
}