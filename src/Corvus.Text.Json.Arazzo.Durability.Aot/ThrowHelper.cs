// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// Centralized exception-throwing helpers for the Native-AOT build service.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; parse-failure helpers used from a catch are <c>Get*Exception</c> factories the caller throws (so the local assigned in the try stays definitely assigned). All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when the package carries no compiled executor assembly to build.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoExecutorAssembly()
        => throw new WorkflowAotBuildException(SR.NoExecutorAssembly);

    /// <summary>Throws when the package carries no executor manifest.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoExecutorManifest()
        => throw new WorkflowAotBuildException(SR.NoExecutorManifest);

    /// <summary>Throws when the package carries no native binary for the target.</summary>
    /// <param name="runtimeIdentifier">The runtime identifier the binary is missing for.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNoNativeBinary(string runtimeIdentifier)
        => throw new WorkflowAotBuildException(SR.Format(SR.NoNativeBinary, runtimeIdentifier));

    /// <summary>Throws when the target's native binary is present but unsigned.</summary>
    /// <param name="runtimeIdentifier">The runtime identifier of the unsigned binary.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsignedNativeBinary(string runtimeIdentifier)
        => throw new WorkflowAotBuildException(SR.Format(SR.UnsignedNativeBinary, runtimeIdentifier));

    /// <summary>Creates the exception for a native artifact attestation that cannot be parsed, for the caller to throw.</summary>
    /// <param name="inner">The parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowAotBuildException GetMalformedNativeAttestationException(FormatException inner)
        => new(SR.Format(SR.MalformedNativeAttestation, inner.Message), inner);

    /// <summary>Throws when the attestation targets a different runtime than the one requested.</summary>
    /// <param name="attestationRuntimeIdentifier">The runtime the attestation targets.</param>
    /// <param name="requestedRuntimeIdentifier">The runtime that was requested.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAttestationRuntimeMismatch(string attestationRuntimeIdentifier, string requestedRuntimeIdentifier)
        => throw new WorkflowAotBuildException(SR.Format(SR.AttestationRuntimeMismatch, attestationRuntimeIdentifier, requestedRuntimeIdentifier));

    /// <summary>Throws when the native binary's digest does not match the attestation's.</summary>
    /// <param name="actualDigest">The digest computed over the binary.</param>
    /// <param name="attestationDigest">The digest recorded in the attestation.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeDigestMismatch(string actualDigest, string attestationDigest)
        => throw new WorkflowAotBuildException(SR.Format(SR.NativeDigestMismatch, actualDigest, attestationDigest));

    /// <summary>Throws when the attestation's package hash does not match the version's content hash.</summary>
    /// <param name="attestationPackageHash">The package hash recorded in the attestation.</param>
    /// <param name="versionPackageHash">The version's actual content hash.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowAttestationPackageHashMismatch(string attestationPackageHash, string versionPackageHash)
        => throw new WorkflowAotBuildException(SR.Format(SR.AttestationPackageHashMismatch, attestationPackageHash, versionPackageHash));

    /// <summary>Creates the exception for a native artifact signature that cannot be parsed, for the caller to throw.</summary>
    /// <param name="inner">The parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowAotBuildException GetMalformedNativeSignatureException(FormatException inner)
        => new(SR.Format(SR.MalformedNativeSignature, inner.Message), inner);

    /// <summary>Throws when a native artifact signature does not verify against a trusted key.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeSignatureUntrusted()
        => throw new WorkflowAotBuildException(SR.NativeSignatureUntrusted);

    /// <summary>Creates the exception for an executor manifest that cannot be parsed, for the caller to throw.</summary>
    /// <param name="inner">The parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowAotBuildException GetMalformedExecutorManifestException(FormatException inner)
        => new(SR.Format(SR.MalformedExecutorManifest, inner.Message), inner);

    /// <summary>Throws when the executor assembly's digest does not match the manifest's.</summary>
    /// <param name="actualDigest">The digest computed over the executor assembly.</param>
    /// <param name="manifestDigest">The digest recorded in the manifest.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorAssemblyDigestMismatch(string actualDigest, string manifestDigest)
        => throw new WorkflowAotBuildException(SR.Format(SR.ExecutorAssemblyDigestMismatch, actualDigest, manifestDigest));

    /// <summary>Throws when the executor package is unsigned.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsignedExecutorPackage()
        => throw new WorkflowAotBuildException(SR.UnsignedExecutorPackage);

    /// <summary>Creates the exception for an executor signature that cannot be parsed, for the caller to throw.</summary>
    /// <param name="inner">The parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowAotBuildException GetMalformedExecutorSignatureException(FormatException inner)
        => new(SR.Format(SR.MalformedExecutorSignature, inner.Message), inner);

    /// <summary>Throws when an executor signature does not verify against a trusted key.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorSignatureUntrusted()
        => throw new WorkflowAotBuildException(SR.ExecutorSignatureUntrusted);
}