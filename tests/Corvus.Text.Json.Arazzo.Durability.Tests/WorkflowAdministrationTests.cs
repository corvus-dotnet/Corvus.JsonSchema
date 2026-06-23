// <copyright file="WorkflowAdministrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests the workflow administration management operations on the catalog client (design §15): a base id's
/// administrator set is established by version 1, governed thereafter by an explicit administrator record, and is
/// reassignable / shareable — always authorized by current-administrator membership, never orphanable, and only when an
/// administrator store is configured.
/// </summary>
[TestClass]
public sealed class WorkflowAdministrationTests
{
    private static readonly CatalogOwner Owner = new("Team", "team@example.com", null, null);
    private static readonly SecurityTagSet Acme = SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]);
    private static readonly SecurityTagSet Globex = SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]);

    [TestMethod]
    public async Task An_added_administrator_may_publish_a_new_version()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);

        // acme establishes the base id; globex cannot version it yet.
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Globex, default));

        // acme adds globex as a co-administrator; globex may now publish.
        IReadOnlyList<SecurityTagSet> admins = await catalog.AddAdministratorAsync("flow", Globex, callerIdentity: Acme, default);
        admins.Count.ShouldBe(2);
        await catalog.AddAsync(Package("flow"), Owner, default, Globex, default);
    }

    [TestMethod]
    public async Task Only_a_current_administrator_may_change_administration()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        // globex is not an administrator, so it cannot add itself (no self-grant).
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAdministratorAsync("flow", Globex, callerIdentity: Globex, default));
    }

    [TestMethod]
    public async Task The_last_administrator_cannot_be_removed()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await catalog.RemoveAdministratorAsync("flow", Acme, callerIdentity: Acme, default));
    }

    [TestMethod]
    public async Task Transfer_reassigns_administration_to_a_new_identity()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        // acme hands the workflow off to globex entirely.
        IReadOnlyList<SecurityTagSet> admins = await catalog.TransferAdministrationAsync("flow", [Globex], callerIdentity: Acme, default);
        admins.Count.ShouldBe(1);

        // globex may now publish; acme may not.
        await catalog.AddAsync(Package("flow"), Owner, default, Globex, default);
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Acme, default));
    }

    [TestMethod]
    public async Task Removing_an_administrator_revokes_publishing()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);
        await catalog.AddAdministratorAsync("flow", Globex, callerIdentity: Acme, default);

        // acme removes globex again; globex can no longer publish.
        await catalog.RemoveAdministratorAsync("flow", Globex, callerIdentity: Acme, default);
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Globex, default));
    }

    [TestMethod]
    public async Task GetAdministrators_falls_back_to_the_version_1_identity()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        // No explicit record has been materialized — administration defaults to version 1's stamped identity.
        IReadOnlyList<SecurityTagSet> admins = await catalog.GetAdministratorsAsync("flow", default);
        admins.Count.ShouldBe(1);
        WorkflowIdentity.SameAdministrator(admins[0], Acme).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Adding_an_existing_administrator_is_an_idempotent_no_op()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        IReadOnlyList<SecurityTagSet> admins = await catalog.AddAdministratorAsync("flow", Acme, callerIdentity: Acme, default);
        admins.Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task Without_an_administrator_store_management_is_unsupported()
    {
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops");
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await catalog.AddAdministratorAsync("flow", Globex, callerIdentity: Acme, default));
    }

    private static SecuredWorkflowCatalog NewCatalog(out InMemoryWorkflowAdministratorStore administrators)
    {
        administrators = new InMemoryWorkflowAdministratorStore();
        return new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", credentials: null, administrators: administrators);
    }

    private static ReadOnlyMemory<byte> Package(string workflowId)
    {
        byte[] workflow = Encoding.UTF8.GetBytes($$"""
        {
          "arazzo": "1.1.0",
          "info": { "title": "Flow", "description": "A flow." },
          "workflows": [ { "workflowId": "{{workflowId}}", "steps": [] } ]
        }
        """);
        return CatalogPackage.Build(workflow, []);
    }
}
