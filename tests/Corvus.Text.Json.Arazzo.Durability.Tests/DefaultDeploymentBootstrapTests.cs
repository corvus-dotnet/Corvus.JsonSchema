// <copyright file="DefaultDeploymentBootstrapTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Bootstrap;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of <see cref="DefaultDeploymentBootstrap"/>: the deployment-agnostic security bootstrap must be idempotent,
/// because a deployment runs it on every startup — appending the same bootstrap bindings each time would accumulate
/// duplicates.
/// </summary>
[TestClass]
public sealed class DefaultDeploymentBootstrapTests
{
    private const string OptionsJson =
        """{"genesisAdminGroup":"arazzo-admins","genesisScopes":["catalog:read","catalog:write"],"identityClaimType":"groups"}""";

    [TestMethod]
    public async Task Bootstrap_seeds_the_shell_and_genesis_bindings_once()
    {
        var store = new InMemorySecurityPolicyStore();
        using ParsedJsonDocument<DeploymentBootstrapOptions> optionsDoc = ParsedJsonDocument<DeploymentBootstrapOptions>.Parse(OptionsJson);
        DeploymentBootstrapOptions options = optionsDoc.RootElement;
        var bootstrap = new DefaultDeploymentBootstrap();

        await bootstrap.BootstrapSecurityAsync(store, options);

        using PooledDocumentList<SecurityBindingDocument> bindings = await store.ListBindingsAsync(default);
        bindings.Count.ShouldBe(2); // the read-all shell binding + the genesis-admin binding

        // The two bindings are distinguished by their subject (claim type + value).
        bindings.Select(b => (b.ClaimTypeValue, b.ClaimValueOrNull))
            .ShouldBe([("*", (string?)null), ("groups", "arazzo-admins")], ignoreOrder: true);
    }

    [TestMethod]
    public async Task Bootstrap_issuer_qualifies_the_genesis_grant_with_additional_clauses()
    {
        var store = new InMemorySecurityPolicyStore();
        const string optionsJson =
            """{"genesisAdminGroup":"arazzo-admins","identityClaimType":"group","genesisAdditionalClauses":[{"dimension":"iss","value":"arazzo-keycloak"}]}""";
        using ParsedJsonDocument<DeploymentBootstrapOptions> optionsDoc = ParsedJsonDocument<DeploymentBootstrapOptions>.Parse(optionsJson);
        var bootstrap = new DefaultDeploymentBootstrap();

        await bootstrap.BootstrapSecurityAsync(store, optionsDoc.RootElement);

        using PooledDocumentList<SecurityBindingDocument> bindings = await store.ListBindingsAsync(default);
        SecurityBindingDocument genesis = bindings.Single(b => b.ClaimValueOrNull == "arazzo-admins");

        // The genesis grant is a tag-set selector (§16.5.4): the primary group clause AND the issuer clause, so it applies
        // only to an arazzo-admins group asserted by THIS deployment's issuer — not a same-named group from another IdP.
        genesis.ClaimTypeValue.ShouldBe("group");
        var clauses = new List<(string Dimension, string? Value)>();
        foreach (SecurityBindingDocument.AdditionalClause clause in genesis.AdditionalClauses.EnumerateArray())
        {
            clauses.Add(((string)clause.DimensionValue, clause.Value.IsNotUndefined() ? (string)clause.Value : null));
        }

        clauses.ShouldBe([("iss", (string?)"arazzo-keycloak")]);
    }

    [TestMethod]
    public async Task Bootstrap_is_idempotent_across_repeated_runs()
    {
        var store = new InMemorySecurityPolicyStore();
        using ParsedJsonDocument<DeploymentBootstrapOptions> optionsDoc = ParsedJsonDocument<DeploymentBootstrapOptions>.Parse(OptionsJson);
        DeploymentBootstrapOptions options = optionsDoc.RootElement;
        var bootstrap = new DefaultDeploymentBootstrap();

        // Three runs, as a deployment would across three restarts.
        await bootstrap.BootstrapSecurityAsync(store, options);
        await bootstrap.BootstrapSecurityAsync(store, options);
        await bootstrap.BootstrapSecurityAsync(store, options);

        // Bindings must NOT accumulate (the regression: they used to grow by two on every run).
        using (PooledDocumentList<SecurityBindingDocument> bindings = await store.ListBindingsAsync(default))
        {
            bindings.Count.ShouldBe(2);
        }

        // Rules are seeded idempotently too (only missing names added).
        using (PooledDocumentList<SecurityRuleDocument> rules = await store.ListRulesAsync(default))
        {
            rules.Count.ShouldBe(3);
        }
    }

    [TestMethod]
    public async Task Bootstrap_restores_a_deleted_genesis_binding_on_the_next_run()
    {
        var store = new InMemorySecurityPolicyStore();
        using ParsedJsonDocument<DeploymentBootstrapOptions> optionsDoc = ParsedJsonDocument<DeploymentBootstrapOptions>.Parse(OptionsJson);
        DeploymentBootstrapOptions options = optionsDoc.RootElement;
        var bootstrap = new DefaultDeploymentBootstrap();
        await bootstrap.BootstrapSecurityAsync(store, options);

        // An operator deletes the genesis-admin binding.
        string genesisId;
        using (PooledDocumentList<SecurityBindingDocument> bindings = await store.ListBindingsAsync(default))
        {
            genesisId = bindings.Single(b => b.ClaimValueOrNull == "arazzo-admins").IdValue;
        }

        (await store.DeleteBindingAsync(genesisId, WorkflowEtag.None, default)).ShouldBeTrue();

        // The next run restores it (subject missing again), and still does not duplicate the read-all shell.
        await bootstrap.BootstrapSecurityAsync(store, options);
        using (PooledDocumentList<SecurityBindingDocument> bindings = await store.ListBindingsAsync(default))
        {
            bindings.Count.ShouldBe(2);
            bindings.ShouldContain(b => b.ClaimValueOrNull == "arazzo-admins");
        }
    }

    [TestMethod]
    public async Task System_workflow_install_catalogues_makes_available_and_establishes_the_genesis_administrator()
    {
        const string optionsJson =
            """{"genesisAdminGroup":"arazzo-admins","identityClaimType":"group","genesisAdditionalClauses":[{"dimension":"iss","value":"arazzo-keycloak"}],"systemWorkflows":{"tokenUrl":"https://keycloak/realms/arazzo/protocol/openid-connect/token","clientSecretRef":"vault://secret/arazzo/controlplane#client-secret"}}""";
        using ParsedJsonDocument<DeploymentBootstrapOptions> optionsDoc = ParsedJsonDocument<DeploymentBootstrapOptions>.Parse(optionsJson);

        var catalogStore = new InMemoryWorkflowCatalogStore();
        var stateStore = new InMemoryWorkflowStateStore();
        var administrators = new InMemoryWorkflowAdministratorStore();
        var credentials = new InMemorySourceCredentialStore();
        var availability = new InMemoryAvailabilityStore();
        var environments = new InMemoryEnvironmentStore();

        await new DefaultDeploymentBootstrap().BootstrapSystemWorkflowsAsync(
            catalogStore, stateStore, administrators, credentials, availability, environments, optionsDoc.RootElement);

        // Catalogued as access-approval v1, available in the system environment, credential + environment provisioned.
        var catalog = new SecuredWorkflowCatalog(catalogStore, stateStore, "test", credentials, administrators);
        using (var version = await catalog.GetAsync("access-approval", 1, AccessContext.System, default))
        {
            version.ShouldNotBeNull();
        }

        using (var entry = await availability.GetAsync("access-approval", 1, "system", default))
        {
            entry.ShouldNotBeNull();
        }

        using (var credential = await credentials.GetAsync("controlplane", "system", AccessContext.System, default))
        {
            credential.ShouldNotBeNull();
        }

        using (var environment = await environments.GetAsync("system", AccessContext.System, default))
        {
            environment.ShouldNotBeNull();
        }

        // The §15 administrator record was established for the genesis administrator's INTERNAL identity (sys:group plus
        // the issuer clause), so a genesis-admin caller — whom the live resolver resolves to the same tag-set — may
        // approve the requests the workflow governs. This is the correctness pin for BuildGenesisAdministratorIdentity.
        SecurityTagSet genesis = SecurityTagSet.FromTags(
            [new SecurityTag("sys:group", "arazzo-admins"), new SecurityTag("sys:iss", "arazzo-keycloak")]);
        using (var admins = await catalog.GetAdministratorsAsync("access-approval", default))
        {
            admins.ShouldNotBeNull();
            admins!.RootElement.IsAdministeredBy(genesis).ShouldBeTrue();
        }
    }

    [TestMethod]
    public async Task System_workflow_install_is_a_noop_when_not_opted_in()
    {
        using ParsedJsonDocument<DeploymentBootstrapOptions> optionsDoc = ParsedJsonDocument<DeploymentBootstrapOptions>.Parse(OptionsJson);

        var catalogStore = new InMemoryWorkflowCatalogStore();
        var stateStore = new InMemoryWorkflowStateStore();
        var administrators = new InMemoryWorkflowAdministratorStore();
        var credentials = new InMemorySourceCredentialStore();
        var availability = new InMemoryAvailabilityStore();
        var environments = new InMemoryEnvironmentStore();

        await new DefaultDeploymentBootstrap().BootstrapSystemWorkflowsAsync(
            catalogStore, stateStore, administrators, credentials, availability, environments, optionsDoc.RootElement);

        // Absent systemWorkflows option: nothing is installed.
        var catalog = new SecuredWorkflowCatalog(catalogStore, stateStore, "test", credentials, administrators);
        using (var version = await catalog.GetAsync("access-approval", 1, AccessContext.System, default))
        {
            version.ShouldBeNull();
        }

        using (var credential = await credentials.GetAsync("controlplane", "system", AccessContext.System, default))
        {
            credential.ShouldBeNull();
        }
    }
}
