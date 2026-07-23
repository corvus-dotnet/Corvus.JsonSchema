// <copyright file="DefaultDeploymentBootstrap.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Corvus.Text.Json.Arazzo.Durability.Environments;
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

        // Bindings carry a store-assigned id (no natural key like a rule name), so this bootstrap must dedupe by the
        // binding SUBJECT (claim type + value) to stay idempotent: a deployment runs it on every startup, and appending
        // the same bootstrap bindings each time would accumulate duplicates. Load the current set once and add only the
        // subjects that are missing (mirrors how SecurityBootstrap.SeedAsync adds only missing rule names). If an
        // operator has since deleted a bootstrap binding, a re-run restores it; if they still hold it, the re-run is a
        // no-op.
        using PooledDocumentList<SecurityBindingDocument> existingBindings = await securityStore.ListBindingsAsync(cancellationToken).ConfigureAwait(false);
        bool BindingExists(string claimType, string? claimValue)
        {
            foreach (SecurityBindingDocument binding in existingBindings)
            {
                if (string.Equals(binding.ClaimTypeValue, claimType, StringComparison.Ordinal)
                    && string.Equals(binding.ClaimValueOrNull, claimValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // (2) The read-all shell binding: every authenticated principal may READ the whole control plane; WRITE/purge
        // reach is deny-by-default, conferred per-principal only through the access-request → approval flow (§16.5).
        if (!BindingExists("*", null))
        {
            using ParsedJsonDocument<SecurityBindingDocument> readAll = SecurityBindingDocument.Draft(
                "*", null, read: VerbGrant.Full, write: VerbGrant.None, purge: VerbGrant.None,
                description: "Authenticated principals may read the whole control plane.");
            (await securityStore.AddBindingAsync(readAll.RootElement, "bootstrap", cancellationToken).ConfigureAwait(false)).Dispose();
        }

        // (3) The genesis administrator (§16.2 tier 3): the configured group claim → all capability scopes plus
        // unrestricted read/write/purge reach. The first admin logs in via OIDC already holding admin (the identity
        // analogue of secret-zero). This stored binding IS the deliberate deployment policy — no group otherwise
        // confers capability by mere membership; everyone else earns reach through the §16.5 approval flow.
        string claimType = options.IdentityClaimType.IsNotUndefined() ? (string)options.IdentityClaimType : "groups";
        string genesisGroup = (string)options.GenesisAdminGroup;
        if (!BindingExists(claimType, genesisGroup))
        {
            using ParsedJsonDocument<SecurityBindingDocument> admin = SecurityBindingDocument.Draft(
                claimType, genesisGroup, read: VerbGrant.Full, write: VerbGrant.Full, purge: VerbGrant.Full,
                scopes: ReadScopes(options),
                description: $"Genesis administrator: the '{genesisGroup}' {claimType} value holds all capability scopes plus unrestricted reach — the deployment's founding grant.",
                additionalClauses: ReadAdditionalClauses(options));
            (await securityStore.AddBindingAsync(admin.RootElement, "bootstrap", cancellationToken).ConfigureAwait(false)).Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask BootstrapSystemWorkflowsAsync(
        IWorkflowCatalogStore catalogStore,
        IWorkflowWaitIndex runs,
        IWorkflowAdministratorStore administrators,
        ISourceCredentialStore credentials,
        IAvailabilityStore availability,
        IEnvironmentStore environments,
        IEnvironmentAdministratorStore environmentAdministrators,
        DeploymentBootstrapOptions options,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogStore);
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(environmentAdministrators);

        // Not opted in: the deployment runs approvals through the built-in direct-to-administrator strategy.
        if (!options.SystemWorkflows.IsNotUndefined())
        {
            return;
        }

        // var infers the generated nested option type without depending on its emitted name.
        var config = options.SystemWorkflows;

        // Compose the catalog the install needs: the credential store makes the catalog-time gate resolve the runner
        // credential, and the administrator store records the §15 administrator (established when v1 is added), so the
        // genesis administrator can later approve the requests the workflow governs.
        var catalog = new SecuredWorkflowCatalog(catalogStore, runs, "bootstrap", credentials, administrators);
        var installer = new SystemWorkflowInstaller(catalog, availability, credentials, environments, environmentAdministrators, executorProvider);
        await installer.InstallAsync(
            new SystemWorkflowInstallOptions
            {
                // The genesis administrator administers the system workflow, so any genesis-admin caller may approve the
                // access requests it governs (its resolved identity set-equals this stamped §15 administrator identity).
                AdministratorIdentity = BuildGenesisAdministratorIdentity(options),
                Owner = new CatalogOwner("Arazzo Control Plane", "control-plane@arazzo.system", "Platform", null),
                CredentialTokenUrl = (string)config.TokenUrl,
                CredentialClientId = config.ClientId.IsNotUndefined() ? (string)config.ClientId : "arazzo-access-approval",
                CredentialClientSecretRef = (string)config.ClientSecretRef,
                Environment = config.Environment.IsNotUndefined() ? (string)config.Environment : "system",
                WorkflowTags = ["system", "approval"],
                Actor = "bootstrap",
            },
            cancellationToken).ConfigureAwait(false);
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

    // The genesis grant's additional identity-dimension clauses (§16.5.4): each is ANDed with the primary group clause,
    // so the genesis grant applies only to a caller whose canonical identity contains every clause. Null when none are
    // configured (a single-clause group grant). Used to issuer-qualify the genesis administrator.
    private static IReadOnlyList<(string Dimension, string? Value)>? ReadAdditionalClauses(DeploymentBootstrapOptions options)
    {
        if (!options.GenesisAdditionalClauses.IsNotUndefined())
        {
            return null;
        }

        var clauses = new List<(string Dimension, string? Value)>();
        foreach (var clause in options.GenesisAdditionalClauses.EnumerateArray())
        {
            clauses.Add(((string)clause.DimensionValue, clause.Value.IsNotUndefined() ? (string)clause.Value : null));
        }

        return clauses.Count > 0 ? clauses : null;
    }

    // The genesis administrator's internal identity: the tag-set the live claims resolver produces for a genesis-admin
    // caller — <prefix>group=<genesisAdminGroup> plus each configured additional dimension clause that carries a value
    // (e.g. <prefix>iss=<idp>). This is the same derivation as the genesis grant's subject, so a genesis-admin caller
    // set-equals the system workflow's stamped §15 administrator and may approve the access requests it governs. A
    // valueless clause (present-with-any-value in a selector) cannot form part of a concrete identity, so it is skipped.
    private static SecurityTagSet BuildGenesisAdministratorIdentity(DeploymentBootstrapOptions options)
    {
        string prefix = options.InternalTagPrefix.IsNotUndefined() ? (string)options.InternalTagPrefix : SecurityShell.DefaultInternalPrefix;
        var tags = new List<SecurityTag> { new(prefix + "group", (string)options.GenesisAdminGroup) };
        if (options.GenesisAdditionalClauses.IsNotUndefined())
        {
            foreach (var clause in options.GenesisAdditionalClauses.EnumerateArray())
            {
                if (clause.Value.IsNotUndefined())
                {
                    tags.Add(new SecurityTag(prefix + (string)clause.DimensionValue, (string)clause.Value));
                }
            }
        }

        return SecurityTagSet.FromTags(tags);
    }
}