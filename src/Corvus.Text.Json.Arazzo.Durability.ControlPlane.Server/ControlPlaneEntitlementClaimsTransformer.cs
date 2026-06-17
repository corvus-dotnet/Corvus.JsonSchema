// <copyright file="ControlPlaneEntitlementClaimsTransformer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// An <see cref="IClaimsTransformation"/> that unions the Arazzo authorization plane's per-principal granted
/// capability scopes (<see cref="ControlPlaneRowSecurityPolicy.ResolveGrantedScopes"/>) into the principal's
/// <c>scope</c> claim at authentication time, so an access-request approval that wrote a capability grant (design
/// §16.5) takes effect at the scope-authorization layer — which reads the <c>scope</c> claim <em>before</em> the
/// handler — without the IdP ever being mutated. Capability is therefore resolved from <em>claims ∪ stored
/// entitlements</em>, the same union the row-security layer applies to reach.
/// </summary>
/// <remarks>
/// The transformer is a per-request warm path, so it is allocation-free on the common path: a principal with no
/// per-principal scope grant resolves the shared empty list and is returned unchanged (no clone, no claim added).
/// Only an actually-elevated principal clones in a scope claim. It is idempotent (ASP.NET Core may invoke a
/// transformer more than once per request): a marker claim short-circuits a second pass.
/// </remarks>
public sealed class ControlPlaneEntitlementClaimsTransformer : IClaimsTransformation
{
    private const string AppliedMarker = "arazzo:entitlement-scopes-applied";
    private readonly ControlPlaneRowSecurityPolicy policy;
    private readonly string scopeClaimType;

    /// <summary>Initializes a new instance of the <see cref="ControlPlaneEntitlementClaimsTransformer"/> class.</summary>
    /// <param name="policy">The deployment's row-security/entitlement policy supplying the per-principal granted scopes.</param>
    /// <param name="scopeClaimType">The claim type carrying granted scopes (default <c>scope</c>; matches <see cref="ControlPlaneAuthorization.AddArazzoControlPlaneAuthorization"/>).</param>
    public ControlPlaneEntitlementClaimsTransformer(ControlPlaneRowSecurityPolicy policy, string scopeClaimType = "scope")
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrEmpty(scopeClaimType);
        this.policy = policy;
        this.scopeClaimType = scopeClaimType;
    }

    /// <inheritdoc/>
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (principal.Identity?.IsAuthenticated != true || principal.HasClaim(static c => c.Type == AppliedMarker))
        {
            return Task.FromResult(principal);
        }

        IReadOnlyList<string> granted = this.policy.ResolveGrantedScopes(principal);
        if (granted.Count == 0)
        {
            // Common path: no per-principal capability grant — nothing to union (no clone, zero allocation).
            return Task.FromResult(principal);
        }

        // Elevated path: stamp the granted scopes as their own scope claim (HasScope unions across all scope claims)
        // plus a marker so a re-invocation is a no-op. A fresh identity leaves the original token principal intact.
        var stamped = new ClaimsIdentity();
        stamped.AddClaim(new Claim(this.scopeClaimType, string.Join(' ', granted)));
        stamped.AddClaim(new Claim(AppliedMarker, "true"));
        principal.AddIdentity(stamped);
        return Task.FromResult(principal);
    }
}

/// <summary>Registration for the control-plane entitlement claims transformer.</summary>
public static class ControlPlaneEntitlementExtensions
{
    /// <summary>
    /// Registers a <see cref="ControlPlaneEntitlementClaimsTransformer"/> that unions the policy's per-principal
    /// granted scopes into each authenticated principal's scope claim. Call this alongside
    /// <see cref="ControlPlaneAuthorization.AddArazzoControlPlaneAuthorization"/> when the deployment authorizes
    /// access requests (§16.5); without it, stored capability grants do not reach the scope-authorization layer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The deployment's row-security/entitlement policy (the same instance passed to <c>MapArazzoControlPlane(rowSecurity:)</c>).</param>
    /// <param name="scopeClaimType">The claim type carrying granted scopes (default <c>scope</c>).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddArazzoControlPlaneEntitlementScopes(this IServiceCollection services, ControlPlaneRowSecurityPolicy policy, string scopeClaimType = "scope")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(policy);
        services.AddSingleton<IClaimsTransformation>(new ControlPlaneEntitlementClaimsTransformer(policy, scopeClaimType));
        return services;
    }
}