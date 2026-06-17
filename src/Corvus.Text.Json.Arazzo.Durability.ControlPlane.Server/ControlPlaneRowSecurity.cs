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
    /// Resolves the capability scopes (§14.1) this policy grants the principal directly — the Arazzo-plane
    /// per-principal grants that are <em>unioned</em> with the scopes the principal already carries in claims, so an
    /// access-request approval can grant run/admin capability (e.g. <c>runs:write</c>) without mutating the IdP. The
    /// deployment stamps these into the principal's effective scopes at authentication time (see the entitlement
    /// claims transformer). The default grants none — scopes come solely from claims.
    /// </summary>
    /// <param name="principal">The authenticated principal (<see langword="null"/> if unauthenticated).</param>
    /// <returns>The granted scopes (empty when none); the common path allocates nothing.</returns>
    public virtual IReadOnlyList<string> ResolveGrantedScopes(ClaimsPrincipal? principal) => [];

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

    /// <summary>Gets the reserved internal tag prefix this policy stamps/maps to (default <see cref="SecurityShell.DefaultInternalPrefix"/>).</summary>
    protected virtual string InternalTagPrefix => SecurityShell.DefaultInternalPrefix;

    /// <summary>Maps operator-supplied source credential usage grants (§13) to the <strong>internal</strong> security
    /// tags a run must carry to use the binding — so a binding's usage scope is expressed in unforgeable, deployment-
    /// stamped identity terms rather than free-form labels. The default maps a grant <c>{dimension, value}</c> to the
    /// internal tag <c>{prefix+dimension = value}</c> (e.g. <c>{workflow, nightly-reconcile}</c> →
    /// <c>sys:workflow=nightly-reconcile</c>); a deployment may override to restrict which grants are permitted.</summary>
    /// <param name="grants">The operator-supplied usage grants.</param>
    /// <returns>The internal usage tags for the binding.</returns>
    public virtual IReadOnlyList<SecurityTag> ResolveUsageGrants(IReadOnlyList<CredentialUsageGrant> grants)
    {
        if (grants.Count == 0)
        {
            return [];
        }

        var tags = new List<SecurityTag>(grants.Count);
        foreach (CredentialUsageGrant grant in grants)
        {
            tags.Add(new SecurityTag(this.InternalTagPrefix + grant.Dimension, grant.Value));
        }

        return tags;
    }

    /// <summary>Describes a binding's stored internal usage tags back as operator-facing grants (the inverse of
    /// <see cref="ResolveUsageGrants"/>) for the management API response. The default strips the internal prefix from
    /// each tag's key; tags without the prefix are ignored.</summary>
    /// <param name="usageTags">The binding's internal usage tags.</param>
    /// <returns>The usage grants.</returns>
    public virtual IReadOnlyList<CredentialUsageGrant> DescribeUsageScope(IReadOnlyList<SecurityTag> usageTags)
    {
        if (usageTags.Count == 0)
        {
            return [];
        }

        var grants = new List<CredentialUsageGrant>(usageTags.Count);
        foreach (SecurityTag tag in usageTags)
        {
            if (tag.Key.StartsWith(this.InternalTagPrefix, StringComparison.Ordinal))
            {
                grants.Add(new CredentialUsageGrant(tag.Key[this.InternalTagPrefix.Length..], tag.Value));
            }
        }

        return grants;
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

    /// <summary>Gets the current request's authenticated principal (e.g. for a handler that needs a specific claim such
    /// as the requesting subject); <see langword="null"/> when unscoped or unauthenticated.</summary>
    internal ClaimsPrincipal? CurrentPrincipal => this.Principal;

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

    /// <summary>Maps source credential usage grants to internal usage tags (empty when unscoped).</summary>
    /// <param name="grants">The operator-supplied usage grants.</param>
    /// <returns>The internal usage tags.</returns>
    public IReadOnlyList<SecurityTag> ResolveUsageGrants(IReadOnlyList<CredentialUsageGrant> grants) => this.policy?.ResolveUsageGrants(grants) ?? [];

    /// <summary>Describes internal usage tags back as operator-facing grants (empty when unscoped).</summary>
    /// <param name="usageTags">The binding's internal usage tags.</param>
    /// <returns>The usage grants.</returns>
    public IReadOnlyList<CredentialUsageGrant> DescribeUsageScope(IReadOnlyList<SecurityTag> usageTags) => this.policy?.DescribeUsageScope(usageTags) ?? [];
}