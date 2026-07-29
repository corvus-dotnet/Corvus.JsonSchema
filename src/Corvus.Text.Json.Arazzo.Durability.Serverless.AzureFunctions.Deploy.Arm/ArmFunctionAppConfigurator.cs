// <copyright file="ArmFunctionAppConfigurator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;

/// <summary>
/// The Azure Resource Manager <see cref="IFunctionAppConfigurator"/> (ADR 0059, ADR 0061): the real management-plane half
/// of a run-from-package deploy. It points the target <c>dotnet-isolated</c> Function App at the uploaded package (the
/// <c>WEBSITE_RUN_FROM_PACKAGE</c> app setting) and sets the deployed environment's source app settings, then returns the
/// app's default host so the deployer can form the invoke URL.
/// </summary>
/// <remarks>
/// The <c>ArmClient</c> is injected: the runner constructs it with the environment's credential (a managed identity or a
/// least-privileged service principal), so this class holds no cloud identity of its own — the runner is the secure
/// boundary (ADR 0059). There is no ARM emulator (Azurite emulates Storage only), so this half is verified against real
/// Azure, the analogue of Lambda's <c>AWS_IAM</c> auth being real-AWS-only (ADR 0060, ADR 0061); the deployer's storage
/// half is verified locally against Azurite. It updates the app's settings by merging <c>WEBSITE_RUN_FROM_PACKAGE</c> and
/// the source settings over the app's existing settings, so the runtime settings set when the app was provisioned
/// (storage, worker runtime, functions version) are preserved.
/// </remarks>
public sealed class ArmFunctionAppConfigurator : IFunctionAppConfigurator
{
    private const string RunFromPackageSettingName = "WEBSITE_RUN_FROM_PACKAGE";

    private readonly ArmClient armClient;
    private readonly ArmFunctionAppConfiguratorOptions options;

    /// <summary>Initializes a new instance of the <see cref="ArmFunctionAppConfigurator"/> class.</summary>
    /// <param name="armClient">The ARM client, constructed by the runner with the environment's credential.</param>
    /// <param name="options">The subscription, resource group, and app-name prefix that locate the target Function App.</param>
    public ArmFunctionAppConfigurator(ArmClient armClient, ArmFunctionAppConfiguratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(armClient);
        ArgumentNullException.ThrowIfNull(options);
        this.armClient = armClient;
        this.options = options;
    }

    /// <inheritdoc/>
    public async ValueTask<Uri> ApplyRunFromPackageAsync(
        ServerlessDeployRequest request,
        Uri packageUrl,
        IReadOnlyDictionary<string, string> appSettings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageUrl);
        ArgumentNullException.ThrowIfNull(appSettings);

        string appName = AppName(request, this.options.AppNamePrefix);
        ResourceIdentifier siteId = WebSiteResource.CreateResourceIdentifier(this.options.SubscriptionId, this.options.ResourceGroupName, appName);
        WebSiteResource site = this.armClient.GetWebSiteResource(siteId);

        // Merge run-from-package and the source settings over the app's existing settings, so the runtime settings the app
        // was provisioned with (AzureWebJobsStorage, FUNCTIONS_WORKER_RUNTIME, FUNCTIONS_EXTENSION_VERSION) are preserved.
        AppServiceConfigurationDictionary current = await site.GetApplicationSettingsAsync(cancellationToken).ConfigureAwait(false);
        current.Properties[RunFromPackageSettingName] = packageUrl.AbsoluteUri;
        foreach ((string key, string value) in appSettings)
        {
            current.Properties[key] = value;
        }

        await site.UpdateApplicationSettingsAsync(current, cancellationToken).ConfigureAwait(false);

        if (this.options.RestartAfterConfigure)
        {
            // Run-from-package is applied on app start, so a restart makes the newly-pointed package take effect.
            await site.RestartAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Hydrate the site to read its default host name, which forms the app base URL the deployer appends the invoke path to.
        WebSiteResource hydrated = await site.GetAsync(cancellationToken).ConfigureAwait(false);
        return new Uri($"https://{hydrated.Data.DefaultHostName}/");
    }

    /// <summary>
    /// The globally-unique Function App name for a deploy target: <c>{prefix}-{base}-v{ver}-{env}-{rid}</c>, lowercased
    /// with any character outside <c>[a-z0-9-]</c> mapped to <c>-</c>, trimmed of leading/trailing hyphens, and truncated
    /// to Azure's 60-character site-name limit with a short deterministic hash suffix so two over-long targets never
    /// collide. The prefix carries the deployment's global-uniqueness; the suffix identifies the target within it.
    /// </summary>
    /// <param name="request">The deploy request whose target tuple names the app.</param>
    /// <param name="prefix">The globally-unique prefix the operator supplies.</param>
    /// <returns>The Function App name.</returns>
    internal static string AppName(ServerlessDeployRequest request, string prefix)
    {
        string body = $"{prefix}-{request.BaseWorkflowId}-v{request.VersionNumber.ToString(CultureInfo.InvariantCulture)}-{request.Environment}-{request.RuntimeIdentifier}";
        string sanitized = Sanitize(body);
        if (sanitized.Length <= 60)
        {
            return sanitized;
        }

        // Too long for an Azure site name: keep a truncated prefix and append a short deterministic hash of the full name
        // so two distinct over-long targets never collide.
        string suffix = ShortHash(sanitized);
        return $"{sanitized[..(60 - 1 - suffix.Length)].TrimEnd('-')}-{suffix}";
    }

    private static string Sanitize(string value)
    {
        // Lowercase and map any character outside the Azure site-name set to '-', then collapse to a name that neither
        // starts nor ends with a hyphen.
        Span<char> buffer = value.Length <= 128 ? stackalloc char[value.Length] : new char[value.Length];
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            buffer[i] = (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-') ? c : '-';
        }

        return new string(buffer).Trim('-');
    }

    private static string ShortHash(string value)
    {
        Span<byte> hash = stackalloc byte[System.Security.Cryptography.SHA256.HashSizeInBytes];
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value), hash);
        return Convert.ToHexStringLower(hash[..4]);
    }
}