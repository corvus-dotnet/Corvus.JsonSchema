// <copyright file="LambdaDeployerOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;

/// <summary>
/// The options a <see cref="LambdaServerlessDeployer"/> needs beyond the injected <c>IAmazonLambda</c> client: the
/// function's execution role and its size/time limits. The runner supplies these from the environment's configuration;
/// the deployer holds no cloud identity of its own (ADR 0059, the runner is the secure boundary).
/// </summary>
public sealed record LambdaDeployerOptions
{
    /// <summary>
    /// Gets the ARN of the IAM execution role the created function assumes. Against LocalStack a dummy value such as
    /// <c>arn:aws:iam::000000000000:role/lambda-role</c> is accepted; against real AWS this is a real, least-privileged
    /// execution role.
    /// </summary>
    public required string ExecutionRoleArn { get; init; }

    /// <summary>Gets the function's memory allocation in megabytes. Defaults to 512.</summary>
    public int MemorySizeMb { get; init; } = 512;

    /// <summary>Gets the function's invocation timeout in seconds. Defaults to 30.</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Gets the environment variables set on the created function — the deployed environment's source configuration the
    /// baked function's transport binder reads (each <c>ARAZZO_SOURCE__&lt;name&gt;</c> is a source's base URL). Empty by
    /// default; the runner supplies it from the environment's source registry (ADR 0059, the runner holds the config).
    /// </summary>
    public System.Collections.Generic.IReadOnlyDictionary<string, string>? FunctionEnvironment { get; init; }
}