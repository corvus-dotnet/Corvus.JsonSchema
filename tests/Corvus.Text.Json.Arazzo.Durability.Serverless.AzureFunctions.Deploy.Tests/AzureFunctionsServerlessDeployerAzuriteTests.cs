// <copyright file="AzureFunctionsServerlessDeployerAzuriteTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Storage.Blobs;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Tests;

/// <summary>
/// The live proof of the deployer's run-from-package storage mechanism against the Azurite emulator (ADR 0061): the
/// deployer uploads a package to blob storage over the same <c>Azure.Storage.Blobs</c> client production uses (only the
/// endpoint differs), hands the platform a run-from-package URL, and passes the source app settings. It fetches that URL
/// the way the App Service platform would and checks it returns exactly the uploaded package. It uses arbitrary package
/// bytes, so it proves the storage path without building a real app; that the uploaded package <em>runs</em> is
/// <c>AzureFunctionsRunFromPackageDeployTests</c> in the control-plane server tests.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureFunctionsServerlessDeployerAzuriteTests
{
    private static AzuriteContainer azurite = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        // Testcontainers.Azurite defaults to a very old Azurite image; pin a recent one so it recognises the Blob REST
        // API version the client and the generated SAS use.
        azurite = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
        await azurite.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (azurite is not null)
        {
            await azurite.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task Deploys_a_package_to_blob_storage_and_hands_the_platform_a_readable_run_from_package_url()
    {
        var blobService = new BlobServiceClient(azurite.GetConnectionString(), new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        BlobContainerClient packages = blobService.GetBlobContainerClient("arazzo-packages");

        var configurator = new RecordingFunctionAppConfigurator { AppBaseUrl = new Uri("https://arazzo-fn-check.example.net/") };
        var deployer = new AzureFunctionsServerlessDeployer(
            packages,
            configurator,
            new FixedSecretResolver("the-invoke-key"),
            new AzureFunctionsDeployerOptions
            {
                InvokeAuthorization = new AzureFunctionsInvokeAuthorization { InvokeKey = SecretRef.Parse("env://ARAZZO_INVOKE_KEY"), EntraAudience = "api://arazzo-functions" },
                FunctionAppSettings = new Dictionary<string, string>(StringComparer.Ordinal) { ["ARAZZO_SOURCE__echo"] = "https://echo.example", [ServerlessCheckpointOrigins.SettingName] = "https://runner.example/" },
            });

        // Arbitrary bytes stand in for the app zip: this proves the storage mechanism, not that the payload runs.
        byte[] package = new byte[4096];
        for (int i = 0; i < package.Length; i++)
        {
            package[i] = (byte)((i * 31) + 7);
        }

        var request = new ServerlessDeployRequest("check", 1, "isolated", "linux-x64", package);
        ServerlessDeployResult result = await deployer.DeployAsync(request, default);

        result.Succeeded.ShouldBeTrue(result.Log);

        // The invoke URL is the configurator's app base with the HTTP-trigger path appended.
        result.FunctionUrl.ShouldBe("https://arazzo-fn-check.example.net/api/invoke");

        // The deployer pointed the platform at a package URL and passed through the source app settings and the target.
        configurator.PackageUrl.ShouldNotBeNull("the deployer handed the platform no run-from-package URL.");
        configurator.AppSettings.ShouldNotBeNull();
        configurator.AppSettings!.ShouldContainKeyAndValue("ARAZZO_SOURCE__echo", "https://echo.example");
        configurator.Request!.Value.BaseWorkflowId.ShouldBe("check");

        // The invoke access goes to the management plane with the package: the key resolved from the runner's secret
        // store, and the Entra audience the app must require (ADR 0059 decision 4).
        configurator.InvokeAccess.ShouldBe(new FunctionAppInvokeAccess("the-invoke-key", "api://arazzo-functions"));

        // The run-from-package URL is the platform's own read handle: fetching it returns exactly the uploaded package.
        using var httpClient = new HttpClient();
        byte[] fetched = await httpClient.GetByteArrayAsync(configurator.PackageUrl);
        fetched.ShouldBe(package, "the package fetched from the run-from-package URL is not the one the deployer uploaded.");

        // And it landed at the deterministic per-target blob name.
        BlobClient blob = packages.GetBlobClient("check-v1-isolated-linux-x64.zip");
        (await blob.ExistsAsync()).Value.ShouldBeTrue("the package is not at its deterministic blob name.");
    }

    [TestMethod]
    public async Task A_refused_invoke_access_is_a_failed_deploy()
    {
        var blobService = new BlobServiceClient(azurite.GetConnectionString(), new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        var deployer = new AzureFunctionsServerlessDeployer(
            blobService.GetBlobContainerClient("arazzo-packages-refused"),
            new RecordingFunctionAppConfigurator { AppBaseUrl = new Uri("https://arazzo-fn-check.example.net/"), Refusal = "the app does not require authentication." },
            new FixedSecretResolver("the-invoke-key"),
            new AzureFunctionsDeployerOptions { InvokeAuthorization = new AzureFunctionsInvokeAuthorization { InvokeKey = SecretRef.Parse("env://ARAZZO_INVOKE_KEY"), EntraAudience = "api://arazzo-functions" }, FunctionAppSettings = new Dictionary<string, string> { [ServerlessCheckpointOrigins.SettingName] = "https://runner.example/" } });

        ServerlessDeployResult result = await deployer.DeployAsync(new ServerlessDeployRequest("check", 1, "isolated", "linux-x64", new byte[16]), default);

        result.Succeeded.ShouldBeFalse();
        result.FunctionUrl.ShouldBeEmpty();
        result.Log.ShouldContain("does not require authentication");
    }

    private sealed class FixedSecretResolver(string value) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => ValueTask.FromResult(SecretMaterial.FromString(value));
    }

    private sealed class RecordingFunctionAppConfigurator : IFunctionAppConfigurator
    {
        public required Uri AppBaseUrl { get; init; }

        public string? Refusal { get; init; }

        public Uri? PackageUrl { get; private set; }

        public IReadOnlyDictionary<string, string>? AppSettings { get; private set; }

        public ServerlessDeployRequest? Request { get; private set; }

        public FunctionAppInvokeAccess? InvokeAccess { get; private set; }

        public ValueTask<Uri> ApplyRunFromPackageAsync(ServerlessDeployRequest request, FunctionAppInvokeAccess invokeAccess, Uri packageUrl, IReadOnlyDictionary<string, string> appSettings, CancellationToken cancellationToken)
        {
            if (this.Refusal is not null)
            {
                throw new FunctionAppInvokeAccessException(this.Refusal);
            }

            this.Request = request;
            this.InvokeAccess = invokeAccess;
            this.PackageUrl = packageUrl;
            this.AppSettings = appSettings;
            return ValueTask.FromResult(this.AppBaseUrl);
        }
    }
}