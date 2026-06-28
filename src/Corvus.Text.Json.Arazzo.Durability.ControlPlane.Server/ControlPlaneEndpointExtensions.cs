// <copyright file="ControlPlaneEndpointExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
    /// <param name="securityMode">
    /// The control plane's security posture (§17.4) — an <strong>explicit, required</strong> choice (there is no
    /// insecure default): <see cref="ControlPlaneSecurityMode.Open"/> (unauthenticated, unrestricted — dev only),
    /// <see cref="ControlPlaneSecurityMode.Scoped"/> (auth + scopes + <paramref name="rowSecurity"/>, the production
    /// posture), <see cref="ControlPlaneSecurityMode.ScopesOnly"/> (auth + scopes, System reach), or
    /// <see cref="ControlPlaneSecurityMode.RowSecurityOnly"/> (auth + reach, no scope gating). Scope-gating modes need
    /// the host to register the scope policies (e.g. <see cref="ControlPlaneAuthorization.AddArazzoControlPlaneAuthorization"/>)
    /// + an authentication scheme + <c>UseAuthentication</c>/<c>UseAuthorization</c>.
    /// </param>
    /// <param name="rowSecurity">
    /// The deployment's row-security policy (§14.2): it scopes every list/search to the rows the principal may see,
    /// gates single-row reads/writes (an invisible row is reported as not found), scopes purge, and stamps
    /// deployment-internal tags onto created rows. <strong>Required</strong> for <see cref="ControlPlaneSecurityMode.Scoped"/>
    /// and <see cref="ControlPlaneSecurityMode.RowSecurityOnly"/> (and must be omitted otherwise — it would be ignored).
    /// When supplied, the host must also register <c>IHttpContextAccessor</c> (<c>services.AddHttpContextAccessor()</c>).
    /// </param>
    /// <param name="securityPolicyStore">
    /// The persistent store backing the row-security authoring API (<c>/security/rules</c>, <c>/security/bindings</c>,
    /// gated by the <c>security:read</c>/<c>security:write</c> scopes, §14.2). When <see langword="null"/> (the
    /// default) an empty in-memory store is used so the endpoints function in development. If <paramref name="rowSecurity"/>
    /// is a <see cref="PersistentRowSecurityPolicy"/>, it is refreshed after each successful write so authoring
    /// changes take effect in-process.
    /// </param>
    /// <param name="sourceCredentialStore">
    /// The persistent store backing the source-credential management API (<c>/credentials</c>, gated by the
    /// <c>credentials:read</c>/<c>credentials:write</c> scopes, §13). The control plane manages <em>references</em>
    /// only — it never reads, returns, or resolves secret material. When <see langword="null"/> (the default) an empty
    /// in-memory store is used so the endpoints function in development.
    /// </param>
    /// <param name="accessRequestStore">
    /// The persistent store backing the access-request API (<c>/accessRequests</c>, design §16.5). When
    /// <see langword="null"/> (the default) an empty in-memory store is used so the endpoints function in development.
    /// </param>
    /// <param name="accessRequestApprovalOptions">
    /// The platform cap an approval applies (max TTL + grantable scopes, §16.5 guardrail 2). Defaults to run access
    /// only, eight hours.
    /// </param>
    /// <param name="accessRequestSubjectClaimType">The claim type identifying the requesting subject (and that a grant keys on); default <c>sub</c>.</param>
    /// <param name="selfElevationEligibility">
    /// An optional predicate deciding whether a requester is eligible to self-elevate a request (§16.5.3); when it
    /// returns <see langword="true"/> the request is auto-approved without a human approver. Default: never eligible.
    /// </param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    /// <remarks>
    /// Authentication is always the host's concern: the control plane depends only on a <c>ClaimsPrincipal</c>
    /// and the named scope policies, so a deployment supplies any ASP.NET Core scheme (JWT bearer, OIDC, mTLS,
    /// a dev key) and how a principal acquires scopes.
    /// </remarks>
    public static IEndpointRouteBuilder MapArazzoControlPlane(this IEndpointRouteBuilder endpoints, ISecuredWorkflowManagement management, ISecuredWorkflowCatalog catalog, IRunnerRegistry runners, ControlPlaneSecurityMode securityMode, ControlPlaneRowSecurityPolicy? rowSecurity = null, ISecurityPolicyStore? securityPolicyStore = null, ISourceCredentialStore? sourceCredentialStore = null, IAccessRequestStore? accessRequestStore = null, AccessRequestApprovalOptions? accessRequestApprovalOptions = null, string accessRequestSubjectClaimType = "sub", Func<ClaimsPrincipal, AccessRequest, bool>? selfElevationEligibility = null, IObservedIdentityStore? observedIdentityStore = null, IPrincipalDirectory? principalDirectory = null, IEnvironmentStore? environmentStore = null, IEnvironmentAdministratorStore? environmentAdministratorStore = null, ISourceStore? sourceStore = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runners);

        // §17.4 secure-by-default: derive the posture from the EXPLICIT mode — there is no insecure-by-omission path.
        // A row policy is required exactly for the reach-enforcing modes, and forbidden for the System-reach modes (where
        // it would be silently ignored); scope gating is applied exactly for the scope-gating modes.
        if ((securityMode is ControlPlaneSecurityMode.Scoped or ControlPlaneSecurityMode.RowSecurityOnly) && rowSecurity is null)
        {
            throw new ArgumentException($"ControlPlaneSecurityMode.{securityMode} requires a row-security policy; pass rowSecurity, or use ScopesOnly (capability scopes, no row reach) or Open (unsecured).", nameof(rowSecurity));
        }

        if ((securityMode is ControlPlaneSecurityMode.Open or ControlPlaneSecurityMode.ScopesOnly) && rowSecurity is not null)
        {
            throw new ArgumentException($"ControlPlaneSecurityMode.{securityMode} grants full (System) row reach and would ignore a row-security policy; omit rowSecurity, or use Scoped/RowSecurityOnly to enforce it.", nameof(rowSecurity));
        }

        bool gateScopes = securityMode is ControlPlaneSecurityMode.Scoped or ControlPlaneSecurityMode.ScopesOnly;
        ControlPlaneRowSecurityPolicy? effectivePolicy = securityMode is ControlPlaneSecurityMode.Scoped or ControlPlaneSecurityMode.RowSecurityOnly ? rowSecurity : null;

        if (securityMode == ControlPlaneSecurityMode.Open)
        {
            endpoints.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Corvus.Text.Json.Arazzo.Durability.ControlPlane")
                .LogWarning("Arazzo control plane mapped in OPEN security mode: no authentication and no row security — every operation is exposed to anonymous callers. Use this only for development or a trusted network.");
        }

        // Resolve the caller's AccessContext per request (§14.2/§14.4): when a row-security policy is configured the
        // handlers gate every read/write/purge by the principal's per-verb reach — the client operations require a
        // context, so an unscoped read cannot exist to be misused. With no policy (Open/ScopesOnly) the access binding
        // yields AccessContext.System throughout. The current principal is read via IHttpContextAccessor.
        ControlPlaneAccess access = effectivePolicy is null
            ? new ControlPlaneAccess()
            : new ControlPlaneAccess(endpoints.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), effectivePolicy);

        // The security-authoring API persists rules/bindings; if the deployment's policy is the persistent one,
        // refresh it after writes so authoring changes take effect for subsequent authorization decisions.
        ISecurityPolicyStore policyStore = securityPolicyStore ?? new InMemorySecurityPolicyStore();
        var securityHandler = new ArazzoControlPlaneSecurityHandler(policyStore, effectivePolicy as PersistentRowSecurityPolicy, access);

        // The source-credential management API persists references + metadata only — it never touches secret material.
        ISourceCredentialStore credentialStore = sourceCredentialStore ?? new InMemorySourceCredentialStore();
        var credentialsHandler = new ArazzoControlPlaneCredentialsHandler(credentialStore, access);

        // The identity layer (§16.5.4): the store-indexed observed-identity typeahead (an in-memory reference by default
        // so the endpoints function in development) plus an optional pluggable directory. The write paths below record
        // observed identities into it, so the grantee typeahead is self-populating.
        IObservedIdentityStore observedStore = observedIdentityStore ?? new InMemoryObservedIdentityStore();

        // The administration management API (§15) governs a base id's administrator set by current-administrator
        // membership; it delegates to the catalog client (which owns the administrator store, if one is configured) and
        // names administrators by deployment-mapped grants rather than raw internal tags.
        var administratorsHandler = new ArazzoControlPlaneAdministratorsHandler(catalog, access, observedStore);

        // The access-request API (§16.5): requests route to the target workflow's §15 administrators (or self-elevate
        // when eligible); an approval writes a single capped, time-boxed grant to the security-policy store (refreshed
        // in-process when the deployment's policy is the persistent one).
        IAccessRequestStore requestStore = accessRequestStore ?? new InMemoryAccessRequestStore();
        var approvalService = new AccessRequestApprovalService(requestStore, policyStore, catalog, options: accessRequestApprovalOptions, rowSecurity: effectivePolicy as PersistentRowSecurityPolicy);
        var accessRequestsHandler = new ArazzoControlPlaneAccessRequestsHandler(approvalService, requestStore, catalog, access, accessRequestSubjectClaimType, selfElevationEligibility);

        var identityHandler = new ArazzoControlPlaneIdentityHandler(observedStore, principalDirectory, access);

        // The environments management API (§7.7): governed, reach-scoped deployment environments and their administrators.
        // The data plane is reach-filtered (the environment store); governance is current-administrator-gated (the
        // administration service over the environment-administrator store), and creating an environment grants the creator
        // administration. Both default to an in-memory store so the endpoints function in development.
        IEnvironmentStore envStore = environmentStore ?? new InMemoryEnvironmentStore();
        IEnvironmentAdministratorStore envAdminStore = environmentAdministratorStore ?? new InMemoryEnvironmentAdministratorStore();
        var environmentAdministration = new SecuredEnvironmentAdministration(envAdminStore);
        var environmentsHandler = new ArazzoControlPlaneEnvironmentsHandler(envStore, environmentAdministration, access, observedStore);

        // The sources registry API (§7.6): first-class, reach-scoped source documents a workflow references by name. The
        // data plane is reach-filtered (the source store); sources are not governed (no administrator set) — reach
        // membership is the management gate. Defaults to an in-memory store so the endpoints function in development.
        ISourceStore srcStore = sourceStore ?? new InMemorySourceStore();
        var sourcesHandler = new ArazzoControlPlaneSourcesHandler(srcStore, access);

        return endpoints.MapApiEndpoints(
            new ArazzoControlPlaneHandler(management, access),
            new ArazzoControlPlaneRunnersHandler(runners),
            new ArazzoControlPlaneCatalogHandler(catalog, management, runners, access),
            securityHandler,
            credentialsHandler,
            environmentsHandler,
            sourcesHandler,
            administratorsHandler,
            accessRequestsHandler,
            identityHandler,
            gateScopes ? ControlPlaneAuthorization.RequireDeclaredScopes : null);
    }
}