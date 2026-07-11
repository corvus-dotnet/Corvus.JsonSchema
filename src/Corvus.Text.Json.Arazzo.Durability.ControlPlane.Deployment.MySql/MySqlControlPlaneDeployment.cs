// <copyright file="MySqlControlPlaneDeployment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;
using Corvus.Text.Json.Arazzo.Durability.MySql;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.MySql;

/// <summary>
/// Provisions an Arazzo durability control-plane deployment backed by MySQL: it creates the schema for every store
/// the control plane owns and runs the deployment-agnostic security bootstrap (the §14.2 rules, the read-all shell
/// binding, and the §16.2-tier-3 genesis-admin grant), driven by a <see cref="DeploymentBootstrapOptions"/> value.
/// </summary>
/// <remarks>
/// <para>
/// This type is coupled to MySQL because schema creation is inherently backend-specific — each backend's
/// <c>PrepareAsync</c> has a different signature — but it is deliberately <em>identity-provider agnostic</em>: it never
/// references Keycloak or any OIDC / ASP.NET wiring. A deployment provisions its store and policy here, then wires
/// whatever identity provider it uses (Keycloak, Entra ID, Auth0, …) separately in its host composition.
/// </para>
/// <para>
/// A configurable deployment (for example a ZeroFailed pipeline) binds the <see cref="DeploymentBootstrapOptions"/>
/// from configuration — validated against its generated JSON Schema — and calls <see cref="ProvisionAsync"/> once at
/// deployment or startup time. Everything here is idempotent, so it is safe to run on every startup.
/// </para>
/// </remarks>
public static class MySqlControlPlaneDeployment
{
    /// <summary>
    /// Provisions the deployment end to end: creates every control-plane store's schema (via
    /// <see cref="ProvisionSchemaAsync"/>), then seeds the security policy from <paramref name="options"/>.
    /// </summary>
    /// <param name="connectionString">The MySQL connection string for the shared control-plane database.</param>
    /// <param name="options">The deployment-agnostic bootstrap configuration (genesis admin, capability scopes, label
    /// orderings, identity claim, …).</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when the schema exists and the security policy is seeded.</returns>
    public static async ValueTask ProvisionAsync(string connectionString, DeploymentBootstrapOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        await ProvisionSchemaAsync(connectionString, cancellationToken).ConfigureAwait(false);

        // The security store's schema is created above, so the policy can now be seeded. Connect a transient handle
        // purely to seed it; the host connects its own handle for request-time reads (the two are independent).
        await using MySqlSecurityPolicyStore securityStore = await MySqlSecurityPolicyStore.ConnectAsync(connectionString, cancellationToken: cancellationToken).ConfigureAwait(false);
        await new DefaultDeploymentBootstrap().BootstrapSecurityAsync(securityStore, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the schema for every store the Arazzo control plane owns (idempotent <c>CREATE TABLE IF NOT EXISTS</c>).
    /// A deployment runs this once, before any store opens.
    /// </summary>
    /// <param name="connectionString">The MySQL connection string for the shared control-plane database.</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when every store's schema exists.</returns>
    public static async ValueTask ProvisionSchemaAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        // Durable execution + catalog + runner registration.
        await MySqlWorkflowStateStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlWorkflowCatalogStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlRunnerRegistry.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlEnvironmentRunnerAuthorizationStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlSourceCredentialStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);

        // §18 debug runs: the working-copy draft-run store + its metadata trace store.
        await MySqlDraftRunStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlDraftRunTraceStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);

        // Environments + working copies + workflow administrators.
        await MySqlEnvironmentStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlWorkspaceWorkflowStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlWorkflowAdministratorStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);

        // Security policy + access requests (§16.5).
        await MySqlSecurityPolicyStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlAccessRequestStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);

        // Governance stores (§7.6-§7.8, §16.5.4): availability ("Available in"), promotion requests, the source
        // registry, per-environment administrators, and the observed-identity typeahead.
        await MySqlAvailabilityStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlAvailabilityRequestStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlSourceStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlEnvironmentAdministratorStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
        await MySqlObservedIdentityStore.PrepareAsync(connectionString, cancellationToken).ConfigureAwait(false);
    }
}