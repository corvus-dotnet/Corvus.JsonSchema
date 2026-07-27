// <copyright file="IServerlessDeployer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// Deploys a version's verified native binary to a function platform for one runtime target and returns the deployed
/// function's invoke URL (ADR 0055, ADR 0059). The implementation acts in the user's cloud account with the runner's
/// identity, so it runs runner-side: the runner holds the environment's credentials and is the secure boundary, and the
/// control plane holds no user cloud secrets. A local emulator implementation stands in for a real platform in tests,
/// and each target platform (AWS Lambda, later Azure Functions) has its own deployer.
/// </summary>
public interface IServerlessDeployer
{
    /// <summary>
    /// Deploys the request's native binary to the function platform and returns the deployed function's invoke URL.
    /// </summary>
    /// <param name="request">The deploy request: the target tuple and the verified native binary to deploy.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The deploy result: the function URL on success, and the deploy log either way.</returns>
    ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// A request to deploy a version's native binary to a function platform for one target (ADR 0055). The binary has
/// already been verified against the trust store by the caller (<see cref="WorkflowDeployService"/>), so a deployer
/// hands it to the platform without re-verifying.
/// </summary>
/// <param name="BaseWorkflowId">The base workflow id whose version is being deployed.</param>
/// <param name="VersionNumber">The version number being deployed.</param>
/// <param name="Environment">The target environment the function serves (per-(env, version) baked).</param>
/// <param name="RuntimeIdentifier">The .NET runtime identifier the native binary targets (e.g. <c>linux-x64</c>).</param>
/// <param name="NativeBinary">The verified native binary bytes to deploy.</param>
public readonly record struct ServerlessDeployRequest(
    string BaseWorkflowId,
    int VersionNumber,
    string Environment,
    string RuntimeIdentifier,
    ReadOnlyMemory<byte> NativeBinary);

/// <summary>The outcome of a serverless deploy.</summary>
/// <param name="Succeeded">Whether the deploy produced a live function.</param>
/// <param name="FunctionUrl">The deployed function's invoke URL when <paramref name="Succeeded"/> is <see langword="true"/>; otherwise empty.</param>
/// <param name="Log">The deploy log, for diagnostics whether the deploy succeeded or failed.</param>
public readonly record struct ServerlessDeployResult(bool Succeeded, string FunctionUrl, string Log)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="functionUrl">The deployed function's invoke URL.</param>
    /// <param name="log">The deploy log.</param>
    /// <returns>The result.</returns>
    public static ServerlessDeployResult Success(string functionUrl, string log) => new(true, functionUrl, log);

    /// <summary>Creates a failed result.</summary>
    /// <param name="log">The deploy log explaining the failure.</param>
    /// <returns>The result.</returns>
    public static ServerlessDeployResult Failure(string log) => new(false, string.Empty, log);
}