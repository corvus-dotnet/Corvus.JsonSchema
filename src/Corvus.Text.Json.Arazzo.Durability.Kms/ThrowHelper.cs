// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Formats.Asn1;
using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

namespace Corvus.Text.Json.Arazzo.Durability.Kms;

/// <summary>
/// Centralized exception-throwing helpers for the AWS KMS integration.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from a catch or a value-producing expression are <c>Get*Exception</c> factories the caller throws (so a definitely-assigned local stays assigned, which <see cref="DoesNotReturnAttribute"/> does not satisfy). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Creates the exception for an ECDSA signature that is not a valid DER SEQUENCE, for the caller to throw.</summary>
    /// <param name="inner">The DER parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static CryptographicException GetInvalidDerSignatureException(AsnContentException inner)
        => new(SR.InvalidDerSignature, inner);

    /// <summary>Throws when an ECDSA signature component is negative.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNegativeSignatureComponent()
        => throw new CryptographicException(SR.NegativeSignatureComponent);

    /// <summary>Throws when an ECDSA signature component is larger than the curve's field size.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSignatureComponentTooLarge()
        => throw new CryptographicException(SR.SignatureComponentTooLarge);

    /// <summary>Throws when an ECDSA signature component could not be encoded.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowSignatureComponentEncodingFailed()
        => throw new CryptographicException(SR.SignatureComponentEncodingFailed);

    /// <summary>Creates the exception for a KMS signing algorithm the verifier does not understand, for the caller to throw.</summary>
    /// <param name="signingAlgorithm">The unsupported signing algorithm.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetUnsupportedSigningAlgorithmException(SigningAlgorithmSpec signingAlgorithm)
        => new(SR.Format(SR.UnsupportedSigningAlgorithm, signingAlgorithm.Value), nameof(signingAlgorithm));
}