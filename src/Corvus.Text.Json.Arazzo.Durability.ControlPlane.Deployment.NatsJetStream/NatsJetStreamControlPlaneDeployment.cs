// <copyright file="NatsJetStreamControlPlaneDeployment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;
using Corvus.Text.Json.Arazzo.Durability.NatsJetStream;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Deployment.NatsJetStream;

/// <summary>
/// Provisions an Arazzo durability control-plane deployment backed by NATS JetStream: it creates the schema for every store
/// the control plane owns and runs the deployment-agnostic security bootstrap (the §14.2 rules, the read-all shell
/// binding, and the §16.2-tier-3 genesis-admin grant), driven by a <see cref="DeploymentBootstrapOptions"/> value.
/// </summary>
/// <remarks>
/// <para>
/// This type is coupled to NATS JetStream because schema creation is inherently backend-specific — each backend's
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
public static class NatsJetStreamControlPlaneDeployment
{
    /// <summary>
    /// Provisions the deployment end to end: creates every control-plane store's schema (via
    /// <see cref="ProvisionSchemaAsync"/>), then seeds the security policy from <paramref name="options"/>.
    /// </summary>
    /// <param name="url">The NATS server URL for the shared control-plane streams.</param>
    /// <param name="options">The deployment-agnostic bootstrap configuration (genesis admin, capability scopes, label
    /// orderings, identity claim, …).</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when the schema exists and the security policy is seeded.</returns>
    public static async ValueTask ProvisionAsync(string url, DeploymentBootstrapOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        await ProvisionSchemaAsync(url, cancellationToken).ConfigureAwait(false);

        // The security store's schema is created above, so the policy can now be seeded. Connect a transient handle
        // purely to seed it; the host connects its own handle for request-time reads (the two are independent).
        await using NatsJetStreamSecurityPolicyStore securityStore = await NatsJetStreamSecurityPolicyStore.ConnectAsync(url, cancellationToken: cancellationToken).ConfigureAwait(false);
        await new DefaultDeploymentBootstrap().BootstrapSecurityAsync(securityStore, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the schema for every store the Arazzo control plane owns (idempotent, so it is safe to re-run).
    /// A deployment runs this once, before any store opens.
    /// </summary>
    /// <param name="url">The NATS server URL for the shared control-plane streams.</param>
    /// <param name="cancellationToken">Cancels the provisioning.</param>
    /// <returns>A task that completes when every store's schema exists.</returns>
    public static async ValueTask ProvisionSchemaAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);

        // Durable execution + catalog + runner registration.
        await NatsJetStreamWorkflowStateStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamWorkflowCatalogStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamRunnerRegistry.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamEnvironmentRunnerAuthorizationStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamSourceCredentialStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);

        // §18 debug runs: the working-copy draft-run store + its metadata trace store.
        await NatsJetStreamDraftRunStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamDraftRunTraceStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);

        // Environments + working copies + workflow administrators.
        await NatsJetStreamEnvironmentStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamWorkspaceWorkflowStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamWorkflowAdministratorStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);

        // Security policy + access requests (§16.5).
        await NatsJetStreamSecurityPolicyStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamAccessRequestStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);

        // Governance stores (§7.6-§7.8, §16.5.4): availability ("Available in"), promotion requests, the source
        // registry, per-environment administrators, and the observed-identity typeahead.
        await NatsJetStreamAvailabilityStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamAvailabilityRequestStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamSourceStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamEnvironmentAdministratorStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
        await NatsJetStreamObservedIdentityStore.PrepareAsync(url, cancellationToken).ConfigureAwait(false);
    }
}