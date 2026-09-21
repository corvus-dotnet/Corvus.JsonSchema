// <copyright file="ArmFunctionInvokeAccess.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;

/// <summary>
/// Establishes a Function App's invoke access through the management plane before anything is published to it
/// (ADR 0059 decision 4): it checks the app's Entra posture when Entra is configured, and sets the invoke function key.
/// </summary>
internal static class ArmFunctionInvokeAccess
{
    // The host-level key collection whose keys authorize every function in the app. The app has one function.
    private const string FunctionKeysType = "functionKeys";

    /// <summary>Checks the Entra posture when an audience is configured, then sets the invoke key on the app.</summary>
    /// <param name="site">The target Function App.</param>
    /// <param name="access">The invoke access to establish.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the app holds the key.</returns>
    /// <exception cref="FunctionAppInvokeAccessException">Entra is configured and the app does not require it.</exception>
    public static async ValueTask ApplyAsync(WebSiteResource site, FunctionAppInvokeAccess access, CancellationToken cancellationToken)
    {
        if (access.EntraAudience is { } audience)
        {
            SiteAuthSettingsV2 settings = await site.GetAuthSettingsV2Async(cancellationToken).ConfigureAwait(false);
            AppServiceAadProvider? entra = settings.IdentityProviders?.AzureActiveDirectory;
            string? refusal = EntraPostureRefusal(
                settings.Platform?.IsEnabled,
                settings.GlobalValidation?.IsAuthenticationRequired,
                settings.GlobalValidation?.UnauthenticatedClientAction,
                settings.GlobalValidation?.ExcludedPaths,
                entra?.IsEnabled,
                entra?.Validation?.AllowedAudiences,
                audience);
            if (refusal is not null)
            {
                throw new FunctionAppInvokeAccessException($"Function App '{site.Id.Name}' is not deployed to: {refusal}");
            }
        }

        var key = new WebAppKeyInfo { Properties = new WebAppKeyInfoProperties { Name = AzureFunctionsInvokeAuthorization.KeyName, Value = access.InvokeKey } };
        await site.CreateOrUpdateHostSecretAsync(FunctionKeysType, AzureFunctionsInvokeAuthorization.KeyName, key, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decides whether an app's authentication settings require Entra authentication for the configured audience on
    /// every path. Anything short of that is refused, because the deployment asked for Entra and would not be getting it.
    /// </summary>
    /// <param name="platformEnabled">Whether the app's built-in authentication is enabled.</param>
    /// <param name="authenticationRequired">Whether the app requires authentication.</param>
    /// <param name="unauthenticatedAction">What the app does with an unauthenticated request.</param>
    /// <param name="excludedPaths">The paths the app exempts from authentication.</param>
    /// <param name="entraEnabled">Whether the Entra identity provider is enabled.</param>
    /// <param name="allowedAudiences">The audiences the Entra provider accepts.</param>
    /// <param name="audience">The audience the runner's token is issued for.</param>
    /// <returns>Why the posture is refused, or <see langword="null"/> when it is sufficient.</returns>
    internal static string? EntraPostureRefusal(
        bool? platformEnabled,
        bool? authenticationRequired,
        UnauthenticatedClientActionV2? unauthenticatedAction,
        IEnumerable<string>? excludedPaths,
        bool? entraEnabled,
        IEnumerable<string>? allowedAudiences,
        string audience)
    {
        if (platformEnabled != true)
        {
            return "Entra authentication is configured for the invoke, and the app's built-in authentication is not enabled.";
        }

        if (authenticationRequired != true)
        {
            return "Entra authentication is configured for the invoke, and the app does not require authentication.";
        }

        if (unauthenticatedAction is not (UnauthenticatedClientActionV2.Return401 or UnauthenticatedClientActionV2.Return403))
        {
            return "Entra authentication is configured for the invoke, and the app does not refuse an unauthenticated request with a 401 or a 403.";
        }

        if (excludedPaths?.Any() == true)
        {
            return "Entra authentication is configured for the invoke, and the app exempts paths from authentication.";
        }

        if (entraEnabled == false)
        {
            return "Entra authentication is configured for the invoke, and the app's Microsoft Entra identity provider is disabled.";
        }

        string wanted = audience.TrimEnd('/');
        if (allowedAudiences?.Any(a => string.Equals(a.TrimEnd('/'), wanted, StringComparison.OrdinalIgnoreCase)) != true)
        {
            return $"Entra authentication is configured for the invoke, and the app's Microsoft Entra identity provider does not accept the audience '{audience}'.";
        }

        return null;
    }
}