// <copyright file="DefaultDeploymentBootstrap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;

/// <summary>
/// The default <see cref="IDeploymentBootstrap"/>: seeds the security / identity policy a control-plane deployment
/// needs, purely from <see cref="DeploymentBootstrapOptions"/>. Nothing here is specific to a deployment — every
/// deployment-specific value (the genesis-administrator group, its scopes, the label taxonomy, …) comes from config.
/// </summary>
public sealed class DefaultDeploymentBootstrap : IDeploymentBootstrap
{
    /// <inheritdoc/>
    public async ValueTask BootstrapSecurityAsync(ISecurityPolicyStore securityStore, DeploymentBootstrapOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(securityStore);

        // (1) The editable bootstrap rules (§14.2) — idempotent (only missing rule names are added).
        await SecurityBootstrap.SeedAsync(securityStore, "bootstrap", cancellationToken).ConfigureAwait(false);

        // (2) The read-all shell binding: every authenticated principal may READ the whole control plane; WRITE/purge
        // reach is deny-by-default, conferred per-principal only through the access-request → approval flow (§16.5).
        using (ParsedJsonDocument<SecurityBindingDocument> readAll = SecurityBindingDocument.Draft(
            "*", null, read: VerbGrant.Full, write: VerbGrant.None, purge: VerbGrant.None,
            description: "Authenticated principals may read the whole control plane."))
        {
            (await securityStore.AddBindingAsync(readAll.RootElement, "bootstrap", cancellationToken).ConfigureAwait(false)).Dispose();
        }

        // (3) The genesis administrator (§16.2 tier 3): the configured group claim → all capability scopes plus
        // unrestricted read/write/purge reach. The first admin logs in via OIDC already holding admin (the identity
        // analogue of secret-zero). This stored binding IS the deliberate deployment policy — no group otherwise
        // confers capability by mere membership; everyone else earns reach through the §16.5 approval flow.
        string claimType = options.IdentityClaimType.IsNotUndefined() ? (string)options.IdentityClaimType : "groups";
        string genesisGroup = (string)options.GenesisAdminGroup;
        using (ParsedJsonDocument<SecurityBindingDocument> admin = SecurityBindingDocument.Draft(
            claimType, genesisGroup, read: VerbGrant.Full, write: VerbGrant.Full, purge: VerbGrant.Full,
            scopes: ReadScopes(options),
            description: $"Genesis administrator (§16.2 tier 3): the '{genesisGroup}' {claimType} value holds all capability scopes plus unrestricted reach."))
        {
            (await securityStore.AddBindingAsync(admin.RootElement, "bootstrap", cancellationToken).ConfigureAwait(false)).Dispose();
        }
    }

    /// <summary>Builds the ordered tag dimensions (§14.2) from configuration — a map of dimension name to its labels
    /// in ascending order. Surfaced read-only at <c>GET /security/orderings</c> and baked into the ordered rule
    /// templates. Used at composition time when constructing the row-security policy.</summary>
    /// <param name="options">The deployment configuration.</param>
    /// <returns>The label orderings.</returns>
    public static SecurityLabelOrderings BuildLabelOrderings(DeploymentBootstrapOptions options)
    {
        var orderings = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        if (options.LabelOrderings.IsNotUndefined())
        {
            foreach (var dimension in options.LabelOrderings.EnumerateObject())
            {
                var labels = new List<string>();
                foreach (var label in dimension.Value.EnumerateArray())
                {
                    labels.Add((string)label);
                }

                orderings[dimension.Name] = labels;
            }
        }

        return new SecurityLabelOrderings(orderings);
    }

    private static IReadOnlyList<string> ReadScopes(DeploymentBootstrapOptions options)
    {
        if (!options.GenesisScopes.IsNotUndefined())
        {
            return [];
        }

        var scopes = new List<string>();
        foreach (var scope in options.GenesisScopes.EnumerateArray())
        {
            scopes.Add((string)scope);
        }

        return scopes;
    }
}