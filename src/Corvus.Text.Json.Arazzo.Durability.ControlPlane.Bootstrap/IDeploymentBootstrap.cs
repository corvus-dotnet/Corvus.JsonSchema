// <copyright file="IDeploymentBootstrap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
}