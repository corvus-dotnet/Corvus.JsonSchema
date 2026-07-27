// <copyright file="WorkflowDeployService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// The runner-side deploy service that verifies a version's signed native binary and hands it to a function platform
/// (ADR 0055, ADR 0059). The runner is the secure boundary: it holds the environment's credentials, and the control
/// plane holds no user cloud secrets, so this runs runner-side. The service verifies the native binary's attestation
/// against the trust store before the binary enters the user's cloud account, reads the verified binary from the
/// package, and deploys it with the target's <see cref="IServerlessDeployer"/>, returning the deployed function's invoke
/// URL. It does not write the deployment record; the runner's deploy worker records the outcome into the
/// <c>WorkflowDeployment</c> store, and the control-plane dispatch gate reads it.
/// </summary>
public sealed class WorkflowDeployService
{
    private readonly IExecutorPackageVerifier verifier;
    private readonly IServerlessDeployer deployer;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowDeployService"/> class.
    /// </summary>
    /// <param name="verifier">The executor-package verifier the runner checks the native binary's attestation against before deploying. The service always verifies (ADR 0055), so this is required.</param>
    /// <param name="deployer">The deployer for the target platform (a container-emulator deployer in tests, an AWS Lambda deployer in production).</param>
    public WorkflowDeployService(IExecutorPackageVerifier verifier, IServerlessDeployer deployer)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(deployer);
        this.verifier = verifier;
        this.deployer = deployer;
    }

    /// <summary>
    /// Verifies the native binary for <paramref name="runtimeIdentifier"/> in <paramref name="package"/> and deploys it
    /// to the function platform, returning the deployed function's invoke URL on success. A deploy failure is returned
    /// as a non-successful outcome carrying the deploy log; a missing, unsigned, or tampered native binary is a
    /// <see cref="WorkflowAotBuildException"/> (bad input the runner must not deploy).
    /// </summary>
    /// <param name="package">The version's package, carrying the signed native binary and its attestation for the target.</param>
    /// <param name="baseWorkflowId">The base workflow id whose version is being deployed.</param>
    /// <param name="versionNumber">The version number being deployed.</param>
    /// <param name="environment">The target environment the function serves.</param>
    /// <param name="runtimeIdentifier">The .NET runtime identifier the native binary targets (e.g. <c>linux-x64</c>).</param>
    /// <param name="cancellationToken">A cancellation token, propagated to the deployer.</param>
    /// <returns>The deploy outcome: on success the deployed function's invoke URL; otherwise the deploy log.</returns>
    /// <exception cref="WorkflowAotBuildException">The package carries no native binary or attestation for the target, the attestation is malformed, the binary's digest, target, or version does not match, or the signature does not verify.</exception>
    public async ValueTask<WorkflowDeployOutcome> DeployAsync(ReadOnlyMemory<byte> package, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // Verify the native binary's attestation before it enters the user's cloud account (ADR 0059): the digest,
        // target, and version bind must match and the signature must verify against the trust store. A bad binary
        // throws, so the runner never deploys an unverified or swapped binary.
        WorkflowAotBuildService.VerifyNativeArtifact(package, runtimeIdentifier, this.verifier);

        // Read the verified binary. Verification above guarantees it is present, so this cannot fail; the guard keeps
        // the compiler's definite-assignment happy and documents the invariant.
        if (!WorkflowPackage.TryReadNativeArtifact(package, runtimeIdentifier, out ReadOnlyMemory<byte> nativeBinary) || nativeBinary.IsEmpty)
        {
            ThrowHelper.ThrowNoNativeBinary(runtimeIdentifier);
        }

        var request = new ServerlessDeployRequest(baseWorkflowId, versionNumber, environment, runtimeIdentifier, nativeBinary);
        ServerlessDeployResult result = await this.deployer.DeployAsync(request, cancellationToken).ConfigureAwait(false);
        return result.Succeeded
            ? WorkflowDeployOutcome.Success(runtimeIdentifier, result.FunctionUrl, result.Log)
            : WorkflowDeployOutcome.Failure(runtimeIdentifier, result.Log);
    }
}

/// <summary>The outcome of a deploy: on success the deployed function's invoke URL; otherwise the deploy log.</summary>
/// <param name="Succeeded">Whether the deploy produced a live function.</param>
/// <param name="RuntimeIdentifier">The runtime target that was deployed.</param>
/// <param name="FunctionUrl">The deployed function's invoke URL when <paramref name="Succeeded"/> is <see langword="true"/>; otherwise empty.</param>
/// <param name="Log">The deploy log, for diagnostics whether the deploy succeeded or failed.</param>
public readonly record struct WorkflowDeployOutcome(bool Succeeded, string RuntimeIdentifier, string FunctionUrl, string Log)
{
    /// <summary>Creates a successful outcome.</summary>
    /// <param name="runtimeIdentifier">The runtime target that was deployed.</param>
    /// <param name="functionUrl">The deployed function's invoke URL.</param>
    /// <param name="log">The deploy log.</param>
    /// <returns>The outcome.</returns>
    public static WorkflowDeployOutcome Success(string runtimeIdentifier, string functionUrl, string log) => new(true, runtimeIdentifier, functionUrl, log);

    /// <summary>Creates a failed outcome.</summary>
    /// <param name="runtimeIdentifier">The runtime target that was attempted.</param>
    /// <param name="log">The deploy log explaining the failure.</param>
    /// <returns>The outcome.</returns>
    public static WorkflowDeployOutcome Failure(string runtimeIdentifier, string log) => new(false, runtimeIdentifier, string.Empty, log);
}