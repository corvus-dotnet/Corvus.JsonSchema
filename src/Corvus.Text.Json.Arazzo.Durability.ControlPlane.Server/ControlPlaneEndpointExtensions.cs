// <copyright file="ControlPlaneEndpointExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="rowSecurity">
    /// The deployment's row-security policy (§14.2): it scopes every list/search to the rows the principal may
    /// see, gates single-row reads/writes (an invisible row is reported as not found), scopes purge to the
    /// principal's reach, and stamps deployment-internal tags onto created rows. When supplied, the host must also
    /// register <c>IHttpContextAccessor</c> (<c>services.AddHttpContextAccessor()</c>) so the current principal can
    /// be read. When <see langword="null"/> (the default) the control plane is unscoped — every row is visible.
    /// </param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    /// <remarks>
    /// Authentication is always the host's concern: the control plane depends only on a <c>ClaimsPrincipal</c>
    /// and the named scope policies, so a deployment supplies any ASP.NET Core scheme (JWT bearer, OIDC, mTLS,
    /// a dev key) and how a principal acquires scopes.
    /// </remarks>
    public static IEndpointRouteBuilder MapArazzoControlPlane(this IEndpointRouteBuilder endpoints, IWorkflowManagementClient management, IWorkflowCatalogClient catalog, IRunnerRegistry runners, bool requireAuthorization = false, ControlPlaneRowSecurityPolicy? rowSecurity = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runners);

        // Resolve the caller's AccessContext per request (§14.2/§14.4): when a row-security policy is configured the
        // handlers gate every read/write/purge by the principal's per-verb reach — the client operations require a
        // context, so an unscoped read cannot exist to be misused. With no policy the access binding yields
        // AccessContext.System throughout (fully unrestricted, behaviour unchanged). The current principal is read
        // via IHttpContextAccessor, which the host must register (services.AddHttpContextAccessor()).
        ControlPlaneAccess access = rowSecurity is null
            ? new ControlPlaneAccess()
            : new ControlPlaneAccess(endpoints.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), rowSecurity);

        return endpoints.MapApiEndpoints(
            new ArazzoControlPlaneHandler(management, access),
            new ArazzoControlPlaneRunnersHandler(runners),
            new ArazzoControlPlaneCatalogHandler(catalog, management, runners, access),
            requireAuthorization ? ControlPlaneAuthorization.RequireDeclaredScopes : null);
    }
}