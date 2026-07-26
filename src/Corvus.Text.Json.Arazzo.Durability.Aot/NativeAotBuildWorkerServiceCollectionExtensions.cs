// <copyright file="NativeAotBuildWorkerServiceCollectionExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Aot;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the control-plane native build worker (ADR 0055) as a hosted background service.
/// </summary>
public static class NativeAotBuildWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Adds the <see cref="NativeBuildBackgroundService"/> and the <see cref="NativeBuildWorker"/> it drives. The host must
    /// already have registered the worker's collaborators in the container: an
    /// <see cref="Corvus.Text.Json.Arazzo.Durability.Publishing.INativeBuildJobStore"/>, an
    /// <see cref="Corvus.Text.Json.Arazzo.Durability.IWorkflowCatalogStore"/>, and a <see cref="WorkflowAotBuildService"/>
    /// (with the runtime target's <see cref="IWorkflowAotBuilder"/> and the trust store's signer and verifier).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">The worker identity and poll cadence.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddNativeAotBuildWorker(this IServiceCollection services, NativeBuildWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton(options);
        services.AddSingleton<NativeBuildWorker>();
        services.AddHostedService<NativeBuildBackgroundService>();
        return services;
    }
}