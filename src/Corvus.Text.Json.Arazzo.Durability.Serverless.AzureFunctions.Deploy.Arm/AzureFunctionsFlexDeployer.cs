// <copyright file="AzureFunctionsFlexDeployer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Headers;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;

/// <summary>
/// The Azure Functions <b>Flex Consumption</b> <see cref="IServerlessDeployer"/> (ADR 0059, ADR 0061). Flex Consumption is
/// the recommended serverless plan for Azure Functions, and its one supported deployment technology is <em>One Deploy</em>:
/// a ready-to-run package is posted to the app's Kudu One Deploy endpoint, which stores it in the deployment container and
/// runs it on start. So this deployer posts the version's verified isolated-worker app package to
/// <c>{scm}/api/publish</c>, sets the deployed environment's source app settings, and returns the app's HTTP-trigger URL.
/// </summary>
/// <remarks>
/// The credential is injected: the runner supplies the environment's identity (a managed identity or a least-privileged
/// service principal), so this class holds no cloud identity of its own — the runner is the secure boundary (ADR 0059). It
/// reads the app's publishing credentials over ARM at deploy time (never stored) to authenticate the One Deploy post. One
/// Deploy is asynchronous for the Flex plan, so this waits on the Kudu deployment status until it completes. An Azure
/// failure is returned as a <see cref="ServerlessDeployResult.Failure"/> rather than thrown.
/// </remarks>
public sealed class AzureFunctionsFlexDeployer : IServerlessDeployer
{
    // Kudu (the SCM/One Deploy site) accepts an Azure Resource Manager bearer token, which is how the CLI deploys; Flex
    // Consumption disables SCM basic authentication by default, so this AAD path is required, not publishing credentials.
    private static readonly string[] ArmScope = ["https://management.azure.com/.default"];

    private static readonly TimeSpan DeploymentPollTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DeploymentPollDelay = TimeSpan.FromSeconds(5);

    private readonly TokenCredential credential;
    private readonly ArmClient armClient;
    private readonly AzureFunctionsFlexDeployerOptions options;

    /// <summary>Initializes a new instance of the <see cref="AzureFunctionsFlexDeployer"/> class.</summary>
    /// <param name="credential">The credential the runner supplies for the environment (the runner is the secure boundary, ADR 0059).</param>
    /// <param name="options">The subscription, resource group, app-name prefix, invoke path, and source app settings.</param>
    public AzureFunctionsFlexDeployer(TokenCredential credential, AzureFunctionsFlexDeployerOptions options)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(options);
        this.credential = credential;
        this.options = options;
        this.armClient = new ArmClient(credential, options.SubscriptionId);
    }

    /// <inheritdoc/>
    public async ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
    {
        string appName = ArmFunctionAppConfigurator.AppName(request, this.options.AppNamePrefix);
        try
        {
            ResourceIdentifier siteId = WebSiteResource.CreateResourceIdentifier(this.options.SubscriptionId, this.options.ResourceGroupName, appName);
            WebSiteResource site = await this.armClient.GetWebSiteResource(siteId).GetAsync(cancellationToken).ConfigureAwait(false);

            // Set the deployed environment's source settings first (merged over the app's existing settings), so they are
            // in place when the deployed package starts.
            if (this.options.FunctionAppSettings is { Count: > 0 } sourceSettings)
            {
                AppServiceConfigurationDictionary current = await site.GetApplicationSettingsAsync(cancellationToken).ConfigureAwait(false);
                foreach ((string key, string value) in sourceSettings)
                {
                    current.Properties[key] = value;
                }

                await site.UpdateApplicationSettingsAsync(current, cancellationToken).ConfigureAwait(false);
            }

            // Authenticate the One Deploy post with an ARM bearer token (Flex disables SCM basic auth). The SCM host is the
            // app's default host with the '.scm' segment inserted.
            AccessToken token = await this.credential.GetTokenAsync(new TokenRequestContext(ArmScope), cancellationToken).ConfigureAwait(false);
            string scmBase = $"https://{appName}.scm.azurewebsites.net";

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using var package = new ByteArrayContent(request.NativeBinary.ToArray());
            package.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            using HttpResponseMessage response = await httpClient
                .PostAsync(new Uri($"{scmBase}/api/publish?type=zip&RemoteBuild=false"), package, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                // One Deploy is asynchronous on Flex: wait on the deployment status until it completes.
                (bool succeeded, string log) = await WaitForDeploymentAsync(httpClient, scmBase, cancellationToken).ConfigureAwait(false);
                if (!succeeded)
                {
                    return ServerlessDeployResult.Failure($"Azure Functions Flex deploy of '{appName}' did not complete: {log}");
                }
            }
            else if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return ServerlessDeployResult.Failure($"Azure Functions Flex deploy of '{appName}' failed: {(int)response.StatusCode} {body}");
            }

            var invokeUrl = new Uri($"https://{site.Data.DefaultHostName}/{this.options.InvokePath}");
            return ServerlessDeployResult.Success(invokeUrl.ToString(), $"One-deployed the Flex Consumption Function App '{appName}'.");
        }
        catch (RequestFailedException ex)
        {
            return ServerlessDeployResult.Failure($"Azure Functions Flex deploy of '{appName}' failed: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return ServerlessDeployResult.Failure($"Azure Functions Flex deploy of '{appName}' failed to reach the deployment endpoint: {ex.Message}");
        }
    }

    // Polls the Kudu latest-deployment status until it completes (or the poll times out). The status endpoint reports a
    // 'complete' flag and a status code where 4 is success and 3 is failed.
    private static async Task<(bool Succeeded, string Log)> WaitForDeploymentAsync(HttpClient httpClient, string scmBase, CancellationToken cancellationToken)
    {
        var latest = new Uri($"{scmBase}/api/deployments/latest");
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(DeploymentPollTimeout);
        string lastBody = string.Empty;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage status = await httpClient.GetAsync(latest, cancellationToken).ConfigureAwait(false);
            lastBody = await status.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (status.IsSuccessStatusCode)
            {
                using var document = System.Text.Json.JsonDocument.Parse(lastBody);
                System.Text.Json.JsonElement root = document.RootElement;
                bool complete = root.TryGetProperty("complete", out System.Text.Json.JsonElement completeElement) && completeElement.ValueKind == System.Text.Json.JsonValueKind.True;
                int code = root.TryGetProperty("status", out System.Text.Json.JsonElement statusElement) && statusElement.TryGetInt32(out int value) ? value : -1;
                if (complete)
                {
                    return (code == 4, lastBody);
                }
            }

            await Task.Delay(DeploymentPollDelay, cancellationToken).ConfigureAwait(false);
        }

        return (false, $"deployment did not complete within {DeploymentPollTimeout}. Last status: {lastBody}");
    }
}