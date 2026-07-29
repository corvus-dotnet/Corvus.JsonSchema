// <copyright file="AzureFunctionsDeployerOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;

/// <summary>
/// The options an <see cref="AzureFunctionsServerlessDeployer"/> needs beyond the injected package
/// <c>BlobContainerClient</c> and <see cref="IFunctionAppConfigurator"/>: the HTTP-trigger invoke path, the source app
/// settings the baked worker reads, and whether to hand the platform a read SAS for the package. The runner supplies these
/// from the environment's configuration; the deployer holds no cloud identity of its own (ADR 0059, the runner is the
/// secure boundary).
/// </summary>
public sealed record AzureFunctionsDeployerOptions
{
    /// <summary>Gets the Function App HTTP-trigger invoke path appended to the app base URL. Defaults to <c>api/invoke</c> (the baked <c>[Function("invoke")]</c> trigger's route).</summary>
    public string InvokePath { get; init; } = "api/invoke";

    /// <summary>
    /// Gets the source app settings set on the Function App — the deployed environment's source configuration the baked
    /// worker's transport binder reads (each <c>ARAZZO_SOURCE__&lt;name&gt;</c> is a source's base URL). Empty by default;
    /// the runner supplies it from the environment's source registry (ADR 0059, the runner holds the config).
    /// </summary>
    public IReadOnlyDictionary<string, string>? FunctionAppSettings { get; init; }

    /// <summary>
    /// Gets a value indicating whether to append a read-only SAS to the package URL handed to the platform. Run-from-package
    /// needs the App Service platform to read the package blob, so a SAS is appended when the blob client can generate one
    /// (a shared-key credential); a managed-identity client cannot, and a role assignment or user-delegation SAS is used
    /// instead, so the bare blob URL is handed over. Defaults to <see langword="true"/>.
    /// </summary>
    public bool AppendReadSasToPackageUrl { get; init; } = true;

    /// <summary>Gets the lifetime of the generated read SAS. The package must stay readable for the deployed app's life, so this defaults to 365 days.</summary>
    public TimeSpan PackageSasLifetime { get; init; } = TimeSpan.FromDays(365);
}