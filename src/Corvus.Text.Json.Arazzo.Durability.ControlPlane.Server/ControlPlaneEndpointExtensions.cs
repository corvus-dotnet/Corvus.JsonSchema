// <copyright file="ControlPlaneEndpointExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
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
    /// <param name="capacityOptions">The deployment's standing capacity limits (ADR 0065 decision 3); defaults are used
    /// when omitted. The stored-run limit defaults to disabled, because nothing reclaims stored runs yet.</param>
    /// <param name="runnerEnrolmentSecret">The secret runner enrolment tokens are minted and validated with (ADR 0065
    /// decision 2). Leave it empty to accept none, which admits only runners an administrator has pre-authorized by id.</param>
    /// <param name="checkpointSecret">The secret the serverless checkpoint surface validates run-scoped checkpoint
    /// tokens with (ADR 0062), shared with whichever component mints them. Leave it empty to serve no checkpoint
    /// surface at all: its caller is a baked function holding no principal, so the token is the only thing that can say
    /// which run a callback may touch, and a surface mapped without one admits any caller the host authenticates to
    /// every run in the deployment. It must be at least <see cref="CheckpointToken.MinimumSecretBytes"/> bytes.</param>
    /// <param name="checkpoints">The host's checkpoint coordinator. A host that also maps the runner API must build one
    /// and pass it to both, because ADR 0065 decision 6 requires the per-run single-flight interlock to be per run
    /// rather than per component. When <see langword="null"/> a private one is built over
    /// <paramref name="workflowStateStore"/>.</param>
    /// <param name="selfElevationEligibility">
    /// An optional predicate deciding whether a requester is eligible to self-elevate a request (§16.5.3); when it
    /// returns <see langword="true"/> the request is auto-approved without a human approver. Default: never eligible.
    /// </param>
    /// <param name="ownerGroupClaimType">The claim type carrying the caller's owner group (default <c>tenant</c>), used
    /// to stamp ownership in a mode that authenticates but configures no <paramref name="rowSecurity"/> policy — a
    /// policy, where present, derives identity itself and this is not consulted. Pass <see langword="null"/> to stamp no
    /// ownership. A principal carrying no such claim also stamps none, which means the deployment cannot tell owner
    /// groups apart and therefore has one (ADR 0065).</param>
    /// <returns>The same endpoint route builder, for chaining.</returns>
    /// <remarks>
    /// Authentication is always the host's concern: the control plane depends only on a <c>ClaimsPrincipal</c>
    /// and the named scope policies, so a deployment supplies any ASP.NET Core scheme (JWT bearer, OIDC, mTLS,
    /// a dev key) and how a principal acquires scopes.
    /// </remarks>
    public static IEndpointRouteBuilder MapArazzoControlPlane(this IEndpointRouteBuilder endpoints, ISecuredWorkflowManagement management, ISecuredWorkflowCatalog catalog, IRunnerRegistry runners, ControlPlaneSecurityMode securityMode, ControlPlaneRowSecurityPolicy? rowSecurity = null, ISecurityPolicyStore? securityPolicyStore = null, ISourceCredentialStore? sourceCredentialStore = null, IAccessRequestStore? accessRequestStore = null, AccessRequestApprovalOptions? accessRequestApprovalOptions = null, string accessRequestSubjectClaimType = "sub", Func<ClaimsPrincipal, AccessRequest, bool>? selfElevationEligibility = null, IObservedIdentityStore? observedIdentityStore = null, IPrincipalDirectory? principalDirectory = null, IEnvironmentStore? environmentStore = null, IEnvironmentAdministratorStore? environmentAdministratorStore = null, ISourceStore? sourceStore = null, IWorkspaceWorkflowStore? workspaceWorkflowStore = null, IAvailabilityStore? availabilityStore = null, IAvailabilityRequestStore? availabilityRequestStore = null, IEnvironmentRunnerAuthorizationStore? environmentRunnerAuthorizationStore = null, SourceDocumentFetcher? sourceFetcher = null,
        Corvus.Text.Json.Arazzo.Testing.WorkflowSimulator? workflowSimulator = null,
        GitHubBroker? gitHubBroker = null,
        ProviderBroker? providerBroker = null,
        IWorkflowStateStore? workflowStateStore = null,
        IDraftRunStore? draftRunStore = null,
        InProcessDraftRunner? draftRunner = null,
        IDraftRunTraceStore? draftRunTraceStore = null,
        WorkflowApprovalOptions? workflowApproval = null,
        Action<IAccessRequestApprovalService>? onApprovalServiceBuilt = null,
        INativeBuildJobStore? nativeBuildJobStore = null,
        IWorkflowDeploymentStore? workflowDeploymentStore = null, ReadOnlyMemory<byte> runnerEnrolmentSecret = default,
        string? ownerGroupClaimType = "tenant",
        Capacity.ControlPlaneCapacityOptions? capacityOptions = null,
        ReadOnlyMemory<byte> checkpointSecret = default,
        WorkflowCheckpointCoordinator? checkpoints = null)
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
            ServerThrowHelper.ThrowRowSecurityPolicyRequired(securityMode);
        }

        if ((securityMode is ControlPlaneSecurityMode.Open or ControlPlaneSecurityMode.ScopesOnly) && rowSecurity is not null)
        {
            ServerThrowHelper.ThrowRowSecurityPolicyForbidden(securityMode);
        }

        // A weak checkpoint secret is caught where it is configured rather than on the first callback that presents a
        // token signed with it, which would be a silent downgrade of the one credential that surface has.
        if (!checkpointSecret.IsEmpty && checkpointSecret.Length < CheckpointToken.MinimumSecretBytes)
        {
            ServerThrowHelper.ThrowCheckpointSecretTooShort(nameof(checkpointSecret));
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
        // Open authenticates nobody, so there is no principal to read and no accessor to require. Every other mode
        // authenticates, so the accessor is a hard requirement there: without it a handler cannot name the actor it
        // records, and ownership cannot be determined at all. Missing registration fails construction here rather
        // than degrading silently at the first request, matching how ADR 0016 validates the mode/policy pairing.
        // Ownership is stamped independently of reach (ADR 0065): where there is no policy to derive an identity from,
        // the owner-group claim supplies one, so a mode that authenticates can still say which owner group created a
        // row. Where there IS a policy, it owns the answer and the claim is not consulted.
        ControlPlaneAccess access = securityMode == ControlPlaneSecurityMode.Open
            ? new ControlPlaneAccess()
            : effectivePolicy is null
                ? new ControlPlaneAccess(endpoints.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), ownerGroupClaimType)
                : new ControlPlaneAccess(endpoints.ServiceProvider.GetRequiredService<IHttpContextAccessor>(), effectivePolicy);

        // The governance/read-access audit (§850/§860) logs under a dedicated "Corvus.Arazzo.Audit" category so a
        // deployment can route/retain it independently; the audit spans ride the always-registered Corvus.Arazzo
        // ActivitySource regardless. Shared by the journal-read audit (§860) and the governance-mutation audit (§850).
        ILogger? auditLogger = endpoints.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("Corvus.Arazzo.Audit");

        // The security-authoring API persists rules/bindings; if the deployment's policy is the persistent one,
        // refresh it after writes so authoring changes take effect for subsequent authorization decisions.
        ISecurityPolicyStore policyStore = securityPolicyStore ?? new InMemorySecurityPolicyStore();

        // The sources registry store is resolved ahead of its handler because the credentials handler classifies each
        // binding's source from it (an AsyncAPI source takes the channel-credential rules, ADR 0051).
        ISourceStore srcStore = sourceStore ?? new InMemorySourceStore();

        // The source-credential management API persists references + metadata only — it never touches secret material.
        ISourceCredentialStore credentialStore = sourceCredentialStore ?? new InMemorySourceCredentialStore();
        var credentialsHandler = new ArazzoControlPlaneCredentialsHandler(credentialStore, access, auditLogger: auditLogger, sources: srcStore);

        // The environment administration service (§7.7) is shared by the environments/availability handlers below and
        // by the access-overview aggregation (administered environments), so it is constructed ahead of both.
        IEnvironmentAdministratorStore envAdminStore = environmentAdministratorStore ?? new InMemoryEnvironmentAdministratorStore();
        var environmentAdministration = new SecuredEnvironmentAdministration(envAdminStore);

        // The environment + availability stores are constructed here (ahead of their own handlers below) so the
        // access-overview aggregation can enrich a grantee's administered-environment rows with each environment's
        // summary and a bounded count of the versions available in it (§849), without a per-row fetch. Both default to
        // an in-memory store so the endpoints function in development.
        IEnvironmentStore envStore = environmentStore ?? new InMemoryEnvironmentStore();
        IAvailabilityStore availStore = availabilityStore ?? new InMemoryAvailabilityStore();

        // The access-overview (GET /access/grants) aggregates a grantee's bindings + conferred capability scopes +
        // administered workflows and environments + usable credentials, so the security handler also reads the catalog
        // (administered workflows + their representative version, §849), the credential store, the environment
        // administration reverse index, and the environment + availability stores (administered-environment enrichment).
        var securityHandler = new ArazzoControlPlaneSecurityHandler(policyStore, effectivePolicy as PersistentRowSecurityPolicy, access, catalog, credentialStore, environmentAdministration, auditLogger: auditLogger, environmentStore: envStore, availabilityStore: availStore);

        // The identity layer (§16.5.4): the store-indexed observed-identity typeahead (an in-memory reference by default
        // so the endpoints function in development) plus an optional pluggable directory. The write paths below record
        // observed identities into it, so the grantee typeahead is self-populating.
        IObservedIdentityStore observedStore = observedIdentityStore ?? new InMemoryObservedIdentityStore();

        // The administration management API (§15) governs a base id's administrator set by current-administrator
        // membership; it delegates to the catalog client (which owns the administrator store, if one is configured) and
        // names administrators by deployment-mapped grants rather than raw internal tags.
        var administratorsHandler = new ArazzoControlPlaneAdministratorsHandler(catalog, access, observedStore, auditLogger);

        // The access-request API (§16.5): requests route to the target workflow's §15 administrators (or self-elevate
        // when eligible); an approval writes a single capped, time-boxed grant to the security-policy store (refreshed
        // in-process when the deployment's policy is the persistent one).
        IAccessRequestStore requestStore = accessRequestStore ?? new InMemoryAccessRequestStore();
        var builtInApproval = new AccessRequestApprovalService(requestStore, policyStore, catalog, options: accessRequestApprovalOptions, rowSecurity: effectivePolicy as PersistentRowSecurityPolicy, selfElevationEligibility: selfElevationEligibility);

        // Design §16.5.1: when the deployment opts into the workflow-backed strategy, submitting a request starts the
        // bootstrapped access-approval workflow and the approve/reject/withdraw touchpoints publish the decision onto the
        // access.decision channel; otherwise the built-in routes decisions directly to §15 administrators. Either way the
        // handler depends only on the IAccessRequestApprovalService seam.
        IAccessRequestApprovalService approvalService = workflowApproval is null
            ? builtInApproval
            : new WorkflowBackedAccessRequestApprovalService(builtInApproval, requestStore, management, catalog, new PublishAccessDecisionProducer(workflowApproval.DecisionTransport), workflowApproval.ApprovalWorkflowId, workflowApproval.Environment, auditLogger);

        // Hand the composed strategy back to the host (e.g. so a demo can seed a pending request through the SAME
        // submission path a real caller uses — starting the approval run — rather than writing a request straight to the
        // store with no run to enact it, §16.5.1). The handler still depends only on the IAccessRequestApprovalService seam.
        onApprovalServiceBuilt?.Invoke(approvalService);
        var accessRequestsHandler = new ArazzoControlPlaneAccessRequestsHandler(approvalService, requestStore, catalog, access, accessRequestSubjectClaimType, auditLogger);

        var identityHandler = new ArazzoControlPlaneIdentityHandler(observedStore, principalDirectory, access);

        // The environments management API (§7.7): governed, reach-scoped deployment environments and their administrators.
        // The data plane is reach-filtered (the environment store, hoisted above); governance is current-administrator-gated
        // (the administration service over the environment-administrator store), and creating an environment grants the
        // creator administration.
        // The runner-authorization store (§5.5), resolved once and shared: the runner-authorizations handler owns its
        // lifecycle, and the environments handler consults it (with the runner registry) to fence an isolation-floor raise
        // (ADR 0058) — refusing to raise an environment's requiredIsolation while an under-isolated runner stays authorized.
        IEnvironmentRunnerAuthorizationStore runnerAuthStore = environmentRunnerAuthorizationStore ?? new InMemoryEnvironmentRunnerAuthorizationStore();
        var environmentsHandler = new ArazzoControlPlaneEnvironmentsHandler(envStore, environmentAdministration, access, observedStore, auditLogger: auditLogger, runners: runners, runnerAuthorizations: runnerAuthStore, securityMode: securityMode);

        // The kit surfaces that key token custody by principal read the authenticated principal through the
        // accessor in the modes whose access binding carries none (ScopesOnly).
        IHttpContextAccessor? httpContextAccessor = endpoints.ServiceProvider.GetService<IHttpContextAccessor>();

        // The sources registry API (§7.6): first-class, reach-scoped source documents a workflow references by name. The
        // data plane is reach-filtered (the source store); sources are not governed (no administrator set) — reach
        // membership is the management gate. The store itself is resolved above (the credentials handler reads it).
        // The fetch's provider auth mode (ADR 0052) reads the caller's connected-provider token from the broker.
        var sourcesHandler = new ArazzoControlPlaneSourcesHandler(
            srcStore, access, sourceFetcher, auditLogger: auditLogger,
            providers: providerBroker, httpContext: httpContextAccessor, subjectClaimType: accessRequestSubjectClaimType);
        IWorkspaceWorkflowStore wcStore = workspaceWorkflowStore ?? new InMemoryWorkspaceWorkflowStore();

        // §18 debug runs on the durable host (workflow-designer design §18 slice 3e-2c): a debug run IS a durable
        // $draft run the in-process runner executes. When the run store, the draft store, and the in-process runner
        // are all wired, build the capture front end's peer — a run-management client constructed with the SAME
        // recording+tracing resumer the runner exposes (InProcessDraftRunner.Resumer), so its native faulted-run
        // resume verbs (retry/skip/rewind/state-patch) are mark-claimable (§18 R5b), so the management client needs no
        // resumer — a runner performs the re-execution. Absent a run store or a runner the debug-run endpoints fail
        // closed; debug runs REQUIRE a runner to advance the runs the control plane marks claimable.
        ISecuredWorkflowManagement? debugRunManagement = workflowStateStore is not null && draftRunner is not null
            ? new SecuredWorkflowManagement(workflowStateStore, "arazzo-debug-runs")
            : null;
        var workspaceHandler = new ArazzoControlPlaneWorkspaceHandler(
            wcStore, access, catalog, srcStore, simulator: workflowSimulator, environments: envStore, credentials: credentialStore,
            workflowStateStore: workflowStateStore, draftRunStore: draftRunStore, debugRunManagement: debugRunManagement, draftRunner: draftRunner, draftRunTraceStore: draftRunTraceStore, auditLogger: auditLogger);

        // The native-build job store (ADR 0055), hoisted so it feeds both the availability handler's deploy-on-publish
        // (first-promoting a version into an Isolated environment queues its serverless build) and the native-builds API
        // below. Defaults to an in-memory store.
        INativeBuildJobStore buildStore = nativeBuildJobStore ?? new InMemoryNativeBuildJobStore();

        // The workflow-deployment store (ADR 0055, ADR 0059): the state of deploying a version's signed native binary to a
        // serverless function per (environment, runtime target). The catalog handler's dispatch-ready gate reads it to hold
        // an Isolated run until its function is Deployed; the runner's deploy worker drives it Queued -> Deployed | Failed.
        // Defaults to an in-memory store.
        IWorkflowDeploymentStore deploymentStore = workflowDeploymentStore ?? new InMemoryWorkflowDeploymentStore();

        // The availability ("promotion") API (§7.8): the additive (workflow version × environment) matrix. Making a
        // version available is governed by the TARGET environment's administrators and readiness-gated (every source the
        // version references must resolve a credential in that environment, §7.7). The store is hoisted above.
        var availabilityHandler = new ArazzoControlPlaneAvailabilityHandler(availStore, envStore, environmentAdministration, catalog, credentialStore, access, auditLogger: auditLogger, builds: buildStore);

        // The availability-request ("promotion request") API (§7.8): a principal who cannot make a version available
        // directly raises a request; the TARGET environment's administrators approve (readiness-gated, mirroring the direct
        // make) or deny, and the requester may withdraw their own. The approver inbox spans the environments the caller
        // administers (the reverse administration index). Defaults to an in-memory store.
        IAvailabilityRequestStore availRequestStore = availabilityRequestStore ?? new InMemoryAvailabilityRequestStore();
        var availabilityRequestsHandler = new ArazzoControlPlaneAvailabilityRequestsHandler(availRequestStore, availStore, envStore, environmentAdministration, catalog, credentialStore, access, accessRequestSubjectClaimType, auditLogger);

        // The native-builds API (ADR 0055): enqueue and poll the asynchronous Native-AOT builds of a version's serverless
        // binary per (environment, runtime target). Each operation is reach-gated to the workflow version, and a background
        // build worker drives each job Queued -> Building -> Ready | Failed. Uses the store hoisted above.
        var nativeBuildsHandler = new ArazzoControlPlaneNativeBuildsHandler(buildStore, catalog, access, auditLogger: auditLogger);

        // The deployments API (ADR 0055): read-only observation of a version's serverless deployments per (environment,
        // runtime target) — their lifecycle state and resulting function invoke URL. The deploy itself runs on the runner
        // (ADR 0059); the control plane only records and reports the state a background deploy worker drives. Reach-gated to
        // the version (catalog:read). Uses the deployment store hoisted above (shared with the catalog dispatch-ready gate).
        var deploymentsHandler = new ArazzoControlPlaneDeploymentsHandler(deploymentStore, catalog, access);

        // The runner-authorization API (§5.5): which runners may serve an environment. A runner enters Pending on
        // registration and is dispatchable only once an administrator of the TARGET environment authorizes it (revocable).
        // Governed by the environment's administrators; the approver inbox spans the environments the caller administers.
        // The store itself was resolved above (shared with the environments isolation-raise fence).

        // The standing capacity limits (ADR 0065 decision 3). Measured against the store, because a magnitude cannot be
        // measured anywhere else. The limits themselves decide what is enforced: an unset one enforces nothing, and the
        // stored-run limit is unset by default because nothing reclaims stored runs yet.
        var capacityGuard = new Capacity.StoreControlPlaneCapacityGuard(management, runnerAuthStore, capacityOptions);

        // The revocation fence (§5.5): if the workflow state store can administer leases, revoke expires a compromised runner's
        // leases so an authorized peer reclaims its in-flight runs at once. A store without the capability still stops all
        // future dispatch on revoke; only the immediate in-flight fence is unavailable.
        var runnerAuthorizationsHandler = new ArazzoControlPlaneRunnerAuthorizationsHandler(runnerAuthStore, envStore, runners, environmentAdministration, access, workflowStateStore as IWorkflowLeaseAdministration, accessRequestSubjectClaimType, runnerEnrolmentSecret, capacityGuard, auditLogger);
        var environmentKeysHandler = new ArazzoControlPlaneEnvironmentKeysHandler(envStore, environmentAdministration, access, auditLogger: auditLogger);

        // The brokered GitHub API (workflow-designer design §4.7): user-to-server sign-in, session
        // status, and proxied contents reads. Deployment-configured; fails closed when no broker is
        // wired. Token custody keys by the same subject claim the request surfaces use.
        var gitHubHandler = new ArazzoControlPlaneGitHubHandler(
            gitHubBroker, access, httpContextAccessor, accessRequestSubjectClaimType,
            workspaceStore: wcStore, sources: srcStore);

        // The connected-providers API (ADR 0052): the registry with the caller's connection state,
        // the brokered per-provider sign-in, and disconnect. Deployment-configured; an absent
        // broker lists an empty registry and refuses the auth operations.
        var providersHandler = new ArazzoControlPlaneProvidersHandler(providerBroker, access, httpContextAccessor, accessRequestSubjectClaimType);

        var schedulesHandler = new ArazzoControlPlaneSchedulesHandler(management, catalog, runners, access, availabilityStore, environmentStore, auditLogger: auditLogger);

        endpoints.MapApiEndpoints(
            securityHandler,
            new ArazzoControlPlaneHandler(management, access, catalog, auditLogger),
            new ArazzoControlPlaneRunnersHandler(runners, access),
            new ArazzoControlPlaneCatalogHandler(catalog, management, runners, access, environmentStore, availabilityStore, workflowSimulator, auditLogger, deploymentStore, capacityGuard),
            availabilityHandler,
            nativeBuildsHandler,
            deploymentsHandler,
            credentialsHandler,
            workspaceHandler,
            gitHubHandler,
            workspaceHandler, // debugRunsHandler — the same instance implements IApiDebugRunsHandler (§18 debug runs).
            sourcesHandler,
            environmentsHandler,
            runnerAuthorizationsHandler,
            environmentKeysHandler,
            schedulesHandler,
            administratorsHandler,
            providersHandler,
            accessRequestsHandler,
            availabilityRequestsHandler,
            identityHandler,
            gateScopes ? ControlPlaneAuthorization.RequireDeclaredScopes : null);

        // The serverless checkpoint surface (ADR 0055): a baked, Native-AOT function advances a run out of process and
        // loads/saves its checkpoint here rather than binding a store SDK. It is not part of the generated OpenAPI
        // surface (raw octet-stream bytes + an ETag + a write-sequence header, not a JSON model), so it is hand-mapped.
        //
        // It is present only when a checkpoint secret is configured, because the token IS this surface's reach gate
        // (ADR 0062). Its caller is a machine acting for one run and holding no principal of its own, so there is no
        // identity to derive reach from; what bounds it is a run-scoped token the dispatcher minted. Mapping it without
        // one leaves the ambient authorization as the only gate, and that admits any authenticated caller to every run
        // in the deployment — and admits everyone in Open. A deployment that dispatches no serverless runs configures
        // no secret and serves no such surface, which is the ADR 0016 posture: absent rather than open.
        if (workflowStateStore is not null && !checkpointSecret.IsEmpty)
        {
            endpoints.MapWorkflowCheckpointEndpoints(
                workflowStateStore,
                requireAuthorization: securityMode != ControlPlaneSecurityMode.Open,
                authenticateCheckpointToken: (id, token) => CheckpointToken.TryValidate(checkpointSecret.Span, token, id.Value, DateTimeOffset.UtcNow),
                checkpoints: checkpoints);
        }

        return endpoints;
    }
}