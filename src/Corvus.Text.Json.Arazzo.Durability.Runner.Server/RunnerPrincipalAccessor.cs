// <copyright file="RunnerPrincipalAccessor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Reads the authenticated machine principal from the current request (ADR 0065 decision 2). The runner API mints no
/// credential of its own: a runner authenticates with the principal it already has — client credentials, a private-key
/// JWT, or mTLS through the deployment's identity provider — and this is where that identity enters the server.
/// </summary>
/// <remarks>
/// Everything downstream derives from the value this returns: it is the lease owner, the subject of every binding
/// lookup, and what revocation expires leases by. Nothing in a request body or header contributes to it, which is what
/// stops a compromised runner renaming itself into another's leases.
/// </remarks>
public sealed class RunnerPrincipalAccessor
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly string claimType;

    /// <summary>Initializes a new instance of the <see cref="RunnerPrincipalAccessor"/> class.</summary>
    /// <param name="httpContextAccessor">The accessor for the current request.</param>
    /// <param name="options">The deployment's options, naming the claim that carries the principal.</param>
    public RunnerPrincipalAccessor(IHttpContextAccessor httpContextAccessor, RunnerApiOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);

        this.httpContextAccessor = httpContextAccessor;
        this.claimType = (options ?? new RunnerApiOptions()).PrincipalClaimType;
    }

    /// <summary>Resolves the current request's machine principal.</summary>
    /// <returns>The principal, or <see langword="null"/> when the request carries no authenticated identity or none that
    /// names one. There is no fallback to another claim: a request whose principal cannot be established is refused,
    /// because guessing it would decide lease ownership from whatever the token happened to carry.</returns>
    public string? Resolve()
    {
        ClaimsPrincipal? user = this.httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string? principal = user.FindFirstValue(this.claimType);
        return string.IsNullOrEmpty(principal) ? null : principal;
    }
}