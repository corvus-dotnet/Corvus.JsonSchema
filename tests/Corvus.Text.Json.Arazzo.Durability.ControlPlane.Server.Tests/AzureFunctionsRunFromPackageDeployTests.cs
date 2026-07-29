// <copyright file="AzureFunctionsRunFromPackageDeployTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The most realistic local proof of the Azure Functions deploy path (ADR 0061): the production
/// <see cref="AzureFunctionsServerlessDeployer"/> deploys a version's real ReadyToRun isolated-worker app by
/// run-from-package to <b>Azurite</b> — the same <c>Azure.Storage.Blobs</c> client production uses against real Azure
/// Storage, only the endpoint differs — then the App Service platform's own operation is performed: the package is fetched
/// from the run-from-package URL the deployer handed over and run under the <b>real Azure Functions host</b>, where a
/// seeded run drives to <c>Completed</c>. So the whole deploy mechanism is exercised locally end to end: upload → the
/// platform's fetch-by-URL → execute, with only the ARM app-configuration step (which has no emulator) standing in as a
/// recording fake, the analogue of Lambda's <c>AWS_IAM</c> auth being real-AWS-only (ADR 0060).
/// </summary>
/// <remarks>
/// Opt-in (<c>[TestCategory("integration")][TestCategory("docker")]</c>): it needs a container runtime, the
/// <c>arazzo-aot-builder</c> image, the local package feed, the Azure Functions <c>dotnet-isolated</c> image, and Azurite.
/// It skips unless <c>ARAZZO_AOT_LOCAL_FEED</c> and <c>ARAZZO_AOT_RUNTIME_VERSION</c> are set.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureFunctionsRunFromPackageDeployTests
{
    [TestMethod]
    public async Task Deploys_a_real_app_by_run_from_package_to_azurite_and_the_uploaded_package_runs_to_completion()
    {
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED (the local package feed path) and ARAZZO_AOT_RUNTIME_VERSION (its Corvus runtime version), and build the arazzo-aot-builder image, to run this proof.");
            return;
        }

        string builderImage = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";

        // 1. Build the real Azure app package (the same shared workflow the execution gate runs).
        byte[] appZip = await ServerlessLiveExecutionSupport.BuildDeployArtifactAsync(ServerlessTarget.AzureFunctions, feed, runtimeVersion, builderImage);

        // 2. Azurite is the run-from-package blob store. The runner wires this same client to real Azure Storage in
        // production; only the endpoint differs (the one-deployer pattern).
        var azurite = new AzuriteBuilder().WithImage("mcr.microsoft.com/azure-storage/azurite:latest").Build();
        await azurite.StartAsync();
        try
        {
            var blobService = new BlobServiceClient(azurite.GetConnectionString(), new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
            BlobContainerClient packages = blobService.GetBlobContainerClient("arazzo-packages");

            var configurator = new RecordingFunctionAppConfigurator { AppBaseUrl = new Uri("https://arazzo-fn-serverless-check.example.net/") };
            var deployer = new AzureFunctionsServerlessDeployer(
                packages,
                configurator,
                new AzureFunctionsDeployerOptions
                {
                    // In production the deployer sets this as an app setting the platform injects; local ARM has no
                    // emulator, so the run below injects the real echo source URL as the container's env directly (the
                    // shared helper), standing in for the platform applying this setting (ADR 0061 asymmetry).
                    FunctionAppSettings = new Dictionary<string, string>(StringComparer.Ordinal) { ["ARAZZO_SOURCE__echo"] = "https://echo.example" },
                });

            // 3. Deploy: the production deployer uploads the package to Azurite and records the run-from-package URL.
            ServerlessDeployResult result = await deployer.DeployAsync(
                new ServerlessDeployRequest("serverless-check", 1, "isolated", "linux-x64", appZip),
                default);
            result.Succeeded.ShouldBeTrue(result.Log);
            result.FunctionUrl.ShouldBe("https://arazzo-fn-serverless-check.example.net/api/invoke");
            configurator.PackageUrl.ShouldNotBeNull("the deployer handed the platform no run-from-package URL.");

            // 4. The App Service platform's own operation: fetch the package from the run-from-package URL. It must be
            // exactly the app the deployer uploaded.
            using var httpClient = new HttpClient();
            byte[] fetched = await httpClient.GetByteArrayAsync(configurator.PackageUrl);
            fetched.ShouldBe(appZip, "the package fetched from the run-from-package URL is not the app the deployer uploaded.");

            // 5. And that fetched package runs: extract it and drive a seeded run to completion under the real Functions host.
            string appDirectory = Directory.CreateTempSubdirectory("arazzo-azure-rfp-").FullName;
            try
            {
                using (var archive = new ZipArchive(new MemoryStream(fetched), ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(appDirectory);
                }

                await ServerlessLiveExecutionSupport.RunSeededRunToCompletionUnderFunctionsHostAsync(appDirectory, ServerlessLiveExecutionSupport.FunctionsImage());
            }
            finally
            {
                TryDeleteDirectory(appDirectory);
            }
        }
        finally
        {
            await azurite.DisposeAsync();
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A best-effort cleanup of the unpacked app; a leftover temp directory is not worth failing the test.
        }
    }

    private sealed class RecordingFunctionAppConfigurator : IFunctionAppConfigurator
    {
        public required Uri AppBaseUrl { get; init; }

        public Uri? PackageUrl { get; private set; }

        public IReadOnlyDictionary<string, string>? AppSettings { get; private set; }

        public ValueTask<Uri> ApplyRunFromPackageAsync(ServerlessDeployRequest request, Uri packageUrl, IReadOnlyDictionary<string, string> appSettings, CancellationToken cancellationToken)
        {
            this.PackageUrl = packageUrl;
            this.AppSettings = appSettings;
            return ValueTask.FromResult(this.AppBaseUrl);
        }
    }
}