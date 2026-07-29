// <copyright file="ArmFunctionAppLiveDeployTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Azure.Identity;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The live-Azure proof of the real management-plane deploy path (ADR 0061): the production
/// <see cref="AzureFunctionsServerlessDeployer"/> + <see cref="FlexConsumptionFunctionAppConfigurator"/> deploy a version's
/// real ReadyToRun isolated-worker app to a <b>real Flex Consumption Function App</b> and the platform runs it. It
/// provisions a Flex Consumption dotnet-isolated (.NET 10) Function App and its storage in a test resource group, runs the
/// production deployer (which uploads the package to real Azure Storage and, through the Flex configurator, triggers One
/// Deploy over Azure Resource Manager), and proves the platform loaded <em>our</em> function from the deployment by
/// invoking the app's <c>invoke</c> route until it is served by our function (a no-runId probe reaches our handler, so the
/// route stops being a 404). It then <b>tears down every resource it created</b>, so there is zero cost between runs. Flex
/// Consumption is the recommended serverless plan and the one that runs the deployed isolated worker in this subscription
/// (Linux Consumption does not, and there is no dedicated/premium VM quota).
/// </summary>
/// <remarks>
/// Opt-in (<c>[TestCategory("integration")][TestCategory("azure")]</c>). It needs the <c>arazzo-aot-builder</c> image and
/// the local package feed (to build the app), the Azure CLI on the path and an authenticated session (locally
/// <c>az login</c>; in CI the pipeline's Azure credentials), and a writable test resource group. It skips unless
/// <c>ARAZZO_AZURE_SUBSCRIPTION_ID</c>, <c>ARAZZO_AZURE_RESOURCE_GROUP</c>, <c>ARAZZO_AOT_LOCAL_FEED</c>, and
/// <c>ARAZZO_AOT_RUNTIME_VERSION</c> are set. No subscription or resource-group identifiers are baked into the source; they
/// arrive only through these environment variables.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("azure")]
public sealed class ArmFunctionAppLiveDeployTests
{
    [TestMethod]
    public async Task Deploys_a_real_app_to_a_live_flex_consumption_function_app_and_the_platform_loads_our_function()
    {
        string? subscription = System.Environment.GetEnvironmentVariable("ARAZZO_AZURE_SUBSCRIPTION_ID");
        string? resourceGroup = System.Environment.GetEnvironmentVariable("ARAZZO_AZURE_RESOURCE_GROUP");
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(subscription) || string.IsNullOrEmpty(resourceGroup) || string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AZURE_SUBSCRIPTION_ID, ARAZZO_AZURE_RESOURCE_GROUP, ARAZZO_AOT_LOCAL_FEED, and ARAZZO_AOT_RUNTIME_VERSION (and be logged in with the Azure CLI) to run this live-Azure proof.");
            return;
        }

        string builderImage = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";
        string location = System.Environment.GetEnvironmentVariable("ARAZZO_AZURE_LOCATION") ?? "uksouth";

        // Globally-unique names for this run (Azure site + storage names are global).
        string id = Guid.NewGuid().ToString("n")[..10];
        string storageAccount = $"arz{id}";
        string appPrefix = $"arz{id}";
        var request = new ServerlessDeployRequest("serverless-check", 1, "isolated", "linux-x64", ReadOnlyMemory<byte>.Empty);
        string appName = ArmFunctionAppConfigurator.AppName(request, appPrefix);

        // Build the real app package BEFORE provisioning, so a build failure leaves no Azure resources behind.
        byte[] appZip = await ServerlessLiveExecutionSupport.BuildDeployArtifactAsync(ServerlessTarget.AzureFunctions, feed, runtimeVersion, builderImage);
        request = request with { NativeBinary = appZip };

        string? planId = null;
        string? appInsightsId = null;
        bool provisioned = false;
        try
        {
            // 1. Provision a Flex Consumption dotnet-isolated (.NET 10) Function App and its storage account. Flex also
            // creates its own plan and an Application Insights component, whose ids we capture so teardown removes them too.
            await RunAzAsync($"storage account create -n {storageAccount} -g {resourceGroup} -l {location} --sku Standard_LRS --allow-blob-public-access false");
            provisioned = true;
            await RunAzAsync($"functionapp create -n {appName} -g {resourceGroup} --storage-account {storageAccount} --flexconsumption-location {location} --runtime dotnet-isolated --runtime-version 10");
            planId = await CaptureAzAsync($"functionapp show -n {appName} -g {resourceGroup} --query appServicePlanId -o tsv");
            appInsightsId = await CaptureAzAsync($"resource list -g {resourceGroup} --resource-type Microsoft.Insights/components --query \"[?name=='{appName}'].id | [0]\" -o tsv");

            // 2. Run the PRODUCTION Flex deployer against real Azure: it posts the app package to the Flex One Deploy
            // endpoint (Flex Consumption's only deployment technology) and returns the app's real invoke URL.
            var deployer = new AzureFunctionsFlexDeployer(
                new AzureCliCredential(),
                new AzureFunctionsFlexDeployerOptions { SubscriptionId = subscription, ResourceGroupName = resourceGroup, AppNamePrefix = appPrefix });

            ServerlessDeployResult result = await deployer.DeployAsync(request, default);
            result.Succeeded.ShouldBeTrue(result.Log);
            result.FunctionUrl.ShouldBe($"https://{appName}.azurewebsites.net/api/invoke");

            // 3. The deploy proof: hitting the invoke URL, once One Deploy has run, is served by OUR function (a no-runId
            // probe reaches our handler, which faults with a 500), so the route stops being a 404. Poll until that
            // transition, which proves the real Flex app loaded our function from the real deployment.
            bool loaded = await PollUntilInvokeRouteIsOurFunctionAsync(new Uri(result.FunctionUrl), TimeSpan.FromMinutes(6));
            loaded.ShouldBeTrue($"the live Flex Function App '{appName}' never served our invoke route (it stayed a 404) after One Deploy.");
        }
        finally
        {
            // 4. Tear down everything created, so there is zero cost between runs. Best-effort and independent.
            if (provisioned)
            {
                await TryRunAzAsync($"functionapp delete -n {appName} -g {resourceGroup}");
                if (!string.IsNullOrWhiteSpace(planId))
                {
                    await TryRunAzAsync($"resource delete --ids {planId.Trim()}");
                }

                if (!string.IsNullOrWhiteSpace(appInsightsId))
                {
                    await TryRunAzAsync($"resource delete --ids {appInsightsId.Trim()}");
                }

                await TryRunAzAsync($"storage account delete -n {storageAccount} -g {resourceGroup} --yes");
            }
        }
    }

    // Polls the app's invoke route until it stops being a 404 (function not present) and is instead served by our loaded
    // function, which faults on a no-runId probe (a 500). Connection resets and 502/503 during start-up are "keep waiting".
    private static async Task<bool> PollUntilInvokeRouteIsOurFunctionAsync(Uri invokeUri, TimeSpan budget)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < budget)
        {
            try
            {
                using HttpResponseMessage response = await httpClient.PostAsync(
                    invokeUri, new StringContent("{\"probe\":true}", System.Text.Encoding.UTF8, "application/json"));

                int status = (int)response.StatusCode;
                if (status != 404 && status != 502 && status != 503)
                {
                    return true;
                }
            }
            catch (HttpRequestException)
            {
                // The app is not yet reachable while it cold-starts; keep waiting.
            }
            catch (TaskCanceledException)
            {
                // A request timed out during a slow cold start; keep waiting.
            }

            await Task.Delay(TimeSpan.FromSeconds(15));
        }

        return false;
    }

    private static async Task RunAzAsync(string arguments)
    {
        (int exitCode, string standardOutput, string standardError) = await RunAzProcessAsync(arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"az {arguments} failed (exit {exitCode}):\n{standardOutput}{standardError}");
        }
    }

    private static async Task<string> CaptureAzAsync(string arguments)
    {
        (int exitCode, string standardOutput, string standardError) = await RunAzProcessAsync(arguments);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"az {arguments} failed (exit {exitCode}):\n{standardOutput}{standardError}");
        }

        // Return only stdout: the CLI can emit a Python dependency warning on stderr, which must not corrupt a captured
        // value such as a connection string.
        return standardOutput.Trim();
    }

    private static async Task TryRunAzAsync(string arguments)
    {
        try
        {
            await RunAzAsync(arguments);
        }
        catch (InvalidOperationException)
        {
            // Best-effort teardown; a delete that fails (already gone, transient) must not fail the test.
        }
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunAzProcessAsync(string arguments)
    {
        // The Azure CLI is a Python launcher script; invoke it by name so the caller's PATH (which includes a user-local
        // install) resolves it, on Linux and in CI alike. Keep stdout and stderr separate so a stderr warning never
        // corrupts a captured stdout value.
        var startInfo = new ProcessStartInfo("az", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await standardOutput, await standardError);
    }
}