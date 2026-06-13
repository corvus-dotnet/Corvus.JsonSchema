// <copyright file="ControlPlaneRowSecurity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// A deployment's row-security policy (design §14.2/§14.3): it turns the authenticated
/// <see cref="ClaimsPrincipal"/> into the access-control decisions the control plane enforces — the read
/// <see cref="SecurityFilter"/> that scopes every list/get to the rows the principal may see, the internal
/// security tags stamped onto rows the principal creates (e.g. <c>sys:tenant=acme</c>), and validation of any
/// user-supplied tags. A deployment typically implements this over a <see cref="SecurityShell"/>: the shell
/// owns the mandated wrapper rule, the reserved prefix, and tag stripping, and this policy maps the principal's
/// claims onto it.
/// </summary>
/// <remarks>
/// Authentication and how a principal acquires claims are always the host's concern (§14.1). The control plane
/// depends only on this policy and a <see cref="ClaimsPrincipal"/> read through
/// <see cref="IHttpContextAccessor"/>, so a deployment supplies any ASP.NET Core scheme and any tenancy model.
/// Pass an instance to <c>MapArazzoControlPlane(..., rowSecurity:)</c> (and call
/// <c>services.AddHttpContextAccessor()</c>) to switch enforcement on; omit it (the default) for an unscoped
/// control plane.
/// </remarks>
public abstract class ControlPlaneRowSecurityPolicy
{
    /// <summary>
    /// Builds the read filter for the principal: the store applies it to every list/search, and single-row
    /// reads/writes are gated by it. Return <see langword="null"/> to leave the principal unrestricted (e.g. an
    /// operator). The same filter must gate single-row access so a row the filter excludes is indistinguishable
    /// from one that does not exist.
    /// </summary>
    /// <param name="principal">The authenticated principal (<see langword="null"/> if the request is unauthenticated).</param>
    /// <returns>The row-authorization filter, or <see langword="null"/> for unrestricted access.</returns>
    public abstract SecurityFilter? GetFilter(ClaimsPrincipal? principal);

    /// <summary>
    /// Returns the deployment-internal security tags to stamp onto rows the principal creates (e.g. the
    /// principal's tenant as <c>sys:tenant=acme</c>), making the new row owned by the principal's slice of the
    /// shell. These carry the reserved internal prefix and are not user-editable. The default stamps nothing.
    /// </summary>
    /// <param name="principal">The authenticated principal creating the row.</param>
    /// <returns>The internal tags to stamp; empty to stamp none.</returns>
    public virtual IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [];

    /// <summary>
    /// Validates user-supplied security tags before a row is created (e.g. rejecting the reserved internal
    /// prefix via <see cref="SecurityShell.ValidateUserTags"/>). The default accepts everything.
    /// </summary>
    /// <param name="userTags">The security tags supplied through the user-facing API.</param>
    /// <exception cref="ArgumentException">A user tag is not permitted (e.g. it uses the reserved internal prefix).</exception>
    public virtual void ValidateUserTags(IReadOnlyList<SecurityTag> userTags)
    {
    }
}

/// <summary>
/// Binds a <see cref="ControlPlaneRowSecurityPolicy"/> to the current request's principal (read through
/// <see cref="IHttpContextAccessor"/>) so the handlers can enforce row security without taking a dependency on
/// ASP.NET Core themselves. Constructed by <c>MapArazzoControlPlane</c> only when a policy is supplied.
/// </summary>
internal sealed class ControlPlaneRowSecurity
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ControlPlaneRowSecurityPolicy policy;

    /// <summary>Initializes a new instance of the <see cref="ControlPlaneRowSecurity"/> class.</summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's principal.</param>
    /// <param name="policy">The deployment's row-security policy.</param>
    public ControlPlaneRowSecurity(IHttpContextAccessor httpContextAccessor, ControlPlaneRowSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(policy);
        this.httpContextAccessor = httpContextAccessor;
        this.policy = policy;
    }

    private ClaimsPrincipal? Principal => this.httpContextAccessor.HttpContext?.User;

    /// <summary>Builds the read filter for the current principal (<see langword="null"/> if unrestricted).</summary>
    /// <returns>The row-authorization filter, or <see langword="null"/>.</returns>
    public SecurityFilter? Filter() => this.policy.GetFilter(this.Principal);

    /// <summary>Whether a row carrying the given security tags is visible to the current principal.</summary>
    /// <param name="securityTags">The row's security tags (<see langword="null"/> treated as none).</param>
    /// <returns><see langword="true"/> if the row is visible (or the principal is unrestricted).</returns>
    public bool IsVisible(IReadOnlyList<SecurityTag>? securityTags)
        => this.Filter()?.IsSatisfiedBy(securityTags ?? []) ?? true;

    /// <summary>Returns the internal tags to stamp onto a row the current principal creates.</summary>
    /// <returns>The internal tags; empty to stamp none.</returns>
    public IReadOnlyList<SecurityTag> InternalTags() => this.policy.GetInternalTags(this.Principal);

    /// <summary>Validates user-supplied security tags for the current principal.</summary>
    /// <param name="userTags">The user-supplied security tags.</param>
    /// <exception cref="ArgumentException">A user tag is not permitted.</exception>
    public void ValidateUserTags(IReadOnlyList<SecurityTag> userTags) => this.policy.ValidateUserTags(userTags);
}