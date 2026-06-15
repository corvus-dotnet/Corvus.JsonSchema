// <copyright file="ControlPlaneAuthorization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The capability scopes the control-plane endpoints require. Each is used both as the authorization
/// <em>policy name</em> the endpoints demand and as the scope value a principal must carry — so a deployment
/// registers a policy per scope (see <see cref="ControlPlaneAuthorization.AddArazzoControlPlaneAuthorization"/>)
/// and the endpoint convention demands the right one per operation.
/// </summary>
public static class ControlPlaneScopes
{
    /// <summary>Read catalog versions, packages, schemas, and source documents.</summary>
    public const string CatalogRead = "catalog:read";

    /// <summary>Add or update catalog versions.</summary>
    public const string CatalogWrite = "catalog:write";

    /// <summary>Delete or purge catalog versions.</summary>
    public const string CatalogPurge = "catalog:purge";

    /// <summary>List and read runs and runners.</summary>
    public const string RunsRead = "runs:read";

    /// <summary>Start (trigger), resume, or cancel runs.</summary>
    public const string RunsWrite = "runs:write";

    /// <summary>Delete or purge runs.</summary>
    public const string RunsPurge = "runs:purge";

    /// <summary>Read the row-security policy (rules and claim→rule bindings).</summary>
    public const string SecurityRead = "security:read";

    /// <summary>Author the row-security policy (create/update/delete rules and bindings).</summary>
    public const string SecurityWrite = "security:write";

    /// <summary>Read source credential bindings (references and metadata only — never secret material).</summary>
    public const string CredentialsRead = "credentials:read";

    /// <summary>Manage source credential bindings (references and metadata only — never secret material).</summary>
    public const string CredentialsWrite = "credentials:write";

    /// <summary>Gets all control-plane capability scopes.</summary>
    public static IReadOnlyList<string> All { get; } = [CatalogRead, CatalogWrite, CatalogPurge, RunsRead, RunsWrite, RunsPurge, SecurityRead, SecurityWrite, CredentialsRead, CredentialsWrite];
}

/// <summary>
/// Wires capability-scope authorization onto the control plane. The control plane ships the scopes as policy
/// names and demands the right one per endpoint; the deployment owns <em>authentication</em> (any ASP.NET Core
/// scheme) and how a principal acquires scopes. <see cref="AddArazzoControlPlaneAuthorization"/> registers a
/// default policy per scope (an authenticated principal carrying the scope in a scope claim); a deployment may
/// register its own policies of the same names instead.
/// </summary>
public static class ControlPlaneAuthorization
{
    /// <summary>
    /// Registers one authorization policy per <see cref="ControlPlaneScopes"/> value: an authenticated
    /// principal whose <paramref name="scopeClaimType"/> claim carries the scope (OAuth-style space-delimited
    /// values are split). Call <c>MapArazzoControlPlane(..., requireAuthorization: true)</c> to demand them.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scopeClaimType">The claim type carrying granted scopes (default <c>scope</c>).</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddArazzoControlPlaneAuthorization(this IServiceCollection services, string scopeClaimType = "scope")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(scopeClaimType);

        services.AddAuthorization(options =>
        {
            foreach (string scope in ControlPlaneScopes.All)
            {
                string required = scope;
                options.AddPolicy(scope, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context => HasScope(context.User, scopeClaimType, required)));
            }
        });

        return services;
    }

    /// <summary>
    /// A <see cref="ConfigureEndpoint"/> convention that demands the operation's declared capability scopes as
    /// authorization policies. An operation with an anonymous (<c>{}</c>) alternative is left open; one that
    /// declares security but no scopes (e.g. mTLS-only) requires an authenticated principal; otherwise every
    /// declared scope is required (the control-plane operations declare one scope shared across their schemes).
    /// </summary>
    /// <param name="endpoint">The descriptor for the operation being mapped.</param>
    /// <param name="builder">The endpoint convention builder.</param>
    internal static void RequireDeclaredScopes(in EndpointDescriptor endpoint, IEndpointConventionBuilder builder)
    {
        foreach (EndpointSecurityRequirementSet alternative in endpoint.SecurityRequirements)
        {
            if (alternative.IsOptional)
            {
                builder.AllowAnonymous();
                return;
            }
        }

        var scopes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (EndpointSecurityRequirementSet alternative in endpoint.SecurityRequirements)
        {
            foreach (EndpointSecurityRequirement requirement in alternative.Requirements)
            {
                foreach (string scope in requirement.Scopes)
                {
                    scopes.Add(scope);
                }
            }
        }

        if (scopes.Count == 0)
        {
            builder.RequireAuthorization();
            return;
        }

        foreach (string scope in scopes)
        {
            builder.RequireAuthorization(scope);
        }
    }

    private static bool HasScope(ClaimsPrincipal user, string scopeClaimType, string scope)
    {
        foreach (Claim claim in user.Claims)
        {
            if (!string.Equals(claim.Type, scopeClaimType, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string part in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(part, scope, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}