// <copyright file="IDeploymentBootstrap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;

/// <summary>
/// The real, deployment-agnostic bootstrap every Arazzo control-plane deployment runs before serving traffic: it
/// seeds the security / identity / policy the platform needs to function, driven entirely by
/// <see cref="DeploymentBootstrapOptions"/> so nothing is hard-coded to one deployment. Idempotent — safe to run on
/// every start / re-deploy.
/// </summary>
/// <remarks>
/// This is distinct from a sample's example seeding (an <c>IExampleSeed</c> that layers fictional demo content on
/// top): a production deployment runs <see cref="IDeploymentBootstrap"/> and never the example seed. Keeping the two
/// apart is what lets the real bootstrap become a configurable deployment step (e.g. a ZeroFailed task) that consumes
/// the JSON <see cref="DeploymentBootstrapOptions"/>, with the demo content strictly on the side.
/// </remarks>
public interface IDeploymentBootstrap
{
    /// <summary>
    /// Seeds the row-security policy store (§14.2 / §16.2): the editable bootstrap rules, the read-all shell binding
    /// (every authenticated principal may read; write/purge deny-by-default), and the genesis-administrator grant
    /// (<see cref="DeploymentBootstrapOptions.GenesisAdminGroup"/> → all <see cref="DeploymentBootstrapOptions.GenesisScopes"/>
    /// plus unrestricted reach). The rules are idempotent (only missing names are added).
    /// </summary>
    /// <param name="securityStore">The row-security policy store to seed.</param>
    /// <param name="options">The deployment configuration.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the security store is seeded.</returns>
    ValueTask BootstrapSecurityAsync(ISecurityPolicyStore securityStore, DeploymentBootstrapOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs the bootstrapped access-approval system workflow (design §16.5.1) when the deployment opts into it via
    /// <see cref="DeploymentBootstrapOptions.SystemWorkflows"/>: catalogues the workflow package, makes it available in
    /// the internal environment, and provisions the system runner's OAuth2 client-credentials identity — so Arazzo
    /// governs its own access approvals. A no-op when the option is absent (the built-in direct-to-administrator
    /// strategy is used). Idempotent — safe to run on every start.
    /// </summary>
    /// <remarks>
    /// The raw stores are taken (rather than a pre-built catalog) so the exact wiring the install depends on — the
    /// bootstrap actor, the credential store the catalog-time gate resolves through, and the administrator store that
    /// records the §15 administrator — lives here once, not replicated (and potentially got subtly wrong) across every
    /// deployment. The implementation composes them into the catalog it needs.
    /// </remarks>
    /// <param name="catalogStore">The catalog store the approval version is published to.</param>
    /// <param name="runs">The wait index the catalog resolves suspended runs through.</param>
    /// <param name="administrators">The administrator store the §15 administrator record is written to (so the genesis
    /// administrator can later approve the requests the workflow governs).</param>
    /// <param name="credentials">The credential store the runner's client-credentials identity is provisioned in (and the
    /// store the catalog-time credential gate resolves through).</param>
    /// <param name="availability">The availability store the version is made available in.</param>
    /// <param name="environments">The environment store the internal environment is created in.</param>
    /// <param name="environmentAdministrators">The environment-administrator store the internal environment's administration
    /// is established in (the genesis administrator), so it is governable like a normally-created environment (§7.7).</param>
    /// <param name="options">The deployment configuration (its <see cref="DeploymentBootstrapOptions.SystemWorkflows"/>
    /// carries the token endpoint, client id, and secret reference; the §15 administrator is the genesis administrator).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the workflow is catalogued and available, or immediately when not opted in.</returns>
    ValueTask BootstrapSystemWorkflowsAsync(
        IWorkflowCatalogStore catalogStore,
        IWorkflowWaitIndex runs,
        IWorkflowAdministratorStore administrators,
        ISourceCredentialStore credentials,
        IAvailabilityStore availability,
        IEnvironmentStore environments,
        IEnvironmentAdministratorStore environmentAdministrators,
        DeploymentBootstrapOptions options,
        IWorkflowExecutorProvider? executorProvider = null,
        Sources.ISourceStore? sources = null,
        CancellationToken cancellationToken = default);
}