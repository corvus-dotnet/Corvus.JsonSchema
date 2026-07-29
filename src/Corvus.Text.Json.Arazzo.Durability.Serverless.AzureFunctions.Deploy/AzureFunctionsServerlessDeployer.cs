// <copyright file="AzureFunctionsServerlessDeployer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// The Azure Functions <see cref="IServerlessDeployer"/> (ADR 0055, ADR 0059, ADR 0061). It deploys a version's verified
/// ReadyToRun isolated-worker app package by <em>run-from-package</em>: it uploads the app zip to the package blob
/// container and points the target <c>dotnet-isolated</c> Function App at it (the <c>WEBSITE_RUN_FROM_PACKAGE</c> app
/// setting) through the injected <see cref="IFunctionAppConfigurator"/>, then returns the app's HTTP-trigger invoke URL.
/// </summary>
/// <remarks>
/// The package <see cref="BlobContainerClient"/> is injected: the runner wires it to Azurite for local development and to
/// the environment's real Azure Storage in production, and only the client's endpoint differs — the same one-deployer,
/// endpoint-only pattern as the Lambda deployer (ADR 0060). The management-plane configuration (the app setting, over ARM)
/// has no local emulator, so it is the injected <see cref="IFunctionAppConfigurator"/>: real Azure Resource Manager in
/// production, a recording fake in tests (ADR 0061 — the storage path is proven locally against Azurite and the ARM path
/// against real Azure). This class never constructs a client and never holds credentials; the runner is the secure
/// boundary (ADR 0059). An Azure failure is returned as a <see cref="ServerlessDeployResult.Failure"/> rather than thrown,
/// matching how the runner's deploy service treats a deploy failure.
/// </remarks>
public sealed class AzureFunctionsServerlessDeployer : IServerlessDeployer
{
    private static readonly IReadOnlyDictionary<string, string> NoAppSettings = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly BlobContainerClient packageContainer;
    private readonly IFunctionAppConfigurator configurator;
    private readonly AzureFunctionsDeployerOptions options;

    /// <summary>Initializes a new instance of the <see cref="AzureFunctionsServerlessDeployer"/> class.</summary>
    /// <param name="packageContainer">The package blob container, wired by the runner to Azurite or to real Azure Storage with the environment's identity.</param>
    /// <param name="configurator">The management-plane configurator that points the Function App at the package (real ARM in production; a fake in tests).</param>
    /// <param name="options">The deployer options: the invoke path, the source app settings, and the package SAS policy.</param>
    public AzureFunctionsServerlessDeployer(BlobContainerClient packageContainer, IFunctionAppConfigurator configurator, AzureFunctionsDeployerOptions options)
    {
        ArgumentNullException.ThrowIfNull(packageContainer);
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(options);
        this.packageContainer = packageContainer;
        this.configurator = configurator;
        this.options = options;
    }

    /// <inheritdoc/>
    public async ValueTask<ServerlessDeployResult> DeployAsync(ServerlessDeployRequest request, CancellationToken cancellationToken)
    {
        string blobName = PackageBlobName(request);
        try
        {
            await this.packageContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            // Upload the verified app package. Overwrite so a redeploy of the same target replaces its own package in place.
            BlobClient package = this.packageContainer.GetBlobClient(blobName);
            await package.UploadAsync(BinaryData.FromBytes(request.NativeBinary), overwrite: true, cancellationToken).ConfigureAwait(false);

            Uri packageUrl = this.PackageUrl(package);
            IReadOnlyDictionary<string, string> appSettings = this.options.FunctionAppSettings ?? NoAppSettings;

            // Point the Function App at the package and set the source app settings (the management-plane half; real ARM in
            // production, a fake in tests). The returned base URL is the app's; the invoke path forms the HTTP-trigger URL
            // that ServerlessRunExecutionBackend posts each invocation to.
            Uri appBaseUrl = await this.configurator
                .ApplyRunFromPackageAsync(request, packageUrl, appSettings, cancellationToken)
                .ConfigureAwait(false);

            var invokeUrl = new Uri(EnsureTrailingSlash(appBaseUrl), this.options.InvokePath);
            return ServerlessDeployResult.Success(
                invokeUrl.ToString(),
                $"Deployed run-from-package '{blobName}' and pointed the dotnet-isolated Function App at it (WEBSITE_RUN_FROM_PACKAGE).");
        }
        catch (RequestFailedException ex)
        {
            return ServerlessDeployResult.Failure($"Azure Functions deploy of package '{blobName}' failed: {ex.Message}");
        }
    }

    /// <summary>
    /// The deterministic package blob name for a deploy target: <c>{base}-v{ver}-{env}-{rid}.zip</c>, lowercased with any
    /// character outside <c>[a-z0-9-_]</c> mapped to <c>-</c>, so the same target overwrites its own package on redeploy and
    /// two distinct targets never collide.
    /// </summary>
    /// <param name="request">The deploy request whose target tuple names the package.</param>
    /// <returns>The blob name.</returns>
    internal static string PackageBlobName(ServerlessDeployRequest request)
    {
        string body = $"{Sanitize(request.BaseWorkflowId)}-v{request.VersionNumber.ToString(CultureInfo.InvariantCulture)}-{Sanitize(request.Environment)}-{Sanitize(request.RuntimeIdentifier)}";
        return $"{body}.zip";
    }

    // The URL the platform runs the package from: the blob URI, with a read SAS appended when the option is set and the
    // client can generate one (a shared-key credential), since run-from-package needs the platform to read the blob.
    private Uri PackageUrl(BlobClient package)
    {
        if (this.options.AppendReadSasToPackageUrl && package.CanGenerateSasUri)
        {
            return package.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(this.options.PackageSasLifetime));
        }

        return package.Uri;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
        => uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");

    private static string Sanitize(string value)
    {
        // Map any character outside the safe set to '-', lowercasing as we go (blob names are case-sensitive, so a stable
        // lowercase keeps the same target's name stable regardless of the source casing).
        return string.Create(value.Length, value, static (span, source) =>
        {
            for (int i = 0; i < source.Length; i++)
            {
                char c = char.ToLowerInvariant(source[i]);
                span[i] = IsSafe(c) ? c : '-';
            }
        });
    }

    private static bool IsSafe(char c)
        => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-' or '_';
}