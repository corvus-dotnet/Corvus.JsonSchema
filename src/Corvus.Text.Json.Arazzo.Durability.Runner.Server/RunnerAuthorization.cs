// <copyright file="RunnerAuthorization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Wires scope authorization onto the runner API. The API ships its scope as a policy name and demands it;
/// the deployment owns authentication and how a runner acquires the scope.
/// </summary>
public static class RunnerAuthorization
{
    /// <summary>
    /// Registers the <see cref="RunnerEndpointExtensions.ExecuteScope"/> policy: an authenticated principal whose
    /// <paramref name="scopeClaimType"/> claim carries the scope. A deployment may register a policy of that name
    /// itself instead, and one identifying its runners by client certificate maps the API with no required scope at
    /// all and needs neither.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="scopeClaimType">The claim type carrying granted scopes (default <c>scope</c>).</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <remarks>
    /// The scope is the door, not the reach. Holding it lets a runner ask the API for work at all; which work it is
    /// offered comes from the environments its <see cref="MachinePrincipal">machine principal</see> is bound to, which
    /// no token can widen.
    /// </remarks>
    public static IServiceCollection AddArazzoRunnerAuthorization(this IServiceCollection services, string scopeClaimType = ScopeClaims.DefaultScopeClaimType)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(scopeClaimType);

        services.AddAuthorization(options => options.AddPolicy(
            RunnerEndpointExtensions.ExecuteScope,
            policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => ScopeClaims.Has(context.User, scopeClaimType, RunnerEndpointExtensions.ExecuteScope))));

        return services;
    }
}