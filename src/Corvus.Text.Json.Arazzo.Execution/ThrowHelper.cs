// <copyright file="ThrowHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// Centralized exception-throwing helpers for the runner-side executor loader and package verification.
/// </summary>
/// <remarks>
/// <para>
/// Guard-position helpers are <c>Throw*</c> methods marked <see cref="DoesNotReturnAttribute"/> so the JIT can optimize call-site code after a throw; helpers used from a <c>catch</c> (where a local assigned in the <c>try</c> stays definitely assigned), from a <c>??</c>/ternary, or before a use of a pattern variable are <c>Get*Exception</c> factories the caller throws. All exception messages come from the embedded <c>Resources/Strings.resx</c> resource file via <c>SR</c>.
/// </para>
/// </remarks>
internal static class ThrowHelper
{
    /// <summary>Throws when a native artifact attestation is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowNativeAttestationNotJsonObject()
        => throw new FormatException(SR.NativeAttestationNotJsonObject);

    /// <summary>Creates the exception for a native attestation missing a required string property, for the caller to throw.</summary>
    /// <param name="propertyName">The missing property's name.</param>
    /// <returns>The exception to throw.</returns>
    public static FormatException GetNativeAttestationMissingStringPropertyException(string propertyName)
        => new(SR.Format(SR.NativeAttestationMissingStringProperty, propertyName));

    /// <summary>Creates the exception for a native attestation missing a required integer property, for the caller to throw.</summary>
    /// <param name="propertyName">The missing property's name.</param>
    /// <returns>The exception to throw.</returns>
    public static FormatException GetNativeAttestationMissingIntegerPropertyException(string propertyName)
        => new(SR.Format(SR.NativeAttestationMissingIntegerProperty, propertyName));

    /// <summary>Throws when an executor manifest is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorManifestNotJsonObject()
        => throw new FormatException(SR.ExecutorManifestNotJsonObject);

    /// <summary>Creates the exception for an executor manifest missing a required string property, for the caller to throw.</summary>
    /// <param name="propertyName">The missing property's name.</param>
    /// <returns>The exception to throw.</returns>
    public static FormatException GetExecutorManifestMissingStringPropertyException(string propertyName)
        => new(SR.Format(SR.ExecutorManifestMissingStringProperty, propertyName));

    /// <summary>Creates the exception for an executor manifest missing a required integer property, for the caller to throw.</summary>
    /// <param name="propertyName">The missing property's name.</param>
    /// <returns>The exception to throw.</returns>
    public static FormatException GetExecutorManifestMissingIntegerPropertyException(string propertyName)
        => new(SR.Format(SR.ExecutorManifestMissingIntegerProperty, propertyName));

    /// <summary>Throws when an executor signature is not a JSON object.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorSignatureNotJsonObject()
        => throw new FormatException(SR.ExecutorSignatureNotJsonObject);

    /// <summary>Throws when an executor signature's value is not valid base64.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorSignatureValueNotBase64()
        => throw new FormatException(SR.ExecutorSignatureValueNotBase64);

    /// <summary>Creates the exception for an executor signature missing a required string property, for the caller to throw.</summary>
    /// <param name="propertyName">The missing property's name.</param>
    /// <returns>The exception to throw.</returns>
    public static FormatException GetExecutorSignatureMissingStringPropertyException(string propertyName)
        => new(SR.Format(SR.ExecutorSignatureMissingStringProperty, propertyName));

    /// <summary>Creates the exception for a trusted public key that is neither ECDSA nor RSA, for the caller to throw.</summary>
    /// <param name="keyId">The trusted key's identifier.</param>
    /// <param name="paramName">The offending parameter's name.</param>
    /// <param name="inner">The underlying import failure.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetTrustedPublicKeyNotAsymmetricException(string keyId, string paramName, ArgumentException inner)
        => new(SR.Format(SR.TrustedPublicKeyNotAsymmetric, keyId), paramName, inner);

    /// <summary>Creates the exception for an ECDSA key whose curve size is unsupported, for the caller to throw.</summary>
    /// <param name="keySize">The key's size, in bits.</param>
    /// <param name="paramName">The offending parameter's name.</param>
    /// <returns>The exception to throw.</returns>
    public static ArgumentException GetUnsupportedEcdsaKeySizeException(int keySize, string paramName)
        => new(SR.Format(SR.UnsupportedEcdsaKeySize, keySize), paramName);

    /// <summary>Creates the exception for a malformed executor manifest, for the caller to throw.</summary>
    /// <param name="inner">The parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowExecutorLoadException GetMalformedExecutorManifestException(FormatException inner)
        => new(SR.Format(SR.MalformedExecutorManifest, inner.Message), inner);

    /// <summary>Throws when the executor manifest's package hash does not match the version's content hash.</summary>
    /// <param name="manifestPackageHash">The package hash recorded in the manifest.</param>
    /// <param name="expectedPackageHash">The version's expected content hash.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorManifestPackageHashMismatch(string manifestPackageHash, string expectedPackageHash)
        => throw new WorkflowExecutorLoadException(SR.Format(SR.ExecutorManifestPackageHashMismatch, manifestPackageHash, expectedPackageHash));

    /// <summary>Throws when the executor assembly's digest does not match the manifest's.</summary>
    /// <param name="actualDigest">The digest computed over the executor assembly.</param>
    /// <param name="manifestDigest">The digest recorded in the manifest.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorAssemblyDigestMismatch(string actualDigest, string manifestDigest)
        => throw new WorkflowExecutorLoadException(SR.Format(SR.ExecutorAssemblyDigestMismatch, actualDigest, manifestDigest));

    /// <summary>Throws when the executor targets a framework this runner cannot load.</summary>
    /// <param name="manifestTargetFramework">The framework the executor targets.</param>
    /// <param name="supportedTargetFramework">The framework this runner supports.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorTargetFrameworkMismatch(string manifestTargetFramework, string supportedTargetFramework)
        => throw new WorkflowExecutorLoadException(SR.Format(SR.ExecutorTargetFrameworkMismatch, manifestTargetFramework, supportedTargetFramework));

    /// <summary>Throws when the executor package is unsigned but the runner requires a signature.</summary>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUnsignedExecutorPackage()
        => throw new WorkflowExecutorLoadException(SR.UnsignedExecutorPackage);

    /// <summary>Creates the exception for a malformed executor signature, for the caller to throw.</summary>
    /// <param name="inner">The parse failure.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowExecutorLoadException GetMalformedExecutorSignatureException(FormatException inner)
        => new(SR.Format(SR.MalformedExecutorSignature, inner.Message), inner);

    /// <summary>Throws when the executor manifest's signature does not verify against a trusted key.</summary>
    /// <param name="keyId">The signing key's identifier.</param>
    /// <param name="algorithm">The signature algorithm.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowExecutorManifestSignatureUntrusted(string keyId, string algorithm)
        => throw new WorkflowExecutorLoadException(SR.Format(SR.ExecutorManifestSignatureUntrusted, keyId, algorithm));

    /// <summary>Creates the exception for a manifest entry type not found in the assembly, for the caller to throw.</summary>
    /// <param name="entryType">The entry type the manifest names.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowExecutorLoadException GetEntryTypeNotFoundException(string entryType)
        => new(SR.Format(SR.EntryTypeNotFound, entryType));

    /// <summary>Creates the exception for an entry type that does not implement IHostedWorkflow, for the caller to throw.</summary>
    /// <param name="entryType">The entry type the manifest names.</param>
    /// <returns>The exception to throw.</returns>
    public static WorkflowExecutorLoadException GetEntryTypeNotHostedWorkflowException(string entryType)
        => new(SR.Format(SR.EntryTypeNotHostedWorkflow, entryType));

    /// <summary>Throws when the manifest does not declare a source the workflow requires.</summary>
    /// <param name="required">The required source name that is undeclared.</param>
    [DoesNotReturn]
    [StackTraceHidden]
    public static void ThrowUndeclaredSource(string required)
        => throw new WorkflowExecutorLoadException(SR.Format(SR.UndeclaredSource, required));
}