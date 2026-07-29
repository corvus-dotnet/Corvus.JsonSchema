// <copyright file="IFunctionAppConfigurator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// The management-plane half of an Azure run-from-package deploy, which has no local emulator (ADR 0061): it points the
/// target <c>dotnet-isolated</c> Function App at the uploaded package (the <c>WEBSITE_RUN_FROM_PACKAGE</c> app setting) and
/// sets the deployed environment's source app settings (each <c>ARAZZO_SOURCE__&lt;name&gt;</c> a source base URL the baked
/// worker's transport binder reads), then returns the app's base URL.
/// </summary>
/// <remarks>
/// The real implementation drives Azure Resource Manager with the runner's identity (ADR 0059, the runner is the secure
/// boundary that holds the environment's cloud credentials). It is a separate seam because Azure has no management-plane
/// emulator (Azurite emulates Storage only), so a deploy's storage half runs against Azurite locally while this half is
/// exercised against real Azure — the analogue of Lambda's <c>AWS_IAM</c> Function URL auth being real-AWS-only (ADR 0060).
/// A recording fake stands in for local and CI tests.
/// </remarks>
public interface IFunctionAppConfigurator
{
    /// <summary>
    /// Configures the target Function App to run from the uploaded package and returns its base URL.
    /// </summary>
    /// <param name="request">The deploy request identifying the (base workflow, version, environment, runtime) whose app is targeted.</param>
    /// <param name="packageUrl">The URL of the uploaded package the app should run from (the <c>WEBSITE_RUN_FROM_PACKAGE</c> value).</param>
    /// <param name="appSettings">The source app settings to set on the app (each <c>ARAZZO_SOURCE__&lt;name&gt;</c> a source base URL).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The configured app's base URL; the deployer appends the HTTP-trigger invoke path to it to form the invoke URL.</returns>
    ValueTask<Uri> ApplyRunFromPackageAsync(
        ServerlessDeployRequest request,
        Uri packageUrl,
        IReadOnlyDictionary<string, string> appSettings,
        CancellationToken cancellationToken);
}