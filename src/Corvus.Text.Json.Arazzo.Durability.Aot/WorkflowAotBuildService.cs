// <copyright file="WorkflowAotBuildService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The control-plane build service that mints a native executor binary for a runtime target from a version's
/// already-signed executor IL and attaches it to the version's package (ADR 0055). It verifies the executor package's
/// integrity and signature first, so the native binary is provably derived from the signed IL (the point of building
/// from the signed <c>executor.dll</c> rather than re-generated source), then assembles a thin serverless host-app
/// around that signed executor, native-AOT compiles it with the target's <see cref="IWorkflowAotBuilder"/>, and attaches
/// the resulting binary as the package's <c>metadata/native/&lt;rid&gt;</c> entry. Because a native binary is metadata,
/// the version's content hash is unchanged. The async publish state machine drives this per (version, target).
/// </summary>
public sealed class WorkflowAotBuildService
{
    private readonly IExecutorPackageVerifier verifier;
    private readonly IExecutorPackageSigner signer;
    private readonly IWorkflowAotBuilder builder;
    private readonly AotHostAppAssembler assembler;
    private readonly AotHostAppOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAotBuildService"/> class.
    /// </summary>
    /// <param name="verifier">The executor-package verifier used to check the signed manifest against a trusted key before building. The build service always verifies (ADR 0055), so this is required.</param>
    /// <param name="signer">The control-plane signer used to sign the native binary's attestation, so a deploy can verify the native binary's provenance. Signing the output is what closes the trust chain for the serverless path, so this is required.</param>
    /// <param name="builder">The Native-AOT builder for the runtime target this service builds for (e.g. a container builder for the Linux targets).</param>
    /// <param name="options">The feed and package versions the assembled host-app references.</param>
    public WorkflowAotBuildService(IExecutorPackageVerifier verifier, IExecutorPackageSigner signer, IWorkflowAotBuilder builder, AotHostAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        this.verifier = verifier;
        this.signer = signer;
        this.builder = builder;
        this.assembler = new AotHostAppAssembler();
        this.options = options;
    }

    /// <summary>
    /// Builds the native binary for <paramref name="runtimeIdentifier"/> from the signed executor in
    /// <paramref name="package"/> and, on success, returns the package with the binary attached under
    /// <c>metadata/native/&lt;rid&gt;</c> (the content hash is unchanged). A native-AOT compile failure is returned as a
    /// non-successful outcome carrying the build log (an AOT-cleanliness gap the caller records as <c>failed</c>); a
    /// missing, malformed, or unverified executor is a <see cref="WorkflowAotBuildException"/> (bad input the caller must
    /// not have produced).
    /// </summary>
    /// <param name="package">The version's canonical package, carrying the signed <c>executor.dll</c>, its manifest, and the detached signature.</param>
    /// <param name="runtimeIdentifier">The .NET runtime identifier to build for (e.g. <c>linux-x64</c>).</param>
    /// <param name="cancellationToken">A cancellation token, propagated to the builder.</param>
    /// <returns>The build outcome: on success the package with the native binary attached; otherwise the build log.</returns>
    /// <exception cref="WorkflowAotBuildException">The package carries no executor, the manifest is malformed, the assembly does not match the manifest, or the signature is missing or does not verify.</exception>
    public async ValueTask<WorkflowAotBuildOutcome> BuildAndAttachAsync(ReadOnlyMemory<byte> package, string runtimeIdentifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        if (!WorkflowPackage.TryReadEntry(package, "metadata/executor.dll"u8, out ReadOnlyMemory<byte> executorAssembly) || executorAssembly.IsEmpty)
        {
            throw new WorkflowAotBuildException("The package carries no compiled executor assembly to build.");
        }

        if (!WorkflowPackage.TryReadEntry(package, "metadata/executor-manifest.json"u8, out ReadOnlyMemory<byte> manifestUtf8) || manifestUtf8.IsEmpty)
        {
            throw new WorkflowAotBuildException("The package carries no executor manifest.");
        }

        // Verify integrity and signature before building: the native binary must be provably derived from the signed IL.
        WorkflowExecutorManifest manifest = VerifyExecutor(package, executorAssembly, manifestUtf8);

        // Assemble the thin host-app around the signed executor for the target, then native-AOT compile it.
        AssembledHostApp hostApp = this.assembler.Assemble(executorAssembly, manifestUtf8, runtimeIdentifier, this.options);
        AotBuildResult result = await this.builder.BuildAsync(hostApp, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return WorkflowAotBuildOutcome.Failure(runtimeIdentifier, result.Log);
        }

        // Sign the native binary while it is still inside this trusted build operation (the smallest trust window): an
        // attestation binding it to this version, target, and engine version, so a deploy verifies its provenance and a
        // swapped binary is detected. The engine version is present because the assembler above rejects a manifest without it.
        var attestation = new NativeArtifactAttestation(
            NativeArtifactAttestation.CurrentFormatVersion,
            manifest.PackageHash,
            runtimeIdentifier,
            manifest.EngineVersion!,
            NativeArtifactAttestation.ComputeDigest(result.NativeBinary.Span));
        byte[] attestationManifest = attestation.ToUtf8();
        ExecutorPackageSignature signature = await this.signer.SignAsync(attestationManifest, cancellationToken).ConfigureAwait(false);

        // Attach the binary and its attestation; both are metadata (ADR 0055), so the content hash is unchanged.
        byte[] attached = WorkflowPackage.AttachSignedNativeArtifact(package, runtimeIdentifier, result.NativeBinary, attestationManifest, signature.ToUtf8());
        return WorkflowAotBuildOutcome.Success(runtimeIdentifier, attached, result.Log);
    }

    /// <summary>
    /// Verifies a native binary's attestation before a deploy, the counterpart to the build-time input check. The
    /// package carries a native binary and its detached attestation for the target, the attestation's digest matches the
    /// binary's bytes, its target and its version (content hash) match, and its signature verifies against a trusted key.
    /// This is what a deploy pipeline calls before it hands the binary to the function platform, so a binary swapped in
    /// storage or transit is caught (ADR 0055).
    /// </summary>
    /// <param name="package">The version's package, carrying the native binary and its attestation.</param>
    /// <param name="runtimeIdentifier">The runtime target being deployed.</param>
    /// <param name="verifier">The trust store the deploy verifies the attestation against.</param>
    /// <returns>The verified attestation.</returns>
    /// <exception cref="WorkflowAotBuildException">The native binary or attestation is missing or malformed, the digest, target, or version does not match, or the signature does not verify.</exception>
    public static NativeArtifactAttestation VerifyNativeArtifact(ReadOnlyMemory<byte> package, string runtimeIdentifier, IExecutorPackageVerifier verifier)
    {
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(verifier);

        if (!WorkflowPackage.TryReadNativeArtifact(package, runtimeIdentifier, out ReadOnlyMemory<byte> nativeBinary) || nativeBinary.IsEmpty)
        {
            throw new WorkflowAotBuildException($"The package carries no native binary for '{runtimeIdentifier}'.");
        }

        if (!WorkflowPackage.TryReadNativeAttestation(package, runtimeIdentifier, out ReadOnlyMemory<byte> attestationManifest, out ReadOnlyMemory<byte> signatureUtf8))
        {
            throw new WorkflowAotBuildException($"The native binary for '{runtimeIdentifier}' is unsigned; refusing to deploy an unattested binary.");
        }

        NativeArtifactAttestation attestation;
        try
        {
            attestation = NativeArtifactAttestation.Parse(attestationManifest);
        }
        catch (FormatException ex)
        {
            throw new WorkflowAotBuildException($"The native artifact attestation is malformed: {ex.Message}", ex);
        }

        if (!string.Equals(attestation.RuntimeIdentifier, runtimeIdentifier, StringComparison.Ordinal))
        {
            throw new WorkflowAotBuildException($"The native artifact attestation targets '{attestation.RuntimeIdentifier}', not the requested '{runtimeIdentifier}'.");
        }

        string actualDigest = NativeArtifactAttestation.ComputeDigest(nativeBinary.Span);
        if (!string.Equals(attestation.NativeDigest, actualDigest, StringComparison.Ordinal))
        {
            throw new WorkflowAotBuildException($"The native binary digest '{actualDigest}' does not match the attestation's '{attestation.NativeDigest}'.");
        }

        string packageHash = CatalogPackage.HashCanonical(package);
        if (!string.Equals(attestation.PackageHash, packageHash, StringComparison.Ordinal))
        {
            throw new WorkflowAotBuildException($"The native artifact attestation's package hash '{attestation.PackageHash}' does not match the version's content hash '{packageHash}'.");
        }

        ExecutorPackageSignature signature;
        try
        {
            signature = ExecutorPackageSignature.Parse(signatureUtf8);
        }
        catch (FormatException ex)
        {
            throw new WorkflowAotBuildException($"The native artifact signature is malformed: {ex.Message}", ex);
        }

        if (!verifier.Verify(attestationManifest, signature))
        {
            throw new WorkflowAotBuildException("The native artifact signature did not verify against a trusted key; refusing to deploy.");
        }

        return attestation;
    }

    // The trust chain (mirrors WorkflowExecutorLoader's load-time verification, applied before an AOT compile instead of a
    // load): the manifest parses, the assembly's digest matches the manifest it is signed under, the package is signed,
    // and the signature verifies against a trusted key. Any failure refuses the build. Returns the parsed manifest so the
    // caller can bind the native attestation to the same package hash and engine version.
    private WorkflowExecutorManifest VerifyExecutor(ReadOnlyMemory<byte> package, ReadOnlyMemory<byte> executorAssembly, ReadOnlyMemory<byte> manifestUtf8)
    {
        WorkflowExecutorManifest manifest;
        try
        {
            manifest = WorkflowExecutorManifest.Parse(manifestUtf8);
        }
        catch (FormatException ex)
        {
            throw new WorkflowAotBuildException($"The executor manifest is malformed: {ex.Message}", ex);
        }

        string actualDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(executorAssembly.Span));
        if (!string.Equals(manifest.AssemblyDigest, actualDigest, StringComparison.Ordinal))
        {
            throw new WorkflowAotBuildException(
                $"The executor assembly digest '{actualDigest}' does not match the manifest's '{manifest.AssemblyDigest}'.");
        }

        if (!WorkflowPackage.TryReadEntry(package, "metadata/executor-manifest.sig"u8, out ReadOnlyMemory<byte> signatureUtf8) || signatureUtf8.IsEmpty)
        {
            throw new WorkflowAotBuildException("The executor package is unsigned; the AOT builder refuses to build from an unsigned executor.");
        }

        ExecutorPackageSignature signature;
        try
        {
            signature = ExecutorPackageSignature.Parse(signatureUtf8);
        }
        catch (FormatException ex)
        {
            throw new WorkflowAotBuildException($"The executor signature is malformed: {ex.Message}", ex);
        }

        if (!this.verifier.Verify(manifestUtf8, signature))
        {
            throw new WorkflowAotBuildException("The executor signature did not verify against a trusted key; refusing to build.");
        }

        return manifest;
    }
}

/// <summary>
/// The outcome of a <see cref="WorkflowAotBuildService"/> build for one runtime target: on success the version's package
/// with the native binary attached, otherwise the build log for diagnosis.
/// </summary>
public readonly record struct WorkflowAotBuildOutcome
{
    private WorkflowAotBuildOutcome(bool succeeded, string runtimeIdentifier, ReadOnlyMemory<byte> package, string log)
    {
        this.Succeeded = succeeded;
        this.RuntimeIdentifier = runtimeIdentifier;
        this.Package = package;
        this.Log = log;
    }

    /// <summary>Gets a value indicating whether the native binary was built and attached.</summary>
    public bool Succeeded { get; }

    /// <summary>Gets the runtime identifier this outcome is for.</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>Gets the version's package with the native binary attached (<c>metadata/native/&lt;rid&gt;</c>) when <see cref="Succeeded"/> is <see langword="true"/>; otherwise empty.</summary>
    public ReadOnlyMemory<byte> Package { get; }

    /// <summary>Gets the build log (compiler and linker output), for diagnostics whether the build succeeded or failed.</summary>
    public string Log { get; }

    /// <summary>Creates a successful outcome carrying the package with the native binary attached.</summary>
    /// <param name="runtimeIdentifier">The runtime identifier built.</param>
    /// <param name="package">The package with the native binary attached.</param>
    /// <param name="log">The build log.</param>
    /// <returns>The outcome.</returns>
    public static WorkflowAotBuildOutcome Success(string runtimeIdentifier, ReadOnlyMemory<byte> package, string log)
        => new(true, runtimeIdentifier, package, log);

    /// <summary>Creates a non-successful outcome carrying the build log that explains the native-AOT compile failure.</summary>
    /// <param name="runtimeIdentifier">The runtime identifier attempted.</param>
    /// <param name="log">The build log explaining the failure.</param>
    /// <returns>The outcome.</returns>
    public static WorkflowAotBuildOutcome Failure(string runtimeIdentifier, string log)
        => new(false, runtimeIdentifier, default, log);
}

/// <summary>
/// Thrown when the AOT build service cannot build a version's native binary because of bad input: a missing or malformed
/// executor, an assembly that does not match its manifest, or a missing or invalid signature. A native-AOT compile
/// failure is not this exception; it is a non-successful <see cref="WorkflowAotBuildOutcome"/> carrying the build log.
/// </summary>
public sealed class WorkflowAotBuildException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="WorkflowAotBuildException"/> class.</summary>
    /// <param name="message">The message describing why the build was refused.</param>
    public WorkflowAotBuildException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="WorkflowAotBuildException"/> class.</summary>
    /// <param name="message">The message describing why the build was refused.</param>
    /// <param name="innerException">The underlying cause.</param>
    public WorkflowAotBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}