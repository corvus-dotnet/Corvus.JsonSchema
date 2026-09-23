// <copyright file="ServerlessDeployerSelection.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.Lambda;
using Amazon.Runtime;
using Azure.Identity;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;
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
/// real AWS in production — only the endpoint and credentials differ, ADR 0060), <c>azure-flex</c> (a real Flex
/// Consumption Function App deployed by One Deploy with an ARM AAD bearer token, ADR 0061 amendment — there is no
/// local Azure management-plane emulator, so this platform always targets real Azure), or <c>micro-guest</c> (the
/// runner's own machine, ADR 0063: the warm sidecar snapshots a Hyperlight micro-VM per (environment, version) and
/// restores it per advance — no cloud is involved and the runner needs a hypervisor). Each platform reads only its
/// own configuration keys, so a Lambda host needs no Azure or sidecar settings and vice versa.
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
            "micro-guest" => CreateMicroGuest(configuration, functionSourceEnv),
            _ => throw new InvalidOperationException(
                $"Runner:Serverless:Platform '{platform}' is not a known serverless platform — use 'lambda' (default), 'azure-flex', or 'micro-guest'."),
        };
    }

    /// <summary>
    /// Builds the configured platform's invoke authenticator (ADR 0059 decision 4), the counterpart of its deployer: what
    /// the runner presents to the deployed function on each invocation. There is no anonymous choice.
    /// </summary>
    /// <param name="configuration">The host configuration.</param>
    /// <returns>The platform's invoke authenticator.</returns>
    /// <exception cref="InvalidOperationException">The platform is unknown, or its required configuration is missing.</exception>
    public static IServerlessInvokeAuthenticator CreateInvokeAuthenticator(IConfiguration configuration)
    {
        string platform = configuration["Runner:Serverless:Platform"] ?? "lambda";
        switch (platform)
        {
            case "lambda":
                // The Function URL is AWS_IAM, so the invocation is signed with the same identity that deployed it.
                // LocalStack Community ignores the signature; real AWS refuses an invocation without it.
                return new SigV4ServerlessInvokeAuthenticator(LambdaCredentials(configuration), configuration["Runner:Lambda:Region"] ?? "us-east-1");

            case "azure-flex":
                AzureFunctionsInvokeAuthorization authorization = AzureInvokeAuthorization(configuration);
                var functionKey = new FunctionKeyServerlessInvokeAuthenticator(RunnerSecrets(), authorization.InvokeKey);
                return authorization.EntraAudience is { } audience
                    ? new EntraServerlessInvokeAuthenticator(functionKey, new DefaultAzureCredential(), audience)
                    : functionKey;

            case "micro-guest":
                // The sidecar's admin surface takes the shared admin token the runner holds in its own secret store
                // (P1-10), and the authenticator refuses to invoke anything that is not on this machine.
                return new MicroGuestSidecarInvokeAuthenticator(
                    RunnerSecrets(),
                    SecretRef.Parse(Required(configuration, "Runner:MicroGuest:AdminTokenRef", "micro-guest")));

            default:
                throw new InvalidOperationException(
                    $"Runner:Serverless:Platform '{platform}' is not a known serverless platform — use 'lambda' (default), 'azure-flex', or 'micro-guest'.");
        }
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
        var lambdaClient = new AmazonLambdaClient(LambdaCredentials(configuration), lambdaConfig);
        return new LambdaServerlessDeployer(
            lambdaClient,
            new LambdaDeployerOptions
            {
                ExecutionRoleArn = configuration["Runner:Lambda:ExecutionRoleArn"] ?? "arn:aws:iam::000000000000:role/lambda-role",
                FunctionEnvironment = functionSourceEnv,
            });
    }

    private static BasicAWSCredentials LambdaCredentials(IConfiguration configuration)
        => new(configuration["Runner:Lambda:AccessKey"] ?? "test", configuration["Runner:Lambda:SecretKey"] ?? "test");

    // The invoke key is a reference into the runner's own secret store (env:// or file:// here). The deployer sets it on
    // each Function App and the invoker presents it, so it never reaches the control plane. Entra is an optional second
    // layer on top of the key, chosen by naming the Function App's audience.
    private static AzureFunctionsInvokeAuthorization AzureInvokeAuthorization(IConfiguration configuration)
        => new()
        {
            InvokeKey = SecretRef.Parse(Required(configuration, "Runner:AzureFlex:InvokeKeyRef", "azure-flex")),
            EntraAudience = configuration["Runner:AzureFlex:EntraAudience"],
        };

    private static ISecretResolver RunnerSecrets() => new SecretResolverBuilder().AddEnvironmentAndFile().Build();

    // The runner's Azure identity is ambient (DefaultAzureCredential: a Managed Identity in production, the developer's
    // CLI sign-in locally — ADR 0059 decision 6, never static keys). The deployer posts the app package to the Flex
    // Consumption One Deploy endpoint with an ARM AAD bearer and stamps the source app settings over ARM (ADR 0061
    // amendment); the runner holds no secret.
    private static AzureFunctionsFlexDeployer CreateAzureFlex(IConfiguration configuration, IReadOnlyDictionary<string, string> functionSourceEnv)
    {
        return new AzureFunctionsFlexDeployer(
            new DefaultAzureCredential(),
            RunnerSecrets(),
            new AzureFunctionsFlexDeployerOptions
            {
                SubscriptionId = Required(configuration, "Runner:AzureFlex:SubscriptionId", "azure-flex"),
                ResourceGroupName = Required(configuration, "Runner:AzureFlex:ResourceGroup", "azure-flex"),
                AppNamePrefix = Required(configuration, "Runner:AzureFlex:AppNamePrefix", "azure-flex"),
                InvokeAuthorization = AzureInvokeAuthorization(configuration),
                FunctionAppSettings = functionSourceEnv,
            });
    }

    // The micro-guest platform deploys to the runner's OWN machine (ADR 0063): the deployer stages the initrd and
    // evolves the (environment, version) sandbox over the warm sidecar's local admin surface, and the recorded function
    // URL is the sidecar's local invoke endpoint. The checkpoint surface must be the runner's routable address — the
    // guest's host-proxied network denies loopback by design — and no cloud identity is involved.
    private static MicroGuestDeployer CreateMicroGuest(IConfiguration configuration, IReadOnlyDictionary<string, string> functionSourceEnv)
    {
        return new MicroGuestDeployer(
            new MicroGuestDeployerOptions
            {
                SidecarBaseUrl = new Uri(Required(configuration, "Runner:MicroGuest:SidecarUrl", "micro-guest")),
                AdminToken = SecretRef.Parse(Required(configuration, "Runner:MicroGuest:AdminTokenRef", "micro-guest")),
                CheckpointSurfaceUrl = new Uri(Required(configuration, "Runner:MicroGuest:CheckpointSurfaceUrl", "micro-guest")),
                GuestEnvironment = functionSourceEnv,
            },
            RunnerSecrets());
    }

    private static string Required(IConfiguration configuration, string key, string platform)
        => configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{key} is required for the {platform} serverless platform.");
}