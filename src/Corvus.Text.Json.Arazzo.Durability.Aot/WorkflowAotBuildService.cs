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
    private readonly IWorkflowAotBuilder builder;
    private readonly AotHostAppAssembler assembler;
    private readonly AotHostAppOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAotBuildService"/> class.
    /// </summary>
    /// <param name="verifier">The executor-package verifier used to check the signed manifest against a trusted key before building. The build service always verifies (ADR 0055), so this is required.</param>
    /// <param name="builder">The Native-AOT builder for the runtime target this service builds for (e.g. a container builder for the Linux targets).</param>
    /// <param name="options">The feed and package versions the assembled host-app references.</param>
    public WorkflowAotBuildService(IExecutorPackageVerifier verifier, IWorkflowAotBuilder builder, AotHostAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        this.verifier = verifier;
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
        VerifyExecutor(package, executorAssembly, manifestUtf8);

        // Assemble the thin host-app around the signed executor for the target, then native-AOT compile it.
        AssembledHostApp hostApp = this.assembler.Assemble(executorAssembly, manifestUtf8, runtimeIdentifier, this.options);
        AotBuildResult result = await this.builder.BuildAsync(hostApp, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return WorkflowAotBuildOutcome.Failure(runtimeIdentifier, result.Log);
        }

        // Attach the native binary; a native binary is metadata (ADR 0055), so the content hash is unchanged.
        byte[] attached = WorkflowPackage.AttachNativeArtifact(package, runtimeIdentifier, result.NativeBinary);
        return WorkflowAotBuildOutcome.Success(runtimeIdentifier, attached, result.Log);
    }

    // The trust chain (mirrors WorkflowExecutorLoader's load-time verification, applied before an AOT compile instead of a
    // load): the manifest parses, the assembly's digest matches the manifest it is signed under, the package is signed,
    // and the signature verifies against a trusted key. Any failure refuses the build.
    private void VerifyExecutor(ReadOnlyMemory<byte> package, ReadOnlyMemory<byte> executorAssembly, ReadOnlyMemory<byte> manifestUtf8)
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