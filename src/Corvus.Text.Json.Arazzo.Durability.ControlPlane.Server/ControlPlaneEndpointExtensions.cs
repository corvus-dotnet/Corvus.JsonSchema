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
    /// <returns>The same endpoint route builder, for chaining.</returns>
    /// <remarks>
    /// Authentication/authorization are the host's concern: the generated
    /// <see cref="ApiEndpointRegistration.SecuritySchemes"/>/<see cref="ApiEndpointRegistration.SecurityRequirements"/>
    /// describe the scopes the OpenAPI document declares (runs:* and catalog:*), and the
    /// generated <c>EndpointSecurityConventions.RequireDeclaredAuthorization</c> can apply them when the host
    /// has registered the matching authentication and authorization policies.
    /// </remarks>
    public static IEndpointRouteBuilder MapArazzoControlPlane(this IEndpointRouteBuilder endpoints, IWorkflowManagementClient management, IWorkflowCatalogClient catalog)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(catalog);
        return endpoints.MapApiEndpoints(new ArazzoControlPlaneHandler(management), new ArazzoControlPlaneCatalogHandler(catalog));
    }
}