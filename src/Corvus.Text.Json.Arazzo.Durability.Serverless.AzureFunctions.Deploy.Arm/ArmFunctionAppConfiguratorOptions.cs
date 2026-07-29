// <copyright file="ArmFunctionAppConfiguratorOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;

/// <summary>
/// The options an <see cref="ArmFunctionAppConfigurator"/> needs beyond the injected <c>ArmClient</c>: the subscription
/// and resource group the target Function App lives in, and a prefix that makes the per-target app name globally unique.
/// The runner supplies these from the environment's configuration; the configurator holds no cloud identity of its own
/// (ADR 0059, the runner is the secure boundary — it constructs the <c>ArmClient</c> with the environment's credential).
/// </summary>
public sealed record ArmFunctionAppConfiguratorOptions
{
    /// <summary>Gets the Azure subscription id the target Function App lives in.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>Gets the resource group the target Function App lives in.</summary>
    public required string ResourceGroupName { get; init; }

    /// <summary>
    /// Gets the prefix prepended to the per-(base, version, environment, rid) app name. Azure Function App names are
    /// globally unique (they form <c>&lt;name&gt;.azurewebsites.net</c>), so the deployment's per-target suffix alone is
    /// not enough; the operator supplies a prefix unique to their deployment (for example an org or environment token).
    /// </summary>
    public required string AppNamePrefix { get; init; }

    /// <summary>
    /// Gets a value indicating whether to restart the Function App after updating its settings, so it re-reads the new
    /// package. Run-from-package is applied on app start, so a restart makes the newly-pointed package take effect.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool RestartAfterConfigure { get; init; } = true;
}