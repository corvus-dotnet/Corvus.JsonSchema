// <copyright file="WorkflowDeployWorkerServiceCollectionExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the runner-side workflow deploy worker (ADR 0059) as a hosted background service.
/// </summary>
public static class WorkflowDeployWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="WorkflowDeployBackgroundService"/> and the <see cref="WorkflowDeployWorker"/> it drives. The host
    /// must already have registered the worker's collaborators in the container: an
    /// <see cref="Corvus.Text.Json.Arazzo.Durability.Publishing.IWorkflowDeploymentStore"/>, an
    /// <see cref="Corvus.Text.Json.Arazzo.Durability.IWorkflowCatalogStore"/>, and a <see cref="WorkflowDeployService"/>
    /// (with the trust store's verifier and the target platform's <see cref="IServerlessDeployer"/>). Because the deploy
    /// runs in the user's cloud account, the host wires the deployer to the runner's own cloud identity (ADR 0059).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The worker identity and poll cadence.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWorkflowDeployWorker(this IServiceCollection services, WorkflowDeployWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<WorkflowDeployWorker>();
        services.AddHostedService<WorkflowDeployBackgroundService>();
        return services;
    }
}