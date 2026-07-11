// <copyright file="MongoControlPlaneDeployment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;
using Corvus.Text.Json.Arazzo.Durability.Mongo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.Mongo;

/// <summary>
/// Provisions an Arazzo durability control-plane deployment backed by MongoDB: it creates the schema for every store
/// the control plane owns and runs the deployment-agnostic security bootstrap (the §14.2 rules, the read-all shell
/// binding, and the §16.2-tier-3 genesis-admin grant), driven by a <see cref="DeploymentBootstrapOptions"/> value.
/// </summary>
/// <remarks>
/// <para>
/// This type is coupled to MongoDB because schema creation is inherently backend-specific — each backend's
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
public static class MongoControlPlaneDeployment
{
    /// <summary>
    /// Provisions the deployment end to end: creates every control-plane store's schema (via
    /// <see cref="ProvisionSchemaAsync"/>), then seeds the security policy from <paramref name="options"/>.
    /// </summary>
    /// <param name="connectionString">The MongoDB connection string for the shared control-plane database.</param>
    /// <param name="options">The deployment-agnostic bootstrap configuration (genesis admin, capability scopes, label
    /// orderings, identity claim, …).</param>
    /// <param name="databaseName">The MongoDB database name; defaults to "arazzo".</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when the schema exists and the security policy is seeded.</returns>
    public static async ValueTask ProvisionAsync(string connectionString, DeploymentBootstrapOptions options, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        await ProvisionSchemaAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);

        // The security store's schema is created above, so the policy can now be seeded. Connect a transient handle
        // purely to seed it; the host connects its own handle for request-time reads (the two are independent).
        await using MongoSecurityPolicyStore securityStore = await MongoSecurityPolicyStore.ConnectAsync(connectionString, databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await new DefaultDeploymentBootstrap().BootstrapSecurityAsync(securityStore, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the schema for the control-plane stores that need explicit index/collection setup. MongoDB creates
    /// collections lazily on first write, so the remaining stores auto-create and are not prepared here. A deployment
    /// runs this once, before any store opens.
    /// </summary>
    /// <param name="connectionString">The MongoDB connection string for the shared control-plane database.</param>
    /// <param name="databaseName">The MongoDB database name; defaults to "arazzo".</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when the prepared stores' schema exists.</returns>
    public static async ValueTask ProvisionSchemaAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        // MongoDB creates collections lazily on first write, so only the stores that need explicit index setup are
        // prepared here; the rest (RunnerRegistry, EnvironmentRunnerAuthorizationStore, DraftRunStore,
        // DraftRunTraceStore, WorkspaceWorkflowStore, SecurityPolicyStore, AccessRequestStore,
        // AvailabilityRequestStore) auto-create.
        await MongoWorkflowStateStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoWorkflowCatalogStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoSourceCredentialStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoEnvironmentStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoWorkflowAdministratorStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoAvailabilityStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoSourceStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoEnvironmentAdministratorStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
        await MongoObservedIdentityStore.PrepareAsync(connectionString, databaseName, cancellationToken).ConfigureAwait(false);
    }
}