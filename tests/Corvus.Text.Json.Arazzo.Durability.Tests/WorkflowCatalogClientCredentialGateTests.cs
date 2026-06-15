// <copyright file="WorkflowCatalogClientCredentialGateTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests the catalog-time source-credential usage gate (design §13): cataloguing a workflow that declares a
/// credential-protected source is refused unless the submitter — by the version's stamped security tags — is entitled
/// to use a binding for that source. This is the earlier of the two usage checks (the run-time bind being the backstop).
/// </summary>
[TestClass]
public sealed class WorkflowCatalogClientCredentialGateTests
{
    private static readonly CatalogOwner Owner = new("Team", "team@example.com", null, null);

    [TestMethod]
    public async Task Cataloguing_a_workflow_that_declares_an_unentitled_source_is_rejected()
    {
        var credentials = new InMemorySourceCredentialStore();
        await credentials.AddAsync(
            new SourceCredentialDefinition(
                "petstore",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "keyvault://petstore")],
                UsageTags: SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")])),
            "system",
            default);

        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", credentials);

        // A submitter not entitled to the petstore binding may not catalogue a workflow that declares it.
        SourceCredentialAccessDeniedException ex = await Should.ThrowAsync<SourceCredentialAccessDeniedException>(async () =>
            await catalog.AddAsync(Package("globex-flow"), Owner, default, SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]), default));
        ex.DeniedSources.ShouldBe(["petstore"]);

        // An entitled submitter may.
        CatalogVersion added = await catalog.AddAsync(Package("acme-flow"), Owner, default, SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]), default);
        ((string)added.WorkflowId).ShouldContain("acme-flow");
    }

    [TestMethod]
    public async Task A_source_with_no_binding_is_allowed()
    {
        // petstore has no credential binding at all — it is unauthenticated (or bindings come later), so declaring it
        // is allowed for any submitter.
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", new InMemorySourceCredentialStore());
        CatalogVersion version = await catalog.AddAsync(Package("flow"), Owner, default, SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]), default);
        ((string)version.WorkflowId).ShouldContain("flow");
    }

    [TestMethod]
    public async Task With_no_credential_store_the_gate_is_off()
    {
        // No credential store wired → no catalog-time check (back-compat).
        var catalog = new WorkflowCatalogClient(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops");
        CatalogVersion version = await catalog.AddAsync(Package("flow"), Owner, default, default, default);
        ((string)version.WorkflowId).ShouldContain("flow");
    }

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Flow", "description": "A flow." },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.json", "type": "openapi" } ],
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        byte[] petstore = Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"Petstore","version":"1.0.0"}}""");
        return CatalogPackage.Build(workflow, [new KeyValuePair<string, byte[]>("petstore", petstore)]);
    }
}