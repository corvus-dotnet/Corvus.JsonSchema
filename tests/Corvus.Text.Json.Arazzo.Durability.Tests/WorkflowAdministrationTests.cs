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

    // A richer caller identity that strictly CONTAINS Acme (a superset): the acme tenant plus a person sub-claim.
    private static readonly SecurityTagSet AcmeAlice = SecurityTagSet.FromTags([new SecurityTag("tenant", "acme"), new SecurityTag("sub", "alice")]);

    [TestMethod]
    public async Task An_added_administrator_may_publish_a_new_version()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);

        // acme establishes the base id; globex cannot version it yet.
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Globex, default, default));

        // acme adds globex as a co-administrator; globex may now publish.
        using (ParsedJsonDocument<WorkflowAdministrators> admins = await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme))
        {
            admins.RootElement.AdministratorCount.ShouldBe(2);
        }

        await catalog.AddAsync(Package("flow"), Owner, default, Globex, default, default);
    }

    [TestMethod]
    public async Task Only_a_current_administrator_may_change_administration()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);

        // globex is not an administrator, so it cannot add itself (no self-grant).
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await AddAdministratorAsync(catalog, "flow", Globex, caller: Globex));
    }

    [TestMethod]
    public async Task A_caller_whose_identity_contains_an_administrator_may_change_administration()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default); // acme is the sole administrator

        // Membership (§16.5.4): AcmeAlice strictly CONTAINS the stored acme identity, so it administers the workflow
        // and may add a co-administrator — even though its identity is not set-equal to acme. Under the superseded
        // set-equality mutation gate this threw WorkflowAdministrationException.
        using ParsedJsonDocument<WorkflowAdministrators> admins = await AddAdministratorAsync(catalog, "flow", Globex, caller: AcmeAlice);
        admins.RootElement.AdministratorCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task Adding_a_more_specific_identity_than_an_administrator_is_a_genuine_addition()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default); // acme = {tenant=acme}

        // Add-idempotency stays EXACT set-equality (an identity operation), not membership: AcmeAlice is a different,
        // more specific identity, so adding it is a real second administrator, never an idempotent no-op.
        using ParsedJsonDocument<WorkflowAdministrators> admins = await AddAdministratorAsync(catalog, "flow", AcmeAlice, caller: Acme);
        admins.RootElement.AdministratorCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task The_last_administrator_cannot_be_removed()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await catalog.RemoveAdministratorAsync("flow", SecurityIdentityDigest.Compute(Acme)!, callerIdentity: Acme, default));
    }

    [TestMethod]
    public async Task Transfer_reassigns_administration_to_a_new_identity()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);

        // acme hands the workflow off to globex entirely.
        using (ParsedJsonDocument<WorkflowAdministrators> admins = await catalog.TransferAdministrationAsync("flow", [Globex], callerIdentity: Acme, default))
        {
            admins.RootElement.AdministratorCount.ShouldBe(1);
        }

        // globex may now publish; acme may not.
        await catalog.AddAsync(Package("flow"), Owner, default, Globex, default, default);
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default));
    }

    [TestMethod]
    public async Task Removing_an_administrator_revokes_publishing()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);
        (await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme)).Dispose();

        // acme removes globex again (by its identity digest); globex can no longer publish.
        (await catalog.RemoveAdministratorAsync("flow", SecurityIdentityDigest.Compute(Globex)!, callerIdentity: Acme, default)).Dispose();
        await Should.ThrowAsync<WorkflowAdministrationException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Globex, default, default));
    }

    [TestMethod]
    public async Task The_creator_is_an_explicit_administrator_from_creation()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);

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
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);
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
            await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default));
    }

    [TestMethod]
    public async Task Adding_an_existing_administrator_is_an_idempotent_no_op()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default);

        using ParsedJsonDocument<WorkflowAdministrators> admins = await AddAdministratorAsync(catalog, "flow", Acme, caller: Acme);
        admins.RootElement.AdministratorCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task An_identity_less_first_version_is_not_administered_by_whoever_comes_next()
    {
        // V-3 and P1-13 of the 2026-08-07 audit. An empty tag set is a subset of every set, so a version 1 that carries
        // no identity was administered by anyone: the next caller published version 2 of somebody else's workflow id.
        SecurityTagSet mallory = SecurityTagSet.FromTags([new SecurityTag("sys:sub", "mallory")]);

        // No empty identity is recorded as an administrator, and none is inherited.
        SecuredWorkflowCatalog explicitCatalog = NewCatalog(out InMemoryWorkflowAdministratorStore administrators);
        (await explicitCatalog.AddAsync(Package("orphan"), Owner, default, SecurityTagSet.Empty, default, default)).Dispose();
        (await administrators.GetAsync("orphan", default)).ShouldBeNull();
        await Should.ThrowAsync<WorkflowAdministrationException>(async () => (await explicitCatalog.AddAsync(Package("orphan"), Owner, default, mallory, default, default)).Dispose());
        await Should.ThrowAsync<WorkflowAdministrationException>(async () => (await AddAdministratorAsync(explicitCatalog, "orphan", mallory, mallory)).Dispose());

        // A posture that identifies nobody stays consistent with itself: the identity-less caller publishes again.
        (await explicitCatalog.AddAsync(Package("orphan"), Owner, default, SecurityTagSet.Empty, default, default)).Dispose();
    }

    [TestMethod]
    public async Task Without_an_administrator_store_publishing_and_administration_are_unsupported()
    {
        // A runner reads the catalog and needs no store. A client that publishes, or reads or changes administration,
        // does: there is no administrator derived from version 1 to fall back on (ADR 0007, V-3 of the 2026-08-07 audit).
        var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "ops", administrators: null);

        (await Should.ThrowAsync<NotSupportedException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default))).Message.ShouldContain("administrator store");
        await Should.ThrowAsync<NotSupportedException>(async () => await catalog.GetAdministratorsAsync("flow", default));
        await Should.ThrowAsync<NotSupportedException>(async () => await AddAdministratorAsync(catalog, "flow", Globex, caller: Acme));
    }

    // Adds a resolved identity with no kind/label (the bare identity form) and returns the resulting record.
    private static ValueTask<ParsedJsonDocument<WorkflowAdministrators>> AddAdministratorAsync(SecuredWorkflowCatalog catalog, string baseWorkflowId, SecurityTagSet identity, SecurityTagSet caller)
        => catalog.AddAdministratorAsync(baseWorkflowId, identity, default(AdminKind), hasKind: false, default, hasLabel: false, caller, default);

    [TestMethod]
    public async Task An_authors_security_tags_are_reach_and_not_part_of_the_administrator_identity()
    {
        SecuredWorkflowCatalog catalog = NewCatalog(out _);
        SecurityTagSet blue = SecurityTagSet.FromTags([new SecurityTag("team", "blue")]);

        // acme publishes version 1 under an author tag. The tag widens the version's reach and nothing else (P1-13).
        using (ParsedJsonDocument<CatalogVersion> version = await catalog.AddAsync(Package("flow"), Owner, default, Acme, blue, default))
        {
            HasTag(version.RootElement.SecurityTagsValue, "team", "blue").ShouldBeTrue();
        }

        // The recorded administrator is acme's identity alone, so a tenant peer without the author tag administers the
        // workflow and publishes its next version.
        using (ParsedJsonDocument<WorkflowAdministrators>? admins = await catalog.GetAdministratorsAsync("flow", default))
        {
            admins.ShouldNotBeNull();
            admins!.RootElement.AdministratorCount.ShouldBe(1);
            admins.RootElement.IsAdministeredBy(Acme).ShouldBeTrue();
        }

        (await catalog.AddAsync(Package("flow"), Owner, default, Acme, default, default)).Dispose();

        // An author cannot write the reserved keyspace through their tags.
        await Should.ThrowAsync<ArgumentException>(async () =>
            await catalog.AddAsync(Package("flow"), Owner, default, Acme, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "globex")]), default));
    }

    private static bool HasTag(SecurityTagSet tags, string key, string value)
    {
        foreach (SecurityTag tag in tags)
        {
            if (tag.Key == key && tag.Value == value)
            {
                return true;
            }
        }

        return false;
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
