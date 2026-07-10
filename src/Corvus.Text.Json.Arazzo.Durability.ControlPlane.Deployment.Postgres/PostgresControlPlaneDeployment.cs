// <copyright file="PostgresControlPlaneDeployment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;
using Corvus.Text.Json.Arazzo.Durability.Postgres;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.Postgres;

/// <summary>
/// Provisions an Arazzo durability control-plane deployment backed by Postgres: it creates the schema for every store
/// the control plane owns and runs the deployment-agnostic security bootstrap (the §14.2 rules, the read-all shell
/// binding, and the §16.2-tier-3 genesis-admin grant), driven by a <see cref="DeploymentBootstrapOptions"/> value.
/// </summary>
/// <remarks>
/// <para>
/// This type is coupled to Postgres because schema creation is inherently backend-specific — each backend's
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
public static class PostgresControlPlaneDeployment
{
    /// <summary>
    /// Provisions the deployment end to end: creates every control-plane store's schema (via
    /// <see cref="ProvisionSchemaAsync"/>), then seeds the security policy from <paramref name="options"/>.
    /// </summary>
    /// <param name="dataSource">The Npgsql data source for the shared control-plane database.</param>
    /// <param name="options">The deployment-agnostic bootstrap configuration (genesis admin, capability scopes, label
    /// orderings, identity claim, …).</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when the schema exists and the security policy is seeded.</returns>
    public static async ValueTask ProvisionAsync(NpgsqlDataSource dataSource, DeploymentBootstrapOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        await ProvisionSchemaAsync(dataSource, cancellationToken).ConfigureAwait(false);

        // The security store's schema is created above, so the policy can now be seeded. Connect a transient handle
        // purely to seed it; the host connects its own handle for request-time reads (the two are independent).
        await using PostgresSecurityPolicyStore securityStore = await PostgresSecurityPolicyStore.ConnectAsync(dataSource, cancellationToken: cancellationToken).ConfigureAwait(false);
        await new DefaultDeploymentBootstrap().BootstrapSecurityAsync(securityStore, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the schema for every store the Arazzo control plane owns (idempotent <c>CREATE TABLE IF NOT EXISTS</c>).
    /// Postgres adapters do not self-create schema on connect, and the runner never runs DDL, so a deployment runs this
    /// once, before any store opens.
    /// </summary>
    /// <param name="dataSource">The Npgsql data source for the shared control-plane database.</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when every store's schema exists.</returns>
    public static async ValueTask ProvisionSchemaAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        // Durable execution + catalog + runner registration.
        await PostgresWorkflowStateStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresWorkflowCatalogStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresRunnerRegistry.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresEnvironmentRunnerAuthorizationStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresSourceCredentialStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);

        // §18 debug runs: the working-copy draft-run store + its metadata trace store.
        await PostgresDraftRunStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresDraftRunTraceStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);

        // Environments + working copies + workflow administrators.
        await PostgresEnvironmentStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresWorkspaceWorkflowStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresWorkflowAdministratorStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);

        // Security policy + access requests (§16.5).
        await PostgresSecurityPolicyStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresAccessRequestStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);

        // Governance stores (§7.6-§7.8, §16.5.4): availability ("Available in"), promotion requests, the source
        // registry, per-environment administrators, and the observed-identity typeahead.
        await PostgresAvailabilityStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresAvailabilityRequestStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresSourceStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresEnvironmentAdministratorStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
        await PostgresObservedIdentityStore.PrepareAsync(dataSource, cancellationToken).ConfigureAwait(false);
    }
}