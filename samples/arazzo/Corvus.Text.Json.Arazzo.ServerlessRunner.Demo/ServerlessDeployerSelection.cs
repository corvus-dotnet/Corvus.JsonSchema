// <copyright file="ServerlessDeployerSelection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.Lambda;
using Amazon.Runtime;
using Azure.Identity;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;
using Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;

namespace Corvus.Text.Json.Arazzo.ServerlessRunner.Demo;

/// <summary>
/// Constructs the serverless runner's platform deployer from configuration — deployer selection is a host-wiring
/// concern (ADR 0061): the same deploy worker, verification, and queue drive whichever <see cref="IServerlessDeployer"/>
/// the host constructs, and the runner supplies the cloud identity (ADR 0059).
/// </summary>
/// <remarks>
/// <c>Runner:Serverless:Platform</c> selects the target: <c>lambda</c> (the default; LocalStack in the demo AppHost,
/// real AWS in production — only the endpoint and credentials differ, ADR 0060) or <c>azure-flex</c> (a real Flex
/// Consumption Function App deployed by One Deploy with an ARM AAD bearer token, ADR 0061 amendment — there is no
/// local Azure management-plane emulator, so this platform always targets real Azure). Each platform reads only its
/// own configuration keys, so a Lambda host needs no Azure settings and vice versa.
/// </remarks>
public static class ServerlessDeployerSelection
{
    /// <summary>Builds the configured platform's deployer.</summary>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="functionSourceEnv">The deployed environment's source settings (<c>ARAZZO_SOURCE__&lt;name&gt;</c> →
    /// base URL) stamped onto each deployed function, which the baked transport binder reads (ADR 0055).</param>
    /// <returns>The platform deployer.</returns>
    /// <exception cref="InvalidOperationException">The platform is unknown, or its required configuration is missing.</exception>
    public static IServerlessDeployer Create(IConfiguration configuration, IReadOnlyDictionary<string, string> functionSourceEnv)
    {
        string platform = configuration["Runner:Serverless:Platform"] ?? "lambda";
        return platform switch
        {
            "lambda" => CreateLambda(configuration, functionSourceEnv),
            "azure-flex" => CreateAzureFlex(configuration, functionSourceEnv),
            _ => throw new InvalidOperationException(
                $"Runner:Serverless:Platform '{platform}' is not a known serverless platform — use 'lambda' (default) or 'azure-flex'."),
        };
    }

    // The runner's AWS identity: an IAmazonLambda pointed at LocalStack (the demo's AWS analogue, ADR 0060) or, in
    // production, at real AWS with the runner's own IAM identity. This is the SAME deployer code either way — only the
    // endpoint and credentials differ (ADR 0060). Dummy static credentials are correct for LocalStack Community (it
    // ignores IAM); a real deployment omits ServiceUrl and lets the AWS SDK resolve the runner's ambient identity. The
    // execution-role ARN is a dummy against LocalStack; a real deployment supplies the function's role.
    private static LambdaServerlessDeployer CreateLambda(IConfiguration configuration, IReadOnlyDictionary<string, string> functionSourceEnv)
    {
        string lambdaServiceUrl = configuration["Runner:Lambda:ServiceUrl"]
            ?? throw new InvalidOperationException("Runner:Lambda:ServiceUrl (the LocalStack edge endpoint, or the AWS endpoint) is required — the AppHost injects LocalStack's.");
        var lambdaConfig = new AmazonLambdaConfig
        {
            ServiceURL = lambdaServiceUrl,
            AuthenticationRegion = configuration["Runner:Lambda:Region"] ?? "us-east-1",
        };
        var lambdaClient = new AmazonLambdaClient(
            new BasicAWSCredentials(
                configuration["Runner:Lambda:AccessKey"] ?? "test",
                configuration["Runner:Lambda:SecretKey"] ?? "test"),
            lambdaConfig);
        return new LambdaServerlessDeployer(
            lambdaClient,
            new LambdaDeployerOptions
            {
                ExecutionRoleArn = configuration["Runner:Lambda:ExecutionRoleArn"] ?? "arn:aws:iam::000000000000:role/lambda-role",
                FunctionEnvironment = functionSourceEnv,
            });
    }

    // The runner's Azure identity is ambient (DefaultAzureCredential: a Managed Identity in production, the developer's
    // CLI sign-in locally — ADR 0059 decision 6, never static keys). The deployer posts the app package to the Flex
    // Consumption One Deploy endpoint with an ARM AAD bearer and stamps the source app settings over ARM (ADR 0061
    // amendment); the runner holds no secret.
    private static AzureFunctionsFlexDeployer CreateAzureFlex(IConfiguration configuration, IReadOnlyDictionary<string, string> functionSourceEnv)
    {
        return new AzureFunctionsFlexDeployer(
            new DefaultAzureCredential(),
            new AzureFunctionsFlexDeployerOptions
            {
                SubscriptionId = Required(configuration, "Runner:AzureFlex:SubscriptionId"),
                ResourceGroupName = Required(configuration, "Runner:AzureFlex:ResourceGroup"),
                AppNamePrefix = Required(configuration, "Runner:AzureFlex:AppNamePrefix"),
                FunctionAppSettings = functionSourceEnv,
            });
    }

    private static string Required(IConfiguration configuration, string key)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{key} is required for the azure-flex serverless platform.");
}