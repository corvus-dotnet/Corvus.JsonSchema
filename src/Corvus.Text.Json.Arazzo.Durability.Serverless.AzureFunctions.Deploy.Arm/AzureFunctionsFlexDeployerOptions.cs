// <copyright file="AzureFunctionsFlexDeployerOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;

/// <summary>
/// The options an <see cref="AzureFunctionsFlexDeployer"/> needs beyond the injected credential: the subscription and
/// resource group the target Flex Consumption Function App lives in, a prefix that makes the per-target app name globally
/// unique, the HTTP-trigger invoke path, and the source app settings the baked worker reads. The runner supplies these
/// from the environment's configuration; the deployer holds no cloud identity of its own (ADR 0059, the runner is the
/// secure boundary — it supplies the credential).
/// </summary>
public sealed record AzureFunctionsFlexDeployerOptions
{
    /// <summary>Gets the Azure subscription id the target Function App lives in.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>Gets the resource group the target Function App lives in.</summary>
    public required string ResourceGroupName { get; init; }

    /// <summary>
    /// Gets the prefix prepended to the per-(base, version, environment, rid) app name. Azure Function App names are
    /// globally unique (they form <c>&lt;name&gt;.azurewebsites.net</c>), so the operator supplies a prefix unique to their
    /// deployment.
    /// </summary>
    public required string AppNamePrefix { get; init; }

    /// <summary>Gets the Function App HTTP-trigger invoke path appended to the app base URL. Defaults to <c>api/invoke</c>.</summary>
    public string InvokePath { get; init; } = "api/invoke";

    /// <summary>
    /// Gets the source app settings set on the Function App — the deployed environment's source configuration the baked
    /// worker's transport binder reads (each <c>ARAZZO_SOURCE__&lt;name&gt;</c> is a source's base URL). Empty by default.
    /// </summary>
    public IReadOnlyDictionary<string, string>? FunctionAppSettings { get; init; }
}