// <copyright file="WorkflowAdministrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using AdminKind = Corvus.Text.Json.Arazzo.Durability.Security.WorkflowAdministrators.AdministratorIdentity.KindEntity;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests the workflow administration management operations on the catalog client (design §15): a base id's
/// administrator set is established by version 1, governed thereafter by an explicit administrator record, and is
/// reassignable / shareable — always authorized by current-administrator membership, never orphanable, and only when an
/// administrator store is configured. Each operation hands back the administration record (a pooled document); removal
/// is keyed by the administrator's stable identity digest.
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
        using (ParsedJsonDocument<WorkflowAdministrators> admins = await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme))
        {
            admins.RootElement.AdministratorCount.ShouldBe(2);
        }

        await catalog.AddAsync(Package("flow"), Owner, default, Globex, default);
    }

    [TestMethod]
    public async Task Only_a_current_administrator_may_change_administration()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        // globex is not an administrator, so it cannot add itself (no self-grant).
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await AddAdministratorAsync(catalog, "flow", Globex, caller: Globex));
    }

    [TestMethod]
    public async Task The_last_administrator_cannot_be_removed()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await catalog.RemoveAdministratorAsync("flow", SecurityIdentityDigest.Compute(Acme)!, callerIdentity: Acme, default));
    }

    [TestMethod]
    public async Task Transfer_reassigns_administration_to_a_new_identity()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        // acme hands the workflow off to globex entirely.
        using (ParsedJsonDocument<WorkflowAdministrators> admins = await catalog.TransferAdministrationAsync("flow", [Globex], callerIdentity: Acme, default))
        {
            admins.RootElement.AdministratorCount.ShouldBe(1);
        }

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
        (await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme)).Dispose();

        // acme removes globex again (by its identity digest); globex can no longer publish.
        (await catalog.RemoveAdministratorAsync("flow", SecurityIdentityDigest.Compute(Globex)!, callerIdentity: Acme, default)).Dispose();
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Globex, default));
    }

    [TestMethod]
    public async Task The_creator_is_an_explicit_administrator_from_creation()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        // Publishing version 1 materializes an EXPLICIT administrator record (§15.2) — administration is the explicit
        // store record, never an implicit version-1 derivation. The creator (acme) is the sole administrator, and the
        // record carries a real (non-None) etag because it physically exists.
        using ParsedJsonDocument<WorkflowAdministrators>? admins = await catalog.GetAdministratorsAsync("flow", default);
        admins.ShouldNotBeNull();
        admins!.RootElement.AdministratorCount.ShouldBe(1);
        admins.RootElement.IsAdministeredBy(Acme).ShouldBeTrue();
        admins.RootElement.EtagValue.IsNone.ShouldBeFalse();
    }

    [TestMethod]
    public async Task The_creator_can_be_removed_once_a_co_administrator_exists()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);

        // acme creates the workflow (becoming the explicit creator-administrator) and adds globex as a co-administrator.
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);
        (await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme)).Dispose();

        // The creator is a normal, removable administrator: globex removes acme (e.g. acme has left the organisation).
        using (ParsedJsonDocument<WorkflowAdministrators> admins = await catalog.RemoveAdministratorAsync("flow", SecurityIdentityDigest.Compute(Acme)!, callerIdentity: Globex, default))
        {
            admins.RootElement.AdministratorCount.ShouldBe(1);
            admins.RootElement.IsAdministeredBy(Globex).ShouldBeTrue();
            admins.RootElement.IsAdministeredBy(Acme).ShouldBeFalse();
        }

        // The creator can no longer administer or publish; administration is purely the explicit grants that remain.
        using ParsedJsonDocument<WorkflowAdministrators>? after = await catalog.GetAdministratorsAsync("flow", default);
        after.ShouldNotBeNull();
        after!.RootElement.IsAdministeredBy(Acme).ShouldBeFalse();
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Acme, default));
    }

    [TestMethod]
    public async Task Adding_an_existing_administrator_is_an_idempotent_no_op()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        using ParsedJsonDocument<WorkflowAdministrators> admins = await AddAdministratorAsync(catalog, "flow", Acme, caller: Acme);
        admins.RootElement.AdministratorCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task Without_an_administrator_store_management_is_unsupported()
    {
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops");
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default);

        await Should.ThrowAsync<NotSupportedException>(async () =>
            await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme));
    }

    // Adds a resolved identity with no kind/label (the bare identity form) and returns the resulting record.
    private static ValueTask<ParsedJsonDocument<WorkflowAdministrators>> AddAdministratorAsync(SecuredWorkflowCatalog catalog, string baseWorkflowId, SecurityTagSet identity, SecurityTagSet caller)
        => catalog.AddAdministratorAsync(baseWorkflowId, identity, default(AdminKind), hasKind: false, default, hasLabel: false, caller, default);

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
