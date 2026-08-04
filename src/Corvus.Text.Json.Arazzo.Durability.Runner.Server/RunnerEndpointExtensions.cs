// <copyright file="RunnerEndpointExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Maps the Arazzo runner API (ADR 0065) onto an endpoint route builder: claims, leases, and checkpoints, generated
/// from <c>docs/arazzo/reference/arazzo-runner.openapi.json</c> and wired to the durable store.
/// </summary>
public static class RunnerEndpointExtensions
{
    /// <summary>The scope a runner's token must carry, as declared by the contract.</summary>
    public const string ExecuteScope = "runner:execute";

    /// <summary>
    /// Maps the runner API. The host owns the store; a runner reaches it only through these operations, which is the
    /// whole point of the surface.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder. Map it into a group carrying the contract's base path
    /// (<c>/arazzo/runner/v1</c>) when hosting alongside another API.</param>
    /// <param name="store">The durable run store. It must also implement <see cref="IWorkflowDispatchIndex"/>, since a
    /// claim is answered from the dispatch index.</param>
    /// <param name="bindings">Resolves the environments each machine principal may execute runs for.</param>
    /// <param name="catalog">The catalog the control plane owns, which executor artifacts are served from.</param>
    /// <param name="availability">What each environment has made available, which is what a runner may execute.</param>
    /// <param name="options">The deployment's lease and body bounds; defaults are used when omitted.</param>
    /// <param name="epochs">Mints the epoch each lease grant carries; defaults to <see cref="MonotonicRunnerLeaseEpochSource"/>.</param>
    /// <param name="requiredScope">The scope to require, or <see langword="null"/> to require only an authenticated
    /// principal. A deployment identifying runners by client certificate has no scopes to require and passes
    /// <see langword="null"/>; one issuing tokens leaves the default.</param>
    /// <param name="requireAuthorization">Whether to require an authenticated principal at all. Only a development host
    /// sets this <see langword="false"/>: every operation derives its lease ownership from the principal, so without one
    /// there is nothing to own a lease.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    /// <remarks>
    /// The host must register <c>IHttpContextAccessor</c> (<c>services.AddHttpContextAccessor()</c>): the machine
    /// principal is read from the current request, and it is what every operation is scoped by.
    /// </remarks>
    public static IEndpointRouteBuilder MapArazzoRunnerApi(
        this IEndpointRouteBuilder endpoints,
        IWorkflowStateStore store,
        IWorkflowCatalogStore catalog,
        IAvailabilityStore availability,
        IRunnerEnvironmentBindings bindings,
        RunnerApiOptions? options = null,
        IRunnerLeaseEpochSource? epochs = null,
        string? requiredScope = ExecuteScope,
        bool requireAuthorization = true,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(bindings);

        RunnerApiOptions resolved = options ?? new RunnerApiOptions();
        var principals = new RunnerPrincipalAccessor(endpoints.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), resolved);
        var coordinator = new RunnerRunCoordinator(store, bindings, resolved, epochs, timeProvider);
        var checkpoints = new WorkflowCheckpointCoordinator(store, timeProvider);
        var catalogCoordinator = new RunnerCatalogCoordinator(catalog, availability, bindings, resolved);

        return endpoints.MapApiEndpoints(
            new ArazzoRunnerClaimsHandler(coordinator, principals),
            new ArazzoRunnerLeasesHandler(coordinator, principals),
            new ArazzoRunnerCheckpointsHandler(checkpoints, coordinator, principals, resolved),
            new ArazzoRunnerCatalogHandler(catalogCoordinator, principals),
            requireAuthorization ? (in EndpointDescriptor _, IEndpointConventionBuilder builder) => Authorize(builder, requiredScope) : null);
    }

    private static void Authorize(IEndpointConventionBuilder builder, string? requiredScope)
    {
        if (requiredScope is null)
        {
            builder.RequireAuthorization();
            return;
        }

        builder.RequireAuthorization(requiredScope);
    }
}