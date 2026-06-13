// <copyright file="ControlPlaneEndpointExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Routing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Maps the Arazzo control-plane REST API onto an ASP.NET Core endpoint route builder.
/// </summary>
public static class ControlPlaneEndpointExtensions
{
    /// <summary>
    /// Maps the control-plane endpoints (the generated routes from the OpenAPI description) onto
    /// <paramref name="endpoints"/>, backed by the given management client.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="management">The run control-plane client the run endpoints delegate to.</param>
    /// <param name="catalog">The catalog client the catalog endpoints delegate to.</param>
    /// <param name="runners">The runner registry the runners endpoint reads and the trigger gate consults.</param>
    /// <param name="requireAuthorization">
    /// When <see langword="true"/>, each endpoint demands its declared capability scope as an authorization
    /// policy (<see cref="ControlPlaneScopes"/>); the host must register those policies (e.g.
    /// <see cref="ControlPlaneAuthorization.AddArazzoControlPlaneAuthorization"/>) and an authentication scheme,
    /// and call <c>UseAuthentication</c>/<c>UseAuthorization</c>. When <see langword="false"/> (the default)
    /// the endpoints are unsecured — for tests and trusted-network deployments.
    /// </param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    /// <remarks>
    /// Authentication is always the host's concern: the control plane depends only on a <c>ClaimsPrincipal</c>
    /// and the named scope policies, so a deployment supplies any ASP.NET Core scheme (JWT bearer, OIDC, mTLS,
    /// a dev key) and how a principal acquires scopes.
    /// </remarks>
    public static IEndpointRouteBuilder MapArazzoControlPlane(this IEndpointRouteBuilder endpoints, IWorkflowManagementClient management, IWorkflowCatalogClient catalog, IRunnerRegistry runners, bool requireAuthorization = false)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runners);
        return endpoints.MapApiEndpoints(
            new ArazzoControlPlaneHandler(management),
            new ArazzoControlPlaneRunnersHandler(runners),
            new ArazzoControlPlaneCatalogHandler(catalog, management, runners),
            requireAuthorization ? ControlPlaneAuthorization.RequireDeclaredScopes : null);
    }
}