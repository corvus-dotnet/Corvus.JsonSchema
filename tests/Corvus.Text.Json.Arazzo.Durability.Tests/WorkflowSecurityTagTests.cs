// <copyright file="WorkflowSecurityTagTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of the run security-tag data model (§14.2 slice 1): KVP labels set at creation are persisted in the
/// checkpoint and survive the serialize → store → resume / read-back round-trip, distinct from the free-form
/// user tags. (Row authorization over these tags is a later slice.)
/// </summary>
[TestClass]
public sealed class WorkflowSecurityTagTests
{
    private static readonly TestTimeProvider Time = new(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero));

    [TestMethod]
    public async Task Security_tags_set_at_creation_survive_serialize_and_resume()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        SecurityTag[] security = [new("tenant", "acme"), new("team", "payments"), new("team", "billing")];
        using (WorkflowRun run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, "development", Time,tags: TagSet.FromTags(["nightly"]), securityTags: SecurityTagSet.FromTags(security)))
        {
            run.SecurityTags.ToList().ShouldBe(security);
            await run.EnqueueAsync(default);
        }

        // Re-enter from the persisted checkpoint: the security tags (and the separate user tags) are restored.
        using WorkflowRun? resumed = await WorkflowRun.ResumeAsync(store, "run-1", Time, default);
        resumed.ShouldNotBeNull();
        resumed.SecurityTags.ToList().ShouldBe(security);
        resumed.Tags.ToList().ShouldBe(["nightly"]);
    }

    [TestMethod]
    public async Task The_management_client_surfaces_security_tags_on_run_detail()
    {
        var store = new InMemoryWorkflowStateStore();
        var management = new SecuredWorkflowManagement(store, owner: "ops");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        using (WorkflowRun run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, "development", Time,securityTags: SecurityTagSet.FromTags([new("tenant", "acme")])))
        {
            await run.EnqueueAsync(default);
        }

        WorkflowRunDetail? detail = await management.GetAsync("run-1", AccessContext.System, default);

        detail.ShouldNotBeNull();
        detail.Value.SecurityTags.ToList().ShouldBe([new("tenant", "acme")]);
    }

    [TestMethod]
    public void A_run_without_security_tags_reports_none()
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{ "petId": 1 }"""u8.ToArray());

        using WorkflowRun run = WorkflowRun.CreateNew(store, "run-1", "wf", doc.RootElement, "development", Time);

        run.SecurityTags.IsEmpty.ShouldBeTrue();
    }

    // ── identity-membership model (§16.5.4): a named identity is a SUBSET of the caller's whole stamped identity ──────

    [TestMethod]
    public void IsSubsetOf_is_membership_not_set_equality()
    {
        SecurityTagSet founder = SecurityTagSet.FromTags([new("sys:group", "arazzo-admins"), new("sys:iss", "arazzo-keycloak")]);

        // A richer caller identity that CONTAINS the founder (the admin who is also a subject and in another group).
        SecurityTagSet richCaller = SecurityTagSet.FromTags([
            new("sys:group", "arazzo-admins"),
            new("sys:group", "payments"),
            new("sys:sub", "u-1042"),
            new("sys:iss", "arazzo-keycloak")]);

        // Membership: the founder is a subset of the richer caller, so the caller administers/reaches it. (Under the
        // superseded exact set-equality this was FALSE — the richer caller did not equal the founder.)
        founder.IsSubsetOf(richCaller).ShouldBeTrue();

        // The reverse is not a subset (the caller is not contained in the founder).
        richCaller.IsSubsetOf(founder).ShouldBeFalse();
    }

    [TestMethod]
    public void IsSubsetOf_rejects_a_partial_or_disjoint_identity()
    {
        SecurityTagSet founder = SecurityTagSet.FromTags([new("sys:group", "arazzo-admins"), new("sys:iss", "arazzo-keycloak")]);

        // A different issuer: same group value, wrong issuer — the founder is NOT a subset (disjoint on one tag).
        SecurityTagSet otherIssuer = SecurityTagSet.FromTags([new("sys:group", "arazzo-admins"), new("sys:iss", "other-idp")]);
        founder.IsSubsetOf(otherIssuer).ShouldBeFalse();

        // A partial identity that carries only one of the founder's tags does not contain it.
        SecurityTagSet partial = SecurityTagSet.FromTags([new("sys:group", "arazzo-admins")]);
        founder.IsSubsetOf(partial).ShouldBeFalse();

        // The empty set is a subset of any identity; nothing non-empty is a subset of the empty set.
        SecurityTagSet.FromTags([]).IsSubsetOf(founder).ShouldBeTrue();
        founder.IsSubsetOf(SecurityTagSet.FromTags([])).ShouldBeFalse();
    }
}