// <copyright file="ControlPlaneRowSecurity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.AspNetCore.Http;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// A deployment's row-security policy (design §14.2/§14.3): it turns the authenticated
/// <see cref="ClaimsPrincipal"/> into the caller's <see cref="AccessContext"/> — the row reach the principal has
/// for each <see cref="AccessVerb"/>, so read can be granted independently of write and purge. It also supplies
/// the internal tags stamped onto rows the principal creates (e.g. <c>sys:tenant=acme</c>) and validates
/// user-supplied tags. A deployment typically implements this over a <see cref="SecurityShell"/>: the shell owns
/// the mandated wrapper rule, the reserved prefix, and tag stripping, and this policy maps the principal's claims
/// onto it.
/// </summary>
/// <remarks>
/// Authentication and how a principal acquires claims are always the host's concern (§14.1). The control plane
/// depends only on this policy and a <see cref="ClaimsPrincipal"/> read through <see cref="IHttpContextAccessor"/>.
/// Pass an instance to <c>MapArazzoControlPlane(..., rowSecurity:)</c> (and call
/// <c>services.AddHttpContextAccessor()</c>) to switch enforcement on; omit it (the default) and the control plane
/// runs with <see cref="AccessContext.System"/> throughout — fully unrestricted, behaviour unchanged.
/// </remarks>
public abstract class ControlPlaneRowSecurityPolicy
{
    /// <summary>
    /// Resolves the caller's access grant from the authenticated principal. The grant's per-verb reach scopes
    /// every list/search and gates each single-row read/write/purge — there is no unscoped path. Return
    /// <see cref="AccessContext.System"/> (or a context with <see langword="null"/> reaches) for an unrestricted
    /// principal such as an operator.
    /// </summary>
    /// <param name="principal">The authenticated principal (<see langword="null"/> if the request is unauthenticated).</param>
    /// <returns>The caller's access grant; never <see langword="null"/>.</returns>
    public abstract AccessContext Resolve(ClaimsPrincipal? principal);

    /// <summary>
    /// Returns the deployment-internal security tags to stamp onto rows the principal creates (e.g. the
    /// principal's tenant as <c>sys:tenant=acme</c>), making the new row owned by the principal's slice of the
    /// shell. These carry the reserved internal prefix and are not user-editable. The default stamps nothing.
    /// </summary>
    /// <param name="principal">The authenticated principal creating the row.</param>
    /// <returns>The internal tags to stamp; empty to stamp none.</returns>
    public virtual IReadOnlyList<SecurityTag> GetInternalTags(ClaimsPrincipal? principal) => [];

    /// <summary>
    /// Validates user-supplied security tags before a row is created (e.g. rejecting the reserved internal prefix
    /// via <see cref="SecurityShell.ValidateUserTags"/>). The default accepts everything.
    /// </summary>
    /// <param name="userTags">The security tags supplied through the user-facing API.</param>
    /// <exception cref="ArgumentException">A user tag is not permitted (e.g. it uses the reserved internal prefix).</exception>
    public virtual void ValidateUserTags(IReadOnlyList<SecurityTag> userTags)
    {
    }
}

/// <summary>
/// Binds a <see cref="ControlPlaneRowSecurityPolicy"/> to the current request's principal (read through
/// <see cref="IHttpContextAccessor"/>) so the handlers can resolve the caller's <see cref="AccessContext"/>
/// without depending on ASP.NET Core themselves. When constructed without a policy it yields
/// <see cref="AccessContext.System"/> for every request — the unscoped default.
/// </summary>
internal sealed class ControlPlaneAccess
{
    private readonly IHttpContextAccessor? httpContextAccessor;
    private readonly ControlPlaneRowSecurityPolicy? policy;

    /// <summary>Initializes an unscoped instance: every request resolves to <see cref="AccessContext.System"/>.</summary>
    public ControlPlaneAccess()
    {
    }

    /// <summary>Initializes a scoped instance backed by a deployment policy.</summary>
    /// <param name="httpContextAccessor">The accessor used to read the current request's principal.</param>
    /// <param name="policy">The deployment's row-security policy.</param>
    public ControlPlaneAccess(IHttpContextAccessor httpContextAccessor, ControlPlaneRowSecurityPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        ArgumentNullException.ThrowIfNull(policy);
        this.httpContextAccessor = httpContextAccessor;
        this.policy = policy;
    }

    private ClaimsPrincipal? Principal => this.httpContextAccessor?.HttpContext?.User;

    /// <summary>Resolves the current request's access grant (<see cref="AccessContext.System"/> when unscoped).</summary>
    /// <returns>The caller's access grant.</returns>
    public AccessContext Current() => this.policy is null ? AccessContext.System : this.policy.Resolve(this.Principal);

    /// <summary>Returns the internal tags to stamp onto a row the current principal creates.</summary>
    /// <returns>The internal tags; empty when unscoped or none configured.</returns>
    public IReadOnlyList<SecurityTag> InternalTags() => this.policy?.GetInternalTags(this.Principal) ?? [];

    /// <summary>Validates user-supplied security tags for the current principal (no-op when unscoped).</summary>
    /// <param name="userTags">The user-supplied security tags.</param>
    /// <exception cref="ArgumentException">A user tag is not permitted.</exception>
    public void ValidateUserTags(IReadOnlyList<SecurityTag> userTags) => this.policy?.ValidateUserTags(userTags);
}